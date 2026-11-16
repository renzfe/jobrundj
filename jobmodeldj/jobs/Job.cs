using jobmodeldj.Model;
using jobmodeldj.Utils;
using jobmodeldj.Exceptions;
using NLog;
using System.Diagnostics;
using System.Globalization;

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
        public virtual int Version { get { return 1; } }
        protected Logger l = LogManager.GetCurrentClassLogger();
        public abstract void Execute(JobConfiguration conf);

        /// <summary>
        /// Asks the job input (text, numbers, dates, confirmations, choices). The value comes from
        /// --param key=value, from the "JobParams" section of [JobName].json or, when jobrundj runs
        /// with -i / --interactive, from a prompt in the console. Same as conf.Prompt.
        /// </summary>
        protected IJobPrompt Prompt { get; private set; } = null!;

        // Progress throttling: at most one log line per percentage point or every PROGRESS_MIN_INTERVAL
        private static readonly TimeSpan PROGRESS_MIN_INTERVAL = TimeSpan.FromSeconds(2);
        private readonly Stopwatch _progressClock = new Stopwatch();
        private int _lastProgressPercent = -1;

        public void MainExecute(JobConfiguration conf)
        {
            l = LogManager.GetLogger(GetType().FullName);
            Prompt = conf.Prompt;
            l.Info("############# START JOB:{0} v{1} ##########", conf.JobID, this.Version);

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

        /// <summary>
        /// Reports the job progress. It writes an Info log line ("Progress 680/1000 (68%) message")
        /// and, when the job runs with --ui, it moves the progress bar of the dashboard.
        /// It can be called on every item: the log line is written only when the percentage changes,
        /// at least every 2 seconds and when the job reaches the total.
        /// </summary>
        /// <param name="current">items processed so far</param>
        /// <param name="total">total items (0 or less = unknown)</param>
        /// <param name="message">optional short description of the current step</param>
        protected void Progress(long current, long total, string? message = null)
        {
            // integer division: 100% is reached only when current >= total
            int percent = total > 0 ? (int)Math.Clamp(current * 100 / total, 0, 100) : -1;

            if (percent == _lastProgressPercent
                && _progressClock.IsRunning
                && _progressClock.Elapsed < PROGRESS_MIN_INTERVAL)
            {
                return;
            }
            _lastProgressPercent = percent;
            _progressClock.Restart();

            string text = total > 0
                ? string.Format(CultureInfo.InvariantCulture, "Progress {0}/{1} ({2}%)", current, total, percent)
                : string.Format(CultureInfo.InvariantCulture, "Progress {0}", current);
            if (!string.IsNullOrWhiteSpace(message)) text += " " + message;

            var ev = new LogEventInfo(LogLevel.Info, l.Name, text);
            ev.Properties[Global.PROGRESS_CURRENT_PROPERTY] = current;
            ev.Properties[Global.PROGRESS_TOTAL_PROPERTY] = total;
            ev.Properties[Global.PROGRESS_MESSAGE_PROPERTY] = message ?? string.Empty;
            l.Log(ev);
        }
    }
}
