# AGENTS.md – context for AI assistants writing jobrundj jobs

This folder is a **jobrundj** installation. jobrundj is a .NET 10 console app that compiles and runs
C# scripts called **jobs** at runtime with Roslyn. The user develops jobs; you help them.

- Work **only** in `jobs/` (job `.cs` and `.json` files) and, when needed, `externaldll/` (extra .dll files).
- Never modify the runtime files (`jobrundj.exe`, `jobrundj.dll`, `jobmodeldj.dll`, other .dll in the root,
  `.vscode/`, `jobs/jobs.csproj`). They are a compiled distribution, there is no source code here.
- There is nothing to build: `dotnet build` is not part of the workflow. jobrundj compiles the job when it runs it.
- Human-oriented documentation: `DEVELOPER_GUIDE.md` (setup, debugging, a full step-by-step example).

## Hard rules – a job that breaks them does not run

1. **One job per file** in `jobs/`. The **file name must equal the class name**: `jobs/JobExportCsv.cs`
   contains `class JobExportCsv`. Otherwise jobrundj reports `Job not found`.
2. The class **extends `jobmodeldj.jobs.Job`**, is not abstract and lives in `namespace jobmodeldj.jobs`.
   Only the first class deriving from `Job` in the file is used; helper classes that don't derive from `Job` are fine.
3. **`JobRuntimeVersion` must return `4`**, otherwise the job fails with a version error.
4. **C# 10 only.** Not available: raw string literals (`"""`), collection expressions (`[1, 2]`),
   primary constructors on classes, `required` members, list patterns, `file` types, generic math.
   Use C# 10 equivalents (verbatim strings `@"..."`, `new[] { 1, 2 }`, constructors).
5. **No implicit usings.** Write every `using` (including `System`, `System.IO`, `System.Linq`, ...).
6. **`Execute` is synchronous.** For async APIs call `SomethingAsync().GetAwaiter().GetResult()`
   or write a private `async Task RunAsync()` and call `RunAsync().GetAwaiter().GetResult()` from `Execute`.
7. Jobs run **unattended** (often scheduled): no `Console.ReadLine`, no UI, never call `Environment.Exit`.
8. The first line declares extra assemblies (see below). Keep it, even empty:
   `//<jobrundjReferencedAssemblies></jobrundjReferencedAssemblies>`

## Template

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
        public override int Version { get { return 1; } } // optional, shown in the log

        public override void Execute(JobConfiguration conf)
        {
            l.Info("{0} is running", JobID);
            // job code
        }
    }
}
```

## Assembly references

The job is compiled against the assemblies already loaded by jobrundj plus the ones listed in the first line.

**Available without declaring anything** (verified): `System`, `System.IO`, `System.Linq`,
`System.Collections.Generic`, `System.Text`, `System.Text.RegularExpressions`, `System.Globalization`,
`System.Threading.Tasks`, NLog, `jobmodeldj` (`Job`, `JobConfiguration`, `DataBaseConnectionUtility`),
`Microsoft.Extensions.Configuration` (`conf.Json`).

**Must be declared in the first line** (verified):

| You use | First line |
|---|---|
| `DataTable`, `DataSet`, `DbConnection` (`System.Data`) | `System.Data.Common.dll` |
| `HttpClient` | `System.Net.Http.dll` |
| `JsonSerializer` (`System.Text.Json`) | `System.Text.Json.dll` |
| `XElement`, `XDocument` (`System.Xml.Linq`) | `System.Private.Xml.Linq.dll` (not `System.Xml.Linq.dll`) |
| `MySqlConnection` directly (MySqlConnector) | copy `MySqlConnector.dll` from the root into `externaldll/`, then `MySqlConnector.dll,System.Data.Common.dll` |
| a third-party library | copy its .dll (and its dependencies) into `externaldll/`, then list them |

Syntax: `//<jobrundjReferencedAssemblies>System.Data.Common.dll,System.Net.Http.dll</jobrundjReferencedAssemblies>`

