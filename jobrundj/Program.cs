using jobrundj.Console;
using jobmodeldj.Model;
using jobmodeldj.Utils;
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
            string dlljobsDirecoryPath = string.Empty;
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

                dlljobsDirecoryPath = Path.Combine(appDirecoryPath, Global.JOBSTEMPDLL_FOLDER_NAME);
                l.Trace("dlljobsDirecoryPath={0}", dlljobsDirecoryPath);

                tmpDirecoryPath = Path.Combine(appDirecoryPath, Global.TEMP_FOLDER_NAME);
                l.Trace("tmpDirecoryPath={0}", tmpDirecoryPath);

                externaldlljobsDirecoryPath = Path.Combine(appDirecoryPath, Global.REFERENCE_DLL_FOLDER_NAME);
                l.Trace("externaldlljobsDirecoryPath={0}", externaldlljobsDirecoryPath);

                JobConfiguration conf = new JobConfiguration(args, executerFileName, appDirecoryPath, jobDirecoryPath, dlljobsDirecoryPath, tmpDirecoryPath, externaldlljobsDirecoryPath);
                conf.logConfig = LogManager.Configuration;

                l.Info("Start {0} - runtime job version: {1}", executerFileName, Global.JOBS_VERSION_RUNTIME);
                Executer.Execute(conf);
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