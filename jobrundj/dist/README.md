# jobrundj – guide for job developers

jobrundj runs C# scripts called **jobs**. You only write `.cs` files in the `jobs` folder:
jobrundj compiles and runs them, so you don't need to build anything.

## Requirements

- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) to run jobs
- [Visual Studio Code](https://code.visualstudio.com/) with the **C#** extension (`ms-dotnettools.csharp`) to edit and debug
- *Optional:* the .NET 10 **SDK**, to get IntelliSense (autocomplete) while editing jobs

## Folder structure

```
jobrundj/
├── .vscode/            VS Code configuration (debug) – do not modify
├── jobs/               ← YOUR JOBS GO HERE
│   ├── JobTest.cs      sample job
│   ├── JobTest.json    sample job configuration (optional)
│   ├── JobDelete.cs    sample job with command line arguments
│   └── jobs.csproj     used only by VS Code for IntelliSense – do not delete
├── externaldll/        extra .dll files referenced by your jobs
├── logs/               log files (created at the first run)
├── NLog.config         logging configuration
└── jobrundj.exe        the job runner (jobrundj.dll on Linux / macOS)
```

## Writing a job

1. Create `jobs/MyJob.cs`. The file name (without `.cs`) is the job name.
2. Write a class that extends `jobmodeldj.jobs.Job` and override `Execute`:

```c#
//<jobrundjReferencedAssemblies></jobrundjReferencedAssemblies>
using System;
using NLog;
using jobmodeldj.Model;

namespace jobmodeldj.jobs
{
    class MyJob : Job
    {
        public override int JobRuntimeVersion { get { return 4; } }

        public override void Execute(JobConfiguration conf)
        {
            l.Info("{0} is running!", this.JobID);
            // your code here
        }
    }
}
```

- `l` is an NLog logger.
- `conf` gives access to folders, command line arguments (`conf.JobArgs`) and to the configuration file.
- Optional configuration: create `jobs/MyJob.json` and read values with `conf.Json?["Key"]`
  or `conf.Json?["Section:SubKey"]`.
- Other assemblies: copy the .dll into `externaldll` and list it in the first line,
  comma separated: `//<jobrundjReferencedAssemblies>MyLib.dll,Other.dll</jobrundjReferencedAssemblies>`.
- The job is compiled with C# 10, without implicit usings: write every `using` you need.

## Debugging in VS Code

1. Open the **jobrundj** folder (the one containing `jobrundj.exe`) in VS Code: *File → Open Folder…*
2. Open your job file in `jobs`.
3. Set breakpoints (click to the left of the line numbers).
4. Press **F5** and choose **Debug current job file**.

> **Important:** always start debugging from the **Run and Debug** panel (`Ctrl+Shift+D`)
> with **Debug current job file** selected in the drop-down, or with **F5**.
> Do **not** use the ▷ / 🐞 button in the top-right corner of the editor and do not choose
> "C#" when VS Code asks for a debugger: that tries to start `jobs.csproj` and fails with
> *"jobs.csproj does not support debugging. No launchable target found"*.
> If F5 asks you to choose a debugger, you probably opened the `jobs` folder instead of the
> jobrundj folder.

Other configurations available in the *Run and Debug* panel:

| Configuration | Use |
|---|---|
| Debug current job file | debugs the job open in the editor |
| Debug job by name | asks for the job name |
| Attach to jobrundj | attaches to a jobrundj process started with `--debug` |

## Running from the command line

```
jobrundj.exe -j MyJob                 (Windows)
dotnet jobrundj.dll -j MyJob          (Linux / macOS)
```

| Option | Description |
|---|---|
| `-j`, `--job` | job to run (required) |
| `-d`, `--jobsdir` | folder containing the jobs (default: `jobs`) |
| `--debug` | compile with debug symbols and wait for a debugger to attach |

Any other argument is passed to the job in `conf.JobArgs`.
Exit code: `0` = success, `1` = error (details in the `logs` folder).
