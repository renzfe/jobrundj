
# jobrundj

is a tool to run C# scripts.  

It is written and runs on .NET 8.0 Framework on Windows, Linux and Mac OS.

Download link: <https://github.com/renzfe/jobrundj/releases>

- Scripts are called jobs. Every job is a .cs file saved in .\jobs folder.  
- The job must contain a class that extends jobmodeldj.jobs.Job.  
- The script code must be implemented overriding the Execute method.  
- Any other assembly dependency must be declare in the first line between jobrundjReferencedAssemblies tags
- Variable l is a NLog.Logger
- The argument conf contains configuration references
- It can be add a [JobName].json file to save custom configurations (connection strings, etc)
- The docs/JobExamples folder contains some job for test

```c#
//<jobrundjReferencedAssemblies></jobrundjReferencedAssemblies>  
using System;  
using NLog;  
using jobmodeldj.Model;  
  
namespace jobmodeldj.jobs  
{  

    class JobTest : Job  
    {  
        public override int JobRuntimeVersion { get { return 4; } }  
        public int Version { get { return 1; } }
  
        public override void Execute(JobConfiguration conf) 
        {
            try
            {
                l.Info("{0} is running!", this.JobID);
                l.Info("conf.AppDirectoryPath={0}", conf.AppDirectoryPath);
                l.Info("conf.ExecuterFileName={0}", conf.ExecuterFileName);
                l.Info("conf.ExternalDLLDirectoryPath={0}", conf.ExternalDLLDirectoryPath);
                l.Info("conf.JobArgs={0}", string.Join(',',conf.JobArgs));
                l.Info("conf.JobAssembyName={0}", conf.JobAssembyName);
                l.Info("conf.JobConfigurationJsonName={0}", conf.JobConfigurationJsonName);
                l.Info("conf.JobFileReference.JobFileInfo.FullName={0}", conf.JobFileReference.JobFileInfo.FullName);
                l.Info("conf.JobID={0}", conf.JobID);
                l.Info("conf.JobsDirectoryPath={0}", conf.JobsDirectoryPath);
                l.Info("conf.TempDirectoryPath={0}", conf.TempDirectoryPath);
                l.Info("ConnectionString={0}", conf.Json?["ConnectionString"]);
                l.Info("Params:LevelOne:LevelTwo={0}", conf.Json?["Params:LevelOne:LevelTwo"]);
                //
                //TODO write your code here..
                //
            }
            catch (Exception ex)
            {
                l.Error("error {0} - {1}", JobID, ex.Message);
                throw ex;
            }
            finally
            {
                l.Info("end {0}", JobID);
            }
        }
  
    }
}
```

# jobrundj.exe / jobrundj.dll

jobrundj is the Console Application to execute a job.

On Windows OS:

**`jobrundj.exe`** **`-j`** `JobName`  

On Linux or Mac OS:

**`dotnet jobrundj.dll`** **`-j`** `JobName`  

# Release notes

## v2.1.0 Release date: 20234-01-07

- Intrduce job Version to version specific job 

## v2.0.0 Release date: 2023-12-29

- Upgrade to dotnet 8.0 
- Runtime jobfile version v4


## v1.5.0 Release date: 2023-04-25

- Upgrade to dotnet 7.0 
- Runtime jobfile version v3


## v1.3.0 Release date: 2022-11-13

- Extend Options class to implement specific argument in your job


## v1.1.0 Release date: 2022-11-06

- This is the first stable release.


---
The license of the project is the [MIT](LICENSE).
