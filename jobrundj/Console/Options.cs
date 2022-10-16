using CommandLine.Text;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jobrundj.Console
{
    internal class Options
    {
        [Option('j', "job", Required = true, HelpText = "Set job to run.")]
        public string JobName { get; set; }
    }
}
