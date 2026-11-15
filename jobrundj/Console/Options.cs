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
    }
}
