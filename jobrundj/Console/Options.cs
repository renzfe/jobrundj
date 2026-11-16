using CommandLine;

namespace jobrundj.Console
{
    public class Options
    {
        [Option('j', "job", Required = true, HelpText = "Set job to run.")]
        public string JobName { get; set; }

        [Option('d', "jobsdir", Required = false, HelpText = "Override the jobs folder (default: <app folder>/jobs).")]
        public string? JobsDirectory { get; set; }

        [Option("debug", Required = false, Default = false, HelpText = "Compile the job with debug symbols and wait for a debugger to attach.")]
        public bool Debug { get; set; }

        [Option("ui", Required = false, Default = false, HelpText = "Show a live dashboard (progress, log counters, latest messages) and a summary at the end.")]
        public bool Ui { get; set; }

        [Option('i', "interactive", Required = false, Default = false, HelpText = "Ask in the console the job input that is not passed with --param key=value or in the json file (it disables --ui).")]
        public bool Interactive { get; set; }

        [Option("param", Required = false, HelpText = "Job input values: --param key=value [key2=value2 ...] (read by the job with Prompt).")]
        public IEnumerable<string> Params { get; set; } = Enumerable.Empty<string>();

        [Option("pause", Required = false, Default = false, HelpText = "Wait for a key before closing (interactive console only), e.g. for desktop shortcuts.")]
        public bool Pause { get; set; }
    }
}
