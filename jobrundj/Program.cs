using jobmodeldj.Model;
using jobmodeldj.Utils;
using jobrundj.Console;
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
            bool debugMode = false;
            
            try
            {
                executerFileName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                l.Trace("ExecuterFileName={0}", executerFileName);

                appDirecoryPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                l.Trace("appDirecoryPath={0}", appDirecoryPath);

                jobDirecoryPath = Path.Combine(appDirecoryPath, Global.JOBS_FOLDER_NAME);
                l.Trace("jobDirecoryPath={0}", jobDirecoryPath);

                tmpDirecoryPath = Path.Combine(appDirecoryPath, Global.TEMP_FOLDER_NAME);
                l.Trace("tmpDirecoryPath={0}", tmpDirecoryPath);

                externaldlljobsDirecoryPath = Path.Combine(appDirecoryPath, Global.REFERENCE_DLL_FOLDER_NAME);
                l.Trace("externaldlljobsDirecoryPath={0}", externaldlljobsDirecoryPath);

                string basePath = Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location);

                l.Info("Start {0} v{1} - jobs runtime v{2}", executerFileName, 
                                                               System.Reflection.Assembly.GetExecutingAssembly().GetName().Version, 
                                                               Global.JOBS_RUNTIME_VERSION);

                l.Debug("Parsing argument job..");
                var parser = new Parser(settings =>
                {
                    settings.IgnoreUnknownArguments = true;
                });
                parser.ParseArguments<Options>(args)
                        .WithParsed(o =>
                        {
                            jobName = o.JobName;
                            if (!string.IsNullOrEmpty(o.JobsDirectory))
                            {
                                jobDirecoryPath = Path.GetFullPath(o.JobsDirectory);
                                l.Debug("jobDirecoryPath overridden={0}", jobDirecoryPath);
                            }
                            debugMode = o.Debug;
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

                if (!string.IsNullOrEmpty(jobName))
                {
                    JobConfiguration conf = new JobConfiguration(jobName, args, executerFileName, appDirecoryPath, jobDirecoryPath, tmpDirecoryPath, externaldlljobsDirecoryPath, basePath);
                    conf.logConfig = LogManager.Configuration;

                    l.Info($"Configuring job to run: {conf.JobID}");

                    JobRunner jobrunner = new JobRunner(conf, debugMode);
                    jobrunner.Run();
                }

                l.Info("{0}: EXIT 0", executerFileName);
                System.Environment.Exit(0);
            }
            catch (Exception ex)
            {
                l.Error("Error={0}", ex.Message);
                l.Warn("{0}: EXIT 1", executerFileName);
                System.Environment.Exit(1);
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