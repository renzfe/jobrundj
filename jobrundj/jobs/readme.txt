===============================================================================
Please, add .cs file in this folder..
===============================================================================
===============================================================================
Example of job: JobTest.cs
===============================================================================
//<jobrundjReferencedAssemblies></jobrundjReferencedAssemblies>
using System;
using NLog;
using jobmodeldj.Model;

namespace jobmodeldj.jobs
{
    class JobTest : Job
    {
        public override int JobRuntimeVersion { get { return 2; } }
        
        public override void Execute(JobConfiguration conf) 
        {
            l.Info("start {0}", JobID);
            
            try
            {
                //
                //TODO here...
                //
				l.Info("It's running!");
				l.Info("Parameter {0}", conf.Json["Params:LevelOne:LevelTwo"]);
            }
            catch (Exception ex)
            {
                l.Error("errore {0} - {1}", JobID, ex.Message);
            }
            finally
            {
                l.Info("end {0}", JobID);
            }
        }
        

    } //end class
} //end namespace
===============================================================================
===============================================================================
Example of job: JobTest.cs
===============================================================================
{
  "Params": 
  {
    "LevelOne": 
	{
      "LevelTwo": "Text found in LevelTwo",
      "LevelTwoB": "Text found in LevelTwoB"
    }
  },
  "ParamInt": 5
}
===============================================================================
===============================================================================