- Comma separated, **no spaces** (`A.dll, B.dll` fails with ` B.dll not found`).
- Each .dll is searched in `externaldll/` first, then in the .NET runtime folder. The jobrundj root folder is **not** searched.
- How to fix compile errors:
  - `CS1069: ... has been forwarded to assembly 'X'` → add `X.dll`.
  - `CS0012: The type '...' is defined in an assembly that is not referenced ... 'X'` → add `X.dll`.
  - `CS0246` on a namespace of a library → the library .dll is missing from `externaldll/` or from the first line.
  - `<name>.dll not found` → wrong name, a space in the list, or the file is not in `externaldll/`.

## What a job can use

**Members inherited from `Job`**

- `l` – NLog `Logger` (`l.Trace/Debug/Info/Warn/Error/Fatal("text {0}", value)`, `l.Error(ex, "text")`).
- `JobID` – the class name.
- `Progress(long current, long total, string? message = null)` – reports progress: writes an Info log line
  (throttled: at most once per percentage point or every 2 seconds, so it can be called on every item) and moves
  the progress bar of the `--ui` dashboard. Use it in loops over many items; `total` = `0` when unknown.
- `Prompt` (`IJobPrompt`, same as `conf.Prompt`) – input for the job. Every call has a key; the value comes from
  `--param key=value [key2=value2 ...]`, then from the `"JobParams"` section of `jobs/<JobName>.json`, then – only
  when jobrundj runs with `-i` / `--interactive` – from a question in the console, then from the default value;
  otherwise a `JobInputException` stops the job. Use it instead of `Console.ReadLine` (never read the console directly).
  - `string Ask(key, question, string? defaultValue = null)`, `T Ask<T>(key, question)` (required),
    `T Ask<T>(key, question, T defaultValue)` – int, decimal, DateTime, enums...
  - `string AskSecret(key, question)` – not echoed, masked in the log.
  - `bool Confirm(key, question, bool defaultValue = false)`.
  - `string Choose(key, title, choices, string? defaultValue = null)`,
    `IList<string> ChooseMany(key, title, choices, defaultValues = null)` (at least one item).
  - `bool IsInteractive` – true when the user can be asked.
  - Every value is logged (`Input env = TEST (command line)`). Provided values use the invariant format
    (`3.5`, `2026-01-31`, `true`/`false`); `ChooseMany` takes `a,b` or a json array. Example: `docs/JobExamples/JobInput.cs`.

**`JobConfiguration conf`**

| Member | Content |
|---|---|
| `conf.Json?["Key"]`, `conf.Json?["Section:SubKey"]` | values from `jobs/<JobName>.json` (string or null; `conf.Json` is null-safe with `?.`) |
| `conf.JobArgs` | the **whole** command line as `string[]`, including `-j <JobName>` and the other jobrundj options |
| `conf.JobID` | job name |
| `conf.AppDirectoryPath` | `DirectoryInfo` of the jobrundj folder |
| `conf.JobsDirectoryPath` | `DirectoryInfo` of the jobs folder |
| `conf.TempDirectoryPath` | `DirectoryInfo` of `temp/` (for temporary files) |
| `conf.ExternalDLLDirectoryPath` | `DirectoryInfo` of `externaldll/` |
| `conf.JobConfigurationJsonName` | `"<JobName>.json"` |

**`jobmodeldj.Utils.DataBaseConnectionUtility`** (`IDisposable`, use it in a `using` block). Only the
MySQL / MariaDB driver is bundled.

```c#
using (var db = DataBaseConnectionUtility.CreateAndOpenConnectionForMySql(connectionString))
{
    DataTable table = db.DoReaderAsDataTable("SELECT ...");   // needs System.Data.Common.dll
    int rows = db.ExecCommand("UPDATE ...");                   // INSERT / UPDATE / DELETE
    // db.BeginTransaction(); db.CommitTransaction(); db.RollbackTransaction();
}
```

