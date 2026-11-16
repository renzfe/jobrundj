//<jobrundjReferencedAssemblies></jobrundjReferencedAssemblies>
using System;
using System.Collections.Generic;
using NLog;
using jobmodeldj.Model;

namespace jobmodeldj.jobs
{
    // Input demo:
    //   jobrundj -j JobInput -i                                   asks the values in the console
    //   jobrundj -j JobInput --param env=TEST tables=Orders,Invoices from=2026-01-01
    //                                                             no questions (e.g. scheduled runs)
    // Values can also be stored in the "JobParams" section of JobInput.json.
    class JobInput : Job
    {
        public override int JobRuntimeVersion { get { return 4; } }

        public override void Execute(JobConfiguration conf)
        {
            string env = Prompt.Choose("env", "Environment", new[] { "DEV", "TEST", "PROD" }, "DEV");
            IList<string> tables = Prompt.ChooseMany("tables", "Tables to export", new[] { "Customers", "Orders", "Invoices" });
            DateTime from = Prompt.Ask("from", "Export from date", DateTime.Today.AddDays(-30));
            int maxRows = Prompt.Ask("maxRows", "Maximum rows per table", 1000);

            if (env == "PROD" && !Prompt.Confirm("confirm", "Run the export on PROD?", false))
            {
                l.Warn("Export on PROD not confirmed");
                return;
            }

            foreach (string table in tables)
            {
                l.Info("Exporting {0} from {1:yyyy-MM-dd} on {2} (max {3} rows)", table, from, env, maxRows);
            }
        }
    }
}
