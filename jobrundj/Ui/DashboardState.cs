using jobmodeldj.Utils;
using NLog;

namespace jobrundj.Ui
{
    internal enum DashboardPhase { Preparing, Running, Completed, Failed }

    internal sealed record DashboardLogLine(DateTime Time, LogLevel Level, string Message);

    internal sealed record DashboardSnapshot(
        DashboardPhase Phase,
        DateTime StartedAt,
        TimeSpan Elapsed,
        int InfoCount,
        int WarnCount,
        int ErrorCount,
        bool HasProgress,
        long ProgressCurrent,
        long ProgressTotal,
        string ProgressMessage,
        TimeSpan? Remaining,
        IReadOnlyList<DashboardLogLine> Recent,
        IReadOnlyList<DashboardLogLine> Issues);

    /// <summary>
    /// Thread-safe state of the dashboard, fed by <see cref="DashboardLogTarget"/> (job thread)
    /// and read by <see cref="JobDashboard"/> (UI thread).
    /// </summary>
    internal sealed class DashboardState
    {
        private const int MAX_RECENT = 100;
        private const int MAX_ISSUES = 50;

        private readonly object sync = new object();
        private readonly string jobId;
        private readonly DateTime startedAt = DateTime.Now;
        private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        private readonly Queue<DashboardLogLine> recent = new Queue<DashboardLogLine>();
        private readonly Queue<DashboardLogLine> issues = new Queue<DashboardLogLine>();

        private DashboardPhase phase = DashboardPhase.Preparing;
        private int infoCount, warnCount, errorCount;
        private bool hasProgress;
        private long progressCurrent, progressTotal;
        private string progressMessage = string.Empty;
        // first progress sample, used to estimate the remaining time
        private TimeSpan firstProgressAt;
        private long firstProgressCurrent;
        private TimeSpan lastProgressAt;

        public DashboardState(string jobId)
        {
            this.jobId = jobId;
        }

        public void Add(LogEventInfo e)
        {
            lock (sync)
            {
                if (phase == DashboardPhase.Preparing && IsJobLogger(e.LoggerName))
                {
                    phase = DashboardPhase.Running;
                }

                if (e.Level >= LogLevel.Error) errorCount++;
                else if (e.Level == LogLevel.Warn) warnCount++;
                else if (e.Level == LogLevel.Info) infoCount++;

                if (e.HasProperties && e.Properties.TryGetValue(Global.PROGRESS_CURRENT_PROPERTY, out var cur))
                {
                    UpdateProgress(e, Convert.ToInt64(cur));
                    return; // progress lines are shown by the progress bar, not in the activity list
                }

                var line = new DashboardLogLine(e.TimeStamp, e.Level, FormatMessage(e));
                Enqueue(recent, line, MAX_RECENT);
                if (e.Level >= LogLevel.Warn) Enqueue(issues, line, MAX_ISSUES);
            }
        }

        public void Finish(Exception? error)
        {
            lock (sync)
            {
                phase = error == null ? DashboardPhase.Completed : DashboardPhase.Failed;
                if (error != null)
                {
                    // the exception is logged by Program after the dashboard is closed: count it here
                    errorCount++;
                    Enqueue(issues, new DashboardLogLine(DateTime.Now, LogLevel.Error, error.Message), MAX_ISSUES);
                }
                clock.Stop();
            }
        }

        public DashboardSnapshot Snapshot()
        {
            lock (sync)
            {
                return new DashboardSnapshot(phase, startedAt, clock.Elapsed,
                    infoCount, warnCount, errorCount,
                    hasProgress, progressCurrent, progressTotal, progressMessage, EstimateRemaining(),
                    recent.ToArray(), issues.ToArray());
            }
        }

        private void UpdateProgress(LogEventInfo e, long current)
        {
            long total = e.Properties.TryGetValue(Global.PROGRESS_TOTAL_PROPERTY, out var tot) ? Convert.ToInt64(tot) : 0;
            string message = e.Properties.TryGetValue(Global.PROGRESS_MESSAGE_PROPERTY, out var msg) ? msg?.ToString() ?? string.Empty : string.Empty;

            // a new sequence (total changed or counter restarted) resets the estimate
            if (!hasProgress || total != progressTotal || current < progressCurrent)
            {
                firstProgressAt = clock.Elapsed;
                firstProgressCurrent = current;
            }

            hasProgress = true;
            progressCurrent = current;
            progressTotal = total;
            progressMessage = message;
            lastProgressAt = clock.Elapsed;
        }

        private TimeSpan? EstimateRemaining()
        {
            if (!hasProgress || progressTotal <= 0 || progressCurrent >= progressTotal) return null;
            long done = progressCurrent - firstProgressCurrent;
            TimeSpan spent = lastProgressAt - firstProgressAt;
            if (done <= 0 || spent < TimeSpan.FromSeconds(1)) return null;

            double secondsPerItem = spent.TotalSeconds / done;
            double remaining = secondsPerItem * (progressTotal - progressCurrent) - (clock.Elapsed - lastProgressAt).TotalSeconds;
            return TimeSpan.FromSeconds(Math.Max(0, remaining));
        }

        private bool IsJobLogger(string? loggerName)
        {
            if (string.IsNullOrEmpty(loggerName)) return false;
            return loggerName == jobId || loggerName.EndsWith("." + jobId, StringComparison.Ordinal);
        }

        private static string FormatMessage(LogEventInfo e)
        {
            string message = e.FormattedMessage ?? string.Empty;
            if (e.Exception != null && !message.Contains(e.Exception.Message, StringComparison.Ordinal))
            {
                message += " - " + e.Exception.Message;
            }
            // one line per event: stack traces and multi-line messages stay in the log files
            int newLine = message.IndexOfAny(new[] { '\r', '\n' });
            return newLine >= 0 ? message.Substring(0, newLine) + " …" : message;
        }

        private static void Enqueue(Queue<DashboardLogLine> queue, DashboardLogLine line, int max)
        {
            queue.Enqueue(line);
            while (queue.Count > max) queue.Dequeue();
        }
    }
}