- Prefer `DoReaderAsDataTable` (or `DoQuery`, which returns a `DataSet`). Avoid `DoReader`: it reads every column with `GetString`.
- It has **no query parameters**: never concatenate user input or external data into SQL.
  When values come from outside, use MySqlConnector directly (see table above) with `MySqlCommand.Parameters`.
- Other factories (`CreateAndOpenConnectionForSQL`, `...ForSqlite`, `...ForOracle`) need the provider .dll in `externaldll/`.

## Configuration file

Optional `jobs/<JobName>.json`, read with `conf.Json?["Key"]`:

```json
{
  "ConnectionString": "Server=localhost;Database=shop;User ID=reader;Password=secret",
  "Params": { "Days": "30" }
}
```

Values are strings: convert them (`int.Parse(conf.Json?["Params:Days"] ?? "30")`). Put connection strings,
paths and anything the user may want to change here instead of hard-coding them. Validate required values
at the start of `Execute` and throw a clear exception if missing.

## Command line arguments

Run: `jobrundj.exe -j MyJob -p C:\data -a 30`. Everything is in `conf.JobArgs`. To parse it with
CommandLineParser (already available) derive the options class from `jobrundj.Console.Options`, so the
jobrundj options are recognized:

```c#
using CommandLine;

internal class MyJobOptions : jobrundj.Console.Options
{
    [Option('p', "path", Required = true, HelpText = "Input folder")]
    public string Path { get; set; }
}

// in Execute:
Parser.Default.ParseArguments<MyJobOptions>(conf.JobArgs)
      .WithParsed(o => { /* use o.Path */ })
      .WithNotParsed(errors => throw new ArgumentException("Invalid arguments"));
```

Do not use the option letters `j`, `d` and `i` or the names `debug`, `ui`, `interactive`, `param` and `pause`:
jobrundj uses them. For simple values prefer `Prompt` (see above): it also works with `-i` and the json file.

## Errors, logging, exit code

- To make the job fail, throw an exception: jobrundj exits with code `1` (`0` = success).
- jobrundj logs only the exception **message**. Log the exception yourself before rethrowing, passing it as the
  first argument (the `NLog.config` layouts append type, message and stack trace with file and line of the job),
  and rethrow with `throw;` (not `throw ex;`):

```c#
try { /* ... */ }
catch (Exception ex)
{
    l.Error(ex, "{0} failed", JobID);
    throw;
}
```

- Logs go to the console and to `logs/` (configured in `NLog.config`).

## Running and debugging

- Run: `jobrundj.exe -j MyJob` (Windows) or `dotnet jobrundj.dll -j MyJob` (Linux / macOS), from the jobrundj folder.
- Live dashboard with progress bar and summary: add `--ui` (interactive terminals only; the job code does not change).
- Jobs that use `Prompt`: add `-i` to be asked the missing values (it disables `--ui`), or pass them with
  `--param key=value ...` / the `"JobParams"` json section for scheduled runs.
- Debug: in VS Code open the job file and press F5 with the configuration **Debug current job file**
  (or **Debug current job file (with arguments)**). Breakpoints in the job file work.
- Compile errors are printed in the log as `CSxxxx: message`: fix them using the rules above.

## Checklist before handing a job to the user

- [ ] File `jobs/<Name>.cs`, class `<Name>`, `namespace jobmodeldj.jobs`, extends `Job`
- [ ] `JobRuntimeVersion` returns `4`
- [ ] First line present; every assembly not in the "available" list is declared, comma separated without spaces
- [ ] Only C# 10 syntax, all `using` directives written
- [ ] Configurable values in `jobs/<Name>.json`, validated at startup; the json file is provided too
- [ ] No SQL built by concatenating external values
- [ ] Exceptions logged with `l.Error(ex, "{0} failed", JobID)` and rethrown with `throw;`
- [ ] Tell the user how to run it: `jobrundj.exe -j <Name>` (plus arguments, if any)
