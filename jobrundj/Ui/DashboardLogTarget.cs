using NLog;
using NLog.Targets;

namespace jobrundj.Ui
{
    /// <summary>
    /// NLog target that sends the log events to the dashboard instead of writing them to the console.
    /// </summary>
    internal sealed class DashboardLogTarget : Target
    {
        public const string TARGET_NAME = "jobrundjDashboard";
        private readonly DashboardState state;

        public DashboardLogTarget(DashboardState state)
        {
            this.state = state;
            Name = TARGET_NAME;
        }

        protected override void Write(LogEventInfo logEvent)
        {
            state.Add(logEvent);
        }
    }
}
