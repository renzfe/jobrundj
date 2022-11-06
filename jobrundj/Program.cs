using jobmodeldj.Model;
using jobmodeldj.Utils;
using jobrundj.Console;
using System.Reflection;
using NLog;

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

                JobConfiguration conf = new JobConfiguration(args, executerFileName, appDirecoryPath, jobDirecoryPath, tmpDirecoryPath, externaldlljobsDirecoryPath, basePath);
                conf.logConfig = LogManager.Configuration;

                l.Info("Start {0} v{1} - jobs runtime v{2}", executerFileName, 
                                                               System.Reflection.Assembly.GetExecutingAssembly().GetName().Version, 
                                                               Global.JOBS_RUNTIME_VERSION);

                Executer executer = new Executer(conf);
                executer.Execute();
                l.Info("{0}: EXIT 0", executerFileName);
                System.Environment.Exit(0);
            }
            catch (Exception ex)
            {
                l.Error("Errore={0}", ex.Message);
                l.Warn("{0}: EXIT 1", executerFileName);
                System.Environment.Exit(1);
            }
        }
    }
}