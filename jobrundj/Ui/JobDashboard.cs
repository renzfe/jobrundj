using System.Runtime.ExceptionServices;
using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace jobrundj.Ui
{
    /// <summary>
    /// Live console dashboard for a job run (option --ui): status, elapsed time, progress bar
    /// (fed by Job.Progress), log counters and the latest log messages, followed by a summary.
    /// The console log targets are replaced by <see cref="DashboardLogTarget"/>; the file targets keep working.
    /// </summary>
    internal sealed class JobDashboard
    {
        private static readonly Color Accent = Color.DodgerBlue1;
        private const int REFRESH_MS = 100;
        private const int JOB_THREAD_STACK_SIZE = 16 * 1024 * 1024;

        private readonly string jobId;
        private readonly string appVersion;
        private readonly DashboardState state;
        private readonly DashboardLogTarget target;
        private readonly bool unicode;

        /// <summary>True when the console can host the live dashboard (interactive, not redirected).</summary>
        public static bool IsSupported()
        {
            if (System.Console.IsOutputRedirected) return false;
            // must happen before the first use of AnsiConsole: Spectre.Console keeps the console writer
            // and detects Unicode support when it is created
            UseUtf8Output();
            bool supported = AnsiConsole.Profile.Capabilities.Interactive;
            if (!supported) RestoreOutputEncoding();
            return supported;
        }

        private static Encoding? originalOutputEncoding;

        /// <summary>Box drawing and bar characters on the classic Windows console (code page 437/850).</summary>
        private static void UseUtf8Output()
        {
            if (OperatingSystem.IsWindows() && originalOutputEncoding == null && System.Console.OutputEncoding.CodePage != Encoding.UTF8.CodePage)
            {
                originalOutputEncoding = System.Console.OutputEncoding;
                System.Console.OutputEncoding = Encoding.UTF8;
            }
        }

        /// <summary>On Windows the console code page outlives the process: put it back.</summary>
        private static void RestoreOutputEncoding()
        {
            if (originalOutputEncoding != null)
            {
                try { System.Console.OutputEncoding = originalOutputEncoding; } catch { }
                originalOutputEncoding = null;
            }
        }

        public JobDashboard(string jobId, string appVersion)
        {
            this.jobId = jobId;
            this.appVersion = appVersion;
            state = new DashboardState(jobId);
            target = new DashboardLogTarget(state);
            unicode = AnsiConsole.Profile.Capabilities.Unicode;

            AttachLogging(LogManager.Configuration);
            // NLog.config has autoReload: keep the console quiet if the file is changed while the job runs
            LogManager.ConfigurationChanged += (_, e) =>
            {
                if (e.ActivatedConfiguration != null && e.ActivatedConfiguration.FindTargetByName(DashboardLogTarget.TARGET_NAME) == null)
                {
                    AttachLogging(e.ActivatedConfiguration);
                }
            };
        }

        /// <summary>
        /// Runs the job on a worker thread while drawing the dashboard, then prints the summary.
        /// Exceptions thrown by the job are rethrown after the summary.
        /// </summary>
        public void Run(Action job)
        {
            Exception? error = null;
            var worker = new Thread(() =>
            {
                try { job(); }
                catch (Exception ex) { error = ex; }
            }, JOB_THREAD_STACK_SIZE)
            {
                IsBackground = true,
                Name = "jobrundj job"
            };

            try
            {
                AnsiConsole.Live(Render(0))
                    .AutoClear(true)
                    .Overflow(VerticalOverflow.Ellipsis)
                    .Cropping(VerticalOverflowCropping.Top)
                    .Start(ctx =>
                    {
                        worker.Start();
                        int frame = 0;
                        while (!worker.Join(REFRESH_MS))
                        {
                            ctx.UpdateTarget(Render(++frame));
                        }
                    });

                state.Finish(error);
                AnsiConsole.Write(RenderSummary(error));
            }
            finally
            {
                RestoreOutputEncoding();
            }

            if (error != null)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }
        }

        #region logging

        private void AttachLogging(LoggingConfiguration? config)
        {
            if (config == null)
            {
                config = new LoggingConfiguration();
                LogManager.Configuration = config;
            }

            foreach (var rule in config.LoggingRules)
            {
                RemoveConsoleTargets(rule);
            }

            if (config.FindTargetByName(DashboardLogTarget.TARGET_NAME) == null)
            {
                config.AddTarget(target);
            }
            config.LoggingRules.Insert(0, new LoggingRule("*", LogLevel.Info, target));
            LogManager.ReconfigExistingLoggers();
        }

        private static void RemoveConsoleTargets(LoggingRule rule)
        {
            for (int i = rule.Targets.Count - 1; i >= 0; i--)
            {
                if (IsConsoleTarget(rule.Targets[i])) rule.Targets.RemoveAt(i);
            }
            foreach (var child in rule.ChildRules)
            {
                RemoveConsoleTargets(child);
            }
        }

        private static bool IsConsoleTarget(Target t)
        {
            Target inner = Unwrap(t);
            return inner is ConsoleTarget || inner is ColoredConsoleTarget;
        }

        private static Target Unwrap(Target t)
        {
            while (t is WrapperTargetBase wrapper && wrapper.WrappedTarget != null)
            {
                t = wrapper.WrappedTarget;
            }
            return t;
        }

        /// <summary>Log files written for this job (rules matching the job logger, file already created).</summary>
        private List<string> GetLogFiles()
        {
            var files = new List<string>();
            var config = LogManager.Configuration;
            if (config == null) return files;

            string jobLogger = $"jobmodeldj.jobs.{jobId}";
            var probe = new LogEventInfo(LogLevel.Info, jobLogger, string.Empty);
            foreach (var rule in config.LoggingRules)
            {
                if (!rule.NameMatches(jobLogger)) continue;
                foreach (var t in rule.Targets)
                {
                    if (Unwrap(t) is FileTarget fileTarget && fileTarget.FileName != null)
                    {
                        string path = Path.GetFullPath(fileTarget.FileName.Render(probe));
                        if (File.Exists(path) && !files.Contains(path)) files.Add(path);
                    }
                }
            }
            return files;
        }

        #endregion

        #region rendering

        private IRenderable Render(int frame)
        {
            var s = state.Snapshot();
            int width = Math.Max(40, AnsiConsole.Profile.Width);
            int height = Math.Max(15, AnsiConsole.Profile.Height);

            var info = new Grid();
            info.AddColumn(new GridColumn().NoWrap().PadRight(3));
            info.AddColumn(new GridColumn());

            info.AddRow(Label("Job"), new Markup($"[bold]{Markup.Escape(jobId)}[/]"));
            info.AddRow(Label("Status"), StatusMarkup(s.Phase, frame));
            info.AddRow(Label("Elapsed"), new Markup($"[bold]{FormatDuration(s.Elapsed)}[/]"));
            if (s.HasProgress)
            {
                info.AddRow(Label("Progress"), ProgressMarkup(s, Math.Clamp(width - 45, 10, 60)));
                if (!string.IsNullOrWhiteSpace(s.ProgressMessage))
                {
                    info.AddRow(Text.Empty, new Text(Truncate(s.ProgressMessage, width - 20), new Style(Color.Grey70)));
                }
                if (s.Remaining.HasValue)
                {
                    info.AddRow(Label("Remaining"), new Markup($"~ {FormatDuration(s.Remaining.Value)}"));
                }
            }
            info.AddRow(Label("Log"), CountersMarkup(s));

            var header = new Panel(info)
                .Header($" [bold {Accent.ToMarkup()}]jobrundj[/] [grey]v{Markup.Escape(appVersion)}[/] ")
                .RoundedBorder()
                .BorderColor(Accent)
                .Padding(1, 0)
                .Expand();

            int infoRows = 4 + (s.HasProgress ? 1 : 0)
                + (s.HasProgress && !string.IsNullOrWhiteSpace(s.ProgressMessage) ? 1 : 0)
                + (s.Remaining.HasValue ? 1 : 0);
            int maxLines = Math.Max(3, height - (infoRows + 2) - 2 - 1);

            var activity = new Grid();
            activity.AddColumn(new GridColumn().NoWrap().PadRight(2));
            activity.AddColumn(new GridColumn().NoWrap().PadRight(2));
            activity.AddColumn(new GridColumn().NoWrap());
            var lines = s.Recent.Skip(Math.Max(0, s.Recent.Count - maxLines)).ToList();
            foreach (var line in lines)
            {
                activity.AddRow(
                    new Text(line.Time.ToString("HH:mm:ss"), new Style(Color.Grey50)),
                    LevelText(line.Level),
                    new Text(Truncate(line.Message, width - 26), MessageStyle(line.Level)));
            }

            IRenderable activityContent = lines.Count > 0
                ? activity
                : new Text("Waiting for log messages…", new Style(Color.Grey50, decoration: Decoration.Italic));

            var activityPanel = new Panel(activityContent)
                .Header(" [grey70]Activity[/] ")
                .RoundedBorder()
                .BorderColor(Color.Grey35)
                .Padding(1, 0)
                .Expand();

            return new Rows(header, activityPanel);
        }

        private IRenderable RenderSummary(Exception? error)
        {
            var s = state.Snapshot();
            bool ok = error == null;
            Color color = ok ? Color.Green3 : Color.Red3;
            string icon = ok ? (unicode ? "✔" : "OK") : (unicode ? "✖" : "X");

            var grid = new Grid();
            grid.AddColumn(new GridColumn().NoWrap().PadRight(3));
            grid.AddColumn(new GridColumn());

            grid.AddRow(Label("Result"), ok
                ? new Markup($"[bold {color.ToMarkup()}]Completed successfully[/]")
                : new Markup($"[bold {color.ToMarkup()}]Failed[/]"));
            if (!ok)
            {
                grid.AddRow(Label("Error"), new Text(error!.Message, new Style(Color.Red1)));
            }
            grid.AddRow(Label("Started"), new Text(s.StartedAt.ToString("yyyy-MM-dd HH:mm:ss")));
            grid.AddRow(Label("Duration"), new Markup($"[bold]{FormatDuration(s.Elapsed)}[/]"));
            if (s.HasProgress)
            {
                grid.AddRow(Label("Progress"), new Text(s.ProgressTotal > 0
                    ? $"{s.ProgressCurrent:N0} / {s.ProgressTotal:N0} ({Percent(s)}%)"
                    : $"{s.ProgressCurrent:N0}"));
            }
            grid.AddRow(Label("Log"), CountersMarkup(s));

            var logFiles = GetLogFiles();
            if (logFiles.Count > 0)
            {
                grid.AddRow(Label("Log files"), new Rows(logFiles.Select(f => new Text(f, new Style(Color.Grey70)))));
            }
            grid.AddRow(Label("Exit code"), new Markup($"[bold]{(ok ? 0 : 1)}[/]"));

            var panel = new Panel(grid)
                .Header($" [bold {color.ToMarkup()}]{icon} {Markup.Escape(jobId)}[/] ")
                .RoundedBorder()
                .BorderColor(color)
                .Padding(1, 0)
                .Expand();

            if (s.Issues.Count == 0)
            {
                return panel;
            }

            var table = new Table()
                .RoundedBorder()
                .BorderColor(Color.Grey35)
                .Title($"[grey70]Warnings and errors[/] [grey50](last {Math.Min(10, s.Issues.Count)} of {s.WarnCount + s.ErrorCount})[/]");
            table.AddColumn(new TableColumn("[grey70]Time[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey70]Level[/]").NoWrap());
            table.AddColumn(new TableColumn("[grey70]Message[/]"));
            foreach (var line in s.Issues.Skip(Math.Max(0, s.Issues.Count - 10)))
            {
                table.AddRow(
                    new Text(line.Time.ToString("HH:mm:ss"), new Style(Color.Grey50)),
                    LevelText(line.Level),
                    new Text(line.Message, MessageStyle(line.Level)));
            }

            return new Rows(panel, table);
        }

        private IRenderable StatusMarkup(DashboardPhase phase, int frame)
        {
            var frames = (unicode ? Spinner.Known.Dots : Spinner.Known.Line).Frames;
            string spin = Markup.Escape(frames[frame % frames.Count]);
            return phase switch
            {
                DashboardPhase.Preparing => new Markup($"[yellow]{spin}[/] [yellow]Preparing[/] [grey](loading and compiling the job)[/]"),
                DashboardPhase.Running => new Markup($"[{Accent.ToMarkup()}]{spin}[/] [bold {Accent.ToMarkup()}]Running[/]"),
                DashboardPhase.Completed => new Markup("[green3]Completed[/]"),
                _ => new Markup("[red3]Failed[/]"),
            };
        }

        private IRenderable ProgressMarkup(DashboardSnapshot s, int barWidth)
        {
            if (s.ProgressTotal <= 0)
            {
                return new Markup($"[bold]{s.ProgressCurrent:N0}[/] [grey]items[/]");
            }

            int percent = Percent(s);
            int filled = (int)Math.Round(barWidth * percent / 100.0);
            char full = unicode ? '━' : '#';
            char empty = unicode ? '━' : '-';
            return new Markup(
                $"[{Accent.ToMarkup()}]{new string(full, filled)}[/][grey23]{new string(empty, barWidth - filled)}[/]" +
                $"  [bold]{percent,3}%[/]  [grey]{s.ProgressCurrent:N0} / {s.ProgressTotal:N0}[/]");
        }

        private static IRenderable CountersMarkup(DashboardSnapshot s)
        {
            static string Count(int n, string color) => n == 0 ? $"[grey50]{n:N0}[/]" : $"[bold {color}]{n:N0}[/]";
            return new Markup(
                $"[grey70]Info[/] {Count(s.InfoCount, "steelblue1")}   " +
                $"[grey70]Warnings[/] {Count(s.WarnCount, "orange1")}   " +
                $"[grey70]Errors[/] {Count(s.ErrorCount, "red1")}");
        }

        private static IRenderable LevelText(LogLevel level)
        {
            if (level >= LogLevel.Fatal) return new Text("FATAL", new Style(Color.White, Color.Red3, Decoration.Bold));
            if (level == LogLevel.Error) return new Text("ERROR", new Style(Color.Red1, decoration: Decoration.Bold));
            if (level == LogLevel.Warn) return new Text("WARN ", new Style(Color.Orange1, decoration: Decoration.Bold));
            if (level == LogLevel.Info) return new Text("INFO ", new Style(Color.SteelBlue1));
            return new Text(level.Name.ToUpperInvariant().PadRight(5), new Style(Color.Grey50));
        }

        private static Style MessageStyle(LogLevel level)
        {
            if (level >= LogLevel.Error) return new Style(Color.Red1);
            if (level == LogLevel.Warn) return new Style(Color.Orange1);
            return Style.Plain;
        }

        private static IRenderable Label(string text) => new Text(text, new Style(Color.Grey62));

        private static int Percent(DashboardSnapshot s) =>
            s.ProgressTotal > 0 ? (int)Math.Clamp(s.ProgressCurrent * 100 / s.ProgressTotal, 0, 100) : 0;

        private static string FormatDuration(TimeSpan t) =>
            t.TotalHours >= 1 ? $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";

        private static string Truncate(string text, int max)
        {
            max = Math.Max(10, max);
            return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
        }

        #endregion
    }
}
