using jobmodeldj.Model;
using jobmodeldj.Utils;
using jobrundj.Console;
using jobrundj.Ui;
using System.Reflection;
using System.Diagnostics;
using NLog;
using CommandLine;

namespace jobrundj
{
    internal class Program
    {
        private static Logger l = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            string executerFileName = string.Empty;
            string appDirecoryPath = string.Empty;
            string jobDirecoryPath = string.Empty;
            string externaldlljobsDirecoryPath = string.Empty;
            string tmpDirecoryPath = string.Empty;
            string jobName = string.Empty;
            string? jobsDirectoryOverride = null;
            bool debugMode = false;
            bool uiMode = false;
            bool interactiveMode = false;
            JobDashboard? dashboard = null;
            // read directly from args (not from the parser) so that the window stays open
            // also when the command line is wrong, e.g. a shortcut with a mistyped option
            bool pauseMode = args.Contains("--pause");

            try
            {
                // arguments are parsed first: with --ui the console log is replaced by the dashboard
                // before anything else is logged
                var parser = new Parser(settings =>
                {
                    settings.IgnoreUnknownArguments = true;
                    // --param can be repeated: --param a=1 --param b=2
                    settings.AllowMultiInstance = true;
                });
                parser.ParseArguments<Options>(args)
                        .WithParsed(o =>
                        {
                            jobName = o.JobName;
                            jobsDirectoryOverride = o.JobsDirectory;
                            debugMode = o.Debug;
                            uiMode = o.Ui;
                            interactiveMode = o.Interactive;
                        })
                        .WithNotParsed(HandleParseError);

                if (debugMode && !Debugger.IsAttached)
                {
                    l.Warn("Debug mode: waiting for a debugger to attach to process {0}..", Environment.ProcessId);
                    while (!Debugger.IsAttached)
                    {
                        Thread.Sleep(250);
                    }
                    l.Info("Debugger attached");
                }
                // compile with debug symbols whenever a debugger is attached (e.g. F5 from VS Code)
                debugMode = debugMode || Debugger.IsAttached;

                string appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? string.Empty;
                if (interactiveMode && !string.IsNullOrEmpty(jobName))
                {
                    if (!ConsoleJobPrompt.IsSupported())
                    {
                        interactiveMode = false;
                        l.Warn("-i ignored: the console is not interactive or the input/output is redirected");
                    }
                    else if (uiMode)
                    {
                        // the live dashboard and the prompts cannot share the console
                        uiMode = false;
                        l.Warn("--ui ignored: not available with -i (--interactive)");
                    }
                }
                if (uiMode && !string.IsNullOrEmpty(jobName))
                {
                    if (JobDashboard.IsSupported())
                    {
                        dashboard = new JobDashboard(jobName, appVersion);
                    }
                    else
                    {
                        l.Warn("--ui ignored: the console is not interactive or the output is redirected");
                    }
                }

                executerFileName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                l.Trace("ExecuterFileName={0}", executerFileName);

                appDirecoryPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                l.Trace("appDirecoryPath={0}", appDirecoryPath);

                jobDirecoryPath = Path.Combine(appDirecoryPath, Global.JOBS_FOLDER_NAME);
                if (!string.IsNullOrEmpty(jobsDirectoryOverride))
                {
                    jobDirecoryPath = Path.GetFullPath(jobsDirectoryOverride);
                    l.Debug("jobDirecoryPath overridden={0}", jobDirecoryPath);
                }
                l.Trace("jobDirecoryPath={0}", jobDirecoryPath);

                tmpDirecoryPath = Path.Combine(appDirecoryPath, Global.TEMP_FOLDER_NAME);
                l.Trace("tmpDirecoryPath={0}", tmpDirecoryPath);

                externaldlljobsDirecoryPath = Path.Combine(appDirecoryPath, Global.REFERENCE_DLL_FOLDER_NAME);
                l.Trace("externaldlljobsDirecoryPath={0}", externaldlljobsDirecoryPath);

                string basePath = Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location);

                l.Info("Start {0} v{1} - jobs runtime v{2}", executerFileName,
                                                               System.Reflection.Assembly.GetExecutingAssembly().GetName().Version,
                                                               Global.JOBS_RUNTIME_VERSION);

                if (!string.IsNullOrEmpty(jobName))
                {
                    JobConfiguration conf = new JobConfiguration(jobName, args, executerFileName, appDirecoryPath, jobDirecoryPath, tmpDirecoryPath, externaldlljobsDirecoryPath, basePath);
                    conf.logConfig = LogManager.Configuration;
                    conf.Prompt = interactiveMode ? new ConsoleJobPrompt(conf) : new JobPrompt(conf);

                    l.Info($"Configuring job to run: {conf.JobID}");

                    JobRunner jobrunner = new JobRunner(conf, debugMode);
                    if (dashboard != null)
                    {
                        dashboard.Run(jobrunner.Run);
                    }
                    else
                    {
                        jobrunner.Run();
                    }
                }

                l.Info("{0}: EXIT 0", executerFileName);
                WaitForKeyIfRequested(pauseMode);
                System.Environment.Exit(0);
            }
            catch (Exception ex)
            {
                l.Error("Error={0}", ex.Message);
                l.Warn("{0}: EXIT 1", executerFileName);
                WaitForKeyIfRequested(pauseMode);
                System.Environment.Exit(1);
            }

            // --pause: keeps the window of a desktop shortcut open to read the result.
            // Never blocks when input or output are redirected (scheduler, scripts, CI).
            static void WaitForKeyIfRequested(bool pause)
            {
                if (!pause || System.Console.IsInputRedirected || System.Console.IsOutputRedirected) return;
                try
                {
                    LogManager.Flush();
                    System.Console.WriteLine();
                    System.Console.Write("Press any key to close...");
                    System.Console.ReadKey(intercept: true);
                    System.Console.WriteLine();
                }
                catch (InvalidOperationException)
                {
                    // no console attached
                }
            }

            static void HandleParseError(IEnumerable<Error> errs)
            {
                foreach (var err in errs)
                {
                    l.Error(err.Tag);
                }
                throw new Exception("Command line parser exception");
            }

        }
    }
}
