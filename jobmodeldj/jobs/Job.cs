using jobmodeldj.Model;
using jobmodeldj.Utils;
using jobmodeldj.Exceptions;
using NLog;
using System.Security;

namespace jobmodeldj.jobs
{
    public abstract class Job : IJob
    {
        public string JobID 
        {
            get 
            { 
                return this.GetType().Name; 
            } 
        }
        public abstract int JobRuntimeVersion { get; }
        protected Logger l = LogManager.GetCurrentClassLogger();
        public abstract void Execute(JobConfiguration conf);

        public void MainExecute(JobConfiguration conf)
        {
            l = LogManager.GetLogger(GetType().FullName);
            l.Info("############# START JOB:{0} ##########", conf.JobID);

            try
            {
                if (Global.JOBS_RUNTIME_VERSION != this.JobRuntimeVersion)
                {
                    throw new VersionException(this.JobRuntimeVersion, Global.JOBS_RUNTIME_VERSION);
                }

                Execute(conf);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                l.Info("############### END JOB:{0} ##########", conf.JobID);
            }
        }
    }
}
