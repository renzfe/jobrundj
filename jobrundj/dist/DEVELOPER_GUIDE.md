# jobrundj – guide for job developers

jobrundj runs C# scripts called **jobs**. You only write `.cs` files in the `jobs` folder:
jobrundj compiles and runs them, so you don't need to build anything.

Project home, releases and release notes: <https://github.com/renzfe/jobrundj>

## Requirements

- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) to run jobs
- [Visual Studio Code](https://code.visualstudio.com/) with the **C#** extension (`ms-dotnettools.csharp`) to edit and debug
- *Optional:* the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), to get IntelliSense (autocomplete) while editing jobs

## Folder structure

```
jobrundj/
├── .vscode/            VS Code configuration (debug) – do not modify
├── jobs/               ← YOUR JOBS GO HERE
│   ├── JobTest.cs      sample job
│   ├── JobTest.json    sample job configuration (optional)
│   └── jobs.csproj     used only by VS Code for IntelliSense – do not delete
├── externaldll/        extra .dll files referenced by your jobs
├── logs/               log files (created at the first run)
├── DEVELOPER_GUIDE.md  this guide
├── AGENTS.md           context for AI assistants (see "Using an AI assistant")
├── CLAUDE.md           same, for Claude Code
├── NLog.config         logging configuration
├── new-shortcut.ps1    creates a desktop shortcut for a job (Windows)
├── new-shortcut.sh     same, for macOS and Linux
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

### Reporting progress

For long jobs call `Progress(current, total, "message")` (inherited from `Job`). When the job is started
with `--ui` it moves the progress bar of the live dashboard and updates the estimated remaining time;
otherwise it only writes a log line such as `Progress 680/1000 (68%) Exporting rows`.

```c#
for (int i = 0; i < rows.Count; i++)
{
    // ... process the row
    Progress(i + 1, rows.Count, "Exporting rows");
}
```

It can be called on every item: the log line is written at most once per percentage point or every
2 seconds. Use `total` = `0` when the total is not known (the dashboard shows only the counter).

### Asking for input

A job can ask for values (text, numbers, dates, confirmations, one or more items from a list) with
`Prompt` (inherited from `Job`, same as `conf.Prompt`). Every request has a **key**; the value is taken,
in this order, from:

1. the command line: `--param key=value [key2=value2 ...]`
2. the `"JobParams"` section of `jobs/<JobName>.json`
3. the user, with a question in the console, when jobrundj is started with **`-i`** (`--interactive`)
4. the default value of the request

When no value is found the job fails with a clear message (`Input 'from': no value for ...`).
This way the same job runs both by hand (`-i`) and from a scheduler (`--param` or the json file).

```c#
string env = Prompt.Choose("env", "Environment", new[] { "DEV", "TEST", "PROD" }, "DEV");
IList<string> tables = Prompt.ChooseMany("tables", "Tables to export", new[] { "Customers", "Orders", "Invoices" });
DateTime from = Prompt.Ask("from", "Export from date", DateTime.Today.AddDays(-30));
int maxRows = Prompt.Ask<int>("maxRows", "Maximum rows");          // required: no default value
string user = Prompt.Ask("user", "User name", "admin");
string password = Prompt.AskSecret("password", "Password");          // not echoed, never logged
if (env == "PROD" && !Prompt.Confirm("confirm", "Run on PROD?", false)) return;
```

```
jobrundj.exe -j JobInput -i
jobrundj.exe -j JobInput --param env=TEST tables=Orders,Invoices from=2026-01-01 maxRows=500
```

```json
{
  "JobParams": { "env": "TEST", "tables": ["Orders", "Invoices"], "maxRows": 500 }
}
```

- With `-i` only the missing values are asked; the log goes on as usual between the questions.
  `-i` disables `--ui` and is ignored when the input or the output is redirected.
- Every received value is written in the log (`Input env = TEST (command line)`); secrets are masked.
- Values from the command line and the json file use the invariant format: `3.5`, `2026-01-31`,
  `true`/`false` (also `yes`/`no`, `y`/`n`, `1`/`0`). `ChooseMany` takes a comma separated list or a json array.
- `Prompt.IsInteractive` tells whether the user can be asked.
- `docs/JobExamples/JobInput.cs` (`jobs/JobInput.cs` in the distribution) is a complete example.

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
| Debug current job file (with arguments) | same, but asks for the job arguments (e.g. `-p C:\temp -a 30`) |
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
| `--ui` | live dashboard (status, elapsed time, progress bar, warnings/errors, latest log messages) and a summary at the end; ignored when the output is redirected or the job is scheduled |
| `-i`, `--interactive` | ask in the console the job input that is not passed with `--param` or in the json file (see [Asking for input](#asking-for-input)); disables `--ui`; ignored when the input or the output is redirected |
| `--param key=value ...` | job input values read with `Prompt`; also repeatable: `--param a=1 --param b=2` |
| `--pause` | at the end wait for a key before closing the window (used by desktop shortcuts); ignored when the output is redirected or the job is scheduled |

Any other argument is passed to the job in `conf.JobArgs`.
Exit code: `0` = success, `1` = error (details in the `logs` folder).

## Desktop shortcuts

To let a user start a job with a double click, create a shortcut with the script included in the
jobrundj folder. The shortcut runs `jobrundj -j <Job> --ui --pause`: the job runs with the live dashboard and
at the end the window stays open on the summary until a key is pressed.

Run the script **on the computer where the shortcut is needed**: the shortcut contains the absolute paths
of that installation. If jobrundj is moved to another folder, create the shortcut again.

### Windows – `new-shortcut.ps1`

From a PowerShell window in the jobrundj folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\new-shortcut.ps1 -Job JobExportCsv -Name "Export customers"
```

| Parameter | Description |
|---|---|
| `-Job` | job name, without `.cs` (required) |
| `-Name` | name shown under the icon (default: the job name) |
| `-Icon` | `.ico` file for the icon (default: the icon of `jobrundj.exe`) |
| `-JobArgs` | arguments for the job, in quotes: `-JobArgs "-p C:\temp -a 30"` |
| `-Location` | `Desktop` (default), `StartMenu` (*Start → jobrundj*), `Both`, or a folder path |
| `-NoUi` | classic colored log instead of the dashboard |
| `-Interactive` | for jobs that ask for input: runs with `-i` (questions in the console, classic colored log) |

`-ExecutionPolicy Bypass` is needed only if Windows blocks scripts (*"running scripts is disabled on this
system"*); it applies only to this command. The shortcut can then be pinned to the taskbar or to Start
(right click → *Pin to taskbar*).

To create it by hand instead: right click on the desktop → *New → Shortcut*, location
`"C:\jobrundj\jobrundj.exe" -j JobExportCsv --ui --pause`, then *Properties → Change Icon…*.

### macOS and Linux – `new-shortcut.sh`

From a terminal in the jobrundj folder:

```bash
bash new-shortcut.sh -j JobExportCsv -n "Export customers"
```

| Option | Description |
|---|---|
| `-j`, `--job` | job name, without `.cs` (required) |
| `-n`, `--name` | name shown under the icon (default: the job name) |
| `-i`, `--icon` | icon image: `.png` on Linux; `.png` or `.icns` on macOS |
| `-a`, `--args` | arguments for the job, in quotes: `-a "-p /data -a 30"` |
| `-o`, `--output` | destination folder (see below) |
| `--no-ui` | classic colored log instead of the dashboard |
| `--interactive` | for jobs that ask for input: runs with `-i` (questions in the terminal, classic colored log) |

- **macOS**: creates `<Name>.command` on the Desktop. A double click opens Terminal and runs the job.
  If the icon can't be set automatically, select the file, *File → Get Info*, and paste the image onto the
  small icon at the top left.
- **Linux**: creates `jobrundj-<Job>.desktop` in the applications menu (`~/.local/share/applications`) and on
  the desktop. On GNOME, if the desktop icon shows a warning, right click → *Allow Launching*.

The script writes the absolute path of `dotnet` into the shortcut, because shortcuts started from the desktop
don't load the terminal `PATH`. If .NET is installed later or moved, create the shortcut again.
If `dotnet` is not found, install the .NET 10 Runtime or set `DOTNET_ROOT`.

Launch `new-shortcut.sh` with `bash` as shown: when the package is extracted from a zip file the script may
not be executable.

## Using an AI assistant

The package contains `AGENTS.md`, a context file that explains to AI coding assistants how jobrundj jobs
work: mandatory rules (file name = class name, `JobRuntimeVersion` 4, C# 10 only), which assemblies must be
declared in the first line, the available API, logging and error handling, and a checklist.

- **Claude Code** reads `CLAUDE.md` automatically, which imports `AGENTS.md`.
- **Codex, Cursor, GitHub Copilot** and other tools that support the `AGENTS.md` standard read it automatically.
- With a chat assistant (for example claude.ai), attach `AGENTS.md` to the conversation or paste its content.

Open the **jobrundj folder** (not the `jobs` folder) so the assistant finds the file, then describe what the
job must do, for example:

> Create a job JobPurgeLogs that deletes the files older than N days in a folder.
> Folder and days must be in the json configuration file.

Always review the generated code and test it with the debugger before scheduling it.

## Step-by-step example: export a query to a CSV file

This example builds a job that runs a `SELECT` on a **MySQL / MariaDB** database
and saves the result in a CSV file. The MySQL driver (MySqlConnector) is already included in jobrundj.

### Step 1 – Create the job file

Create the file `jobs/JobExportCsv.cs`.

> The file name and the class name **must be the same** (`JobExportCsv`):
> jobrundj looks for the job by file name and then checks the class name.

### Step 2 – Create the configuration file

Create `jobs/JobExportCsv.json` next to the job, with the connection string and the query:

```json
{
  "ConnectionString": "Server=localhost;Port=3306;Database=shop;User ID=reader;Password=secret",
  "Query": "SELECT id, name, price, created_at FROM products ORDER BY id",
  "OutputFolder": "",
  "Separator": ";"
}
```

| Key | Description |
|---|---|
| `ConnectionString` | how to connect to the database (server, port, database, user, password) |
| `Query` | the `SELECT` to run |
| `OutputFolder` | folder where the CSV is saved; empty = `output` folder next to `jobrundj.exe` |
| `Separator` | CSV separator; `;` is the one Excel expects with European regional settings |

Keeping these values in the json file means you can change the database or the query without touching the code.

> The json file contains a password: do not share it and do not commit it to a public repository.
> Use a database user that can only read.

### Step 3 – Write the code

Copy this code into `jobs/JobExportCsv.cs`:

```c#
//<jobrundjReferencedAssemblies>System.Data.Common.dll</jobrundjReferencedAssemblies>
using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using NLog;
using jobmodeldj.Model;
using jobmodeldj.Utils;

namespace jobmodeldj.jobs
{
    class JobExportCsv : Job
    {
        public override int JobRuntimeVersion { get { return 4; } }
        public override int Version { get { return 1; } }

        public override void Execute(JobConfiguration conf)
        {
            // 1. read the configuration from JobExportCsv.json
            string connectionString = conf.Json?["ConnectionString"];
            string query = conf.Json?["Query"];
            string outputFolder = conf.Json?["OutputFolder"];
            string separator = conf.Json?["Separator"] ?? ";";

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(query))
            {
                throw new Exception("ConnectionString and Query are required in " + conf.JobConfigurationJsonName);
            }
            if (string.IsNullOrEmpty(outputFolder))
            {
                outputFolder = Path.Combine(conf.AppDirectoryPath.FullName, "output");
            }
            Directory.CreateDirectory(outputFolder);

            // 2. run the query
            l.Info("Running query: {0}", query);
            DataTable table;
            using (var db = DataBaseConnectionUtility.CreateAndOpenConnectionForMySql(connectionString))
            {
                table = db.DoReaderAsDataTable(query);
            }
            l.Info("{0} rows read", table.Rows.Count);

            // 3. save the rows in a csv file
            string fileName = string.Format("{0}_{1:yyyyMMdd_HHmmss}.csv", conf.JobID, DateTime.Now);
            string filePath = Path.Combine(outputFolder, fileName);
            WriteCsv(table, filePath, separator);
            l.Info("File saved: {0}", filePath);
        }

        private void WriteCsv(DataTable table, string filePath, string separator)
        {
            int total = table.Rows.Count;
            int written = 0;

            // UTF-8 with BOM: Excel opens accented characters correctly
            using (var writer = new StreamWriter(filePath, false, new UTF8Encoding(true)))
            {
                // header
                var header = new string[table.Columns.Count];
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    header[c] = CsvValue(table.Columns[c].ColumnName, separator);
                }
                writer.WriteLine(string.Join(separator, header));

                // rows
                foreach (DataRow row in table.Rows)
                {
                    var values = new string[table.Columns.Count];
                    for (int c = 0; c < table.Columns.Count; c++)
                    {
                        values[c] = CsvValue(row[c], separator);
                    }
                    writer.WriteLine(string.Join(separator, values));

                    // progress bar of the --ui dashboard + "Progress 680/1000 (68%)" in the log
                    written++;
                    Progress(written, total, "Writing rows");
                }
            }
        }

        private static string CsvValue(object value, string separator)
        {
            if (value == null || value == DBNull.Value) return string.Empty;

            string text;
            if (value is DateTime date)
                text = date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            else if (value is IFormattable formattable)
                text = formattable.ToString(null, CultureInfo.InvariantCulture); // 1234.5, not 1234,5
            else
                text = value.ToString();

            // values containing the separator, quotes or new lines go between quotes
            if (text.Contains(separator) || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
            {
                text = "\"" + text.Replace("\"", "\"\"") + "\"";
            }
            return text;
        }
    }
}
```

What the code does:

- **First line** – `System.Data.Common.dll` contains `DataTable`. jobrundj only knows the assemblies
  it uses itself, so every other assembly must be listed between the `jobrundjReferencedAssemblies` tags.
  Without this line the job fails with *"CS1069: The type name 'DataTable' could not be found"*.
  The .dll is searched in `externaldll` and then in the .NET installation folder.
- **Step 1 in `Execute`** – reads the values from `JobExportCsv.json` with `conf.Json?["Key"]`
  and stops with a clear error if something is missing.
- **Step 2** – `DataBaseConnectionUtility.CreateAndOpenConnectionForMySql` opens the connection and
  `DoReaderAsDataTable` returns all the rows in a `DataTable`. The `using` block closes the connection
  even if the query fails.
- **Step 3** – writes a header row with the column names and one line per row. The file name contains
  date and time (`JobExportCsv_20261115_103000.csv`), so every run creates a new file.
- **`Progress(written, total, "Writing rows")`** – reports how many rows have been written. With `--ui`
  the dashboard shows a progress bar and the estimated remaining time (see Step 5); without `--ui` it only
  writes a log line such as `Progress 680/1000 (68%) Writing rows`. It is called on every row, but the log
  line is written at most once per percentage point or every 2 seconds, so the log stays readable.
  `WriteCsv` is not `static` because `Progress` belongs to the job.
- **`CsvValue`** – turns each value into text: empty for `NULL`, dates as `yyyy-MM-dd HH:mm:ss`,
  numbers with the dot as decimal separator (`1234.5`), and puts between quotes the values that contain
  the separator, quotes or new lines (`"Caffè; ""special"""`).

### Step 4 – Debug it

1. Set a breakpoint on the line `l.Info("{0} rows read", table.Rows.Count);`.
2. Press **F5** with **Debug current job file** selected.
3. When the debugger stops, look at `table` in the **Variables** panel: you can check the columns
   and the rows read from the database before the file is written.
4. Press **F5** again to continue. The terminal shows the path of the CSV file.

If the database cannot be reached, the log shows
*"Unable to connect to any of the specified MySQL hosts"*: check server, port and firewall.

### Step 5 – Run it

```
jobrundj.exe -j JobExportCsv
```

Result (`output/JobExportCsv_20261115_103000.csv`):

```
id;name;price;created_at
1;"Caffè; ""special""";1234.5;2026-11-15 10:30:00
2;Tea;2;
```

The exit code is `0` if the export worked and `1` if it failed. To run the job automatically,
schedule this command with Windows Task Scheduler or cron.

To follow the export in the live dashboard, add `--ui`:

```
jobrundj.exe -j JobExportCsv --ui
```

While the rows are written you see the progress bar, the number of rows (`680 / 1,000`), the message
*Writing rows* and the estimated remaining time; at the end a summary shows the result, the duration, the
warnings and errors and the log files. Running the query (step 2) is a single call, so during that phase
the dashboard shows only *Running* and the elapsed time: the bar appears when the rows are written.
To let a user start the export with a double click, create a shortcut (see [Desktop shortcuts](#desktop-shortcuts)):

```powershell
powershell -ExecutionPolicy Bypass -File .\new-shortcut.ps1 -Job JobExportCsv -Name "Export customers"
```

### Going further

- **Another database**: only the MySQL / MariaDB driver is included. For SQL Server, SQLite or Oracle,
  copy the provider .dll (with its dependencies) into `externaldll`, list it in the first line of the job
  and use `CreateAndOpenConnectionForSQL`, `CreateAndOpenConnectionForSqlite` or
  `CreateAndOpenConnectionForOracle` instead of `CreateAndOpenConnectionForMySql`.
- **Query from the command line**: read it from `conf.JobArgs` instead of the json file
  (see *Debug current job file (with arguments)*).
