<#
.SYNOPSIS
  Creates a Windows shortcut that runs a jobrundj job with the live dashboard.

.DESCRIPTION
  The shortcut starts "jobrundj.exe -j <Job> --ui --pause [JobArgs]": the job runs with the live
  dashboard and the window stays open at the end until a key is pressed, so the summary can be read.
  Keep this script in the jobrundj folder (next to jobrundj.exe).

.PARAMETER Job
  Job name (file name in the jobs folder, without .cs).

.PARAMETER Name
  Shortcut name shown under the icon (default: the job name).

.PARAMETER Icon
  Optional .ico file for the shortcut (default: the icon of jobrundj.exe).

.PARAMETER JobArgs
  Optional arguments for the job, e.g. "-p C:\data -a 30".

.PARAMETER Location
  Desktop (default), StartMenu, Both, or a folder path.

.PARAMETER NoUi
  Classic colored log instead of the dashboard (the window still waits for a key).

.PARAMETER Interactive
  For jobs that ask for input: runs with -i, so the missing values are asked in the console
  (it replaces the dashboard with the classic colored log).

.EXAMPLE
  .\new-shortcut.ps1 -Job JobExportCsv -Name "Export customers" -Icon C:\icons\export.ico

.EXAMPLE
  .
ew-shortcut.ps1 -Job JobInput -Name "Export (asks the options)" -Interactive

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File new-shortcut.ps1 -Job JobDelete -JobArgs "-p C:\temp -a 30" -Location Both
#>
param(
    [Parameter(Mandatory = $true)][string]$Job,
    [string]$Name,
    [string]$Icon,
    [string]$JobArgs = "",
    [string]$Location = "Desktop",
    [switch]$NoUi,
    [switch]$Interactive
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$exe = Join-Path $root "jobrundj.exe"
if (-not (Test-Path $exe)) {
    throw "jobrundj.exe not found in $root. Keep new-shortcut.ps1 in the jobrundj folder."
}

$Job = [IO.Path]::GetFileNameWithoutExtension($Job)   # accepts "JobX" or "JobX.cs"
if (-not (Test-Path (Join-Path $root "jobs\$Job.cs"))) {
    Write-Warning "jobs\$Job.cs not found: the shortcut is created anyway, check the job name."
}

if (-not $Name) { $Name = $Job }
# characters not allowed in file names
$fileName = ($Name -replace '[\\/:*?"<>|]', '_') + ".lnk"

$iconPath = $null
if ($Icon) {
    $iconPath = (Resolve-Path $Icon).Path
    if ([IO.Path]::GetExtension($iconPath) -ne ".ico") {
        Write-Warning "$iconPath is not an .ico file: Windows may not show it."
    }
}

$folders = switch ($Location) {
    "Desktop"   { @([Environment]::GetFolderPath("Desktop")) }   # also when the Desktop is on OneDrive
    "StartMenu" { @(Join-Path ([Environment]::GetFolderPath("Programs")) "jobrundj") }
    "Both"      { @([Environment]::GetFolderPath("Desktop"), (Join-Path ([Environment]::GetFolderPath("Programs")) "jobrundj")) }
    default     { @($Location) }
}

$arguments = "-j $Job"
if ($Interactive) { $arguments += " -i" }
elseif (-not $NoUi) { $arguments += " --ui" }
$arguments += " --pause"
if ($JobArgs) { $arguments += " $JobArgs" }

$shell = New-Object -ComObject WScript.Shell
foreach ($folder in $folders) {
    if (-not (Test-Path $folder)) { New-Item -ItemType Directory -Force $folder | Out-Null }
    $path = Join-Path $folder $fileName

    $shortcut = $shell.CreateShortcut($path)
    $shortcut.TargetPath = $exe
    $shortcut.Arguments = $arguments
    $shortcut.WorkingDirectory = $root
    $shortcut.Description = "jobrundj - $Job"
    if ($iconPath) { $shortcut.IconLocation = "$iconPath,0" }
    $shortcut.Save()

    Write-Host "Created $path"
}
Write-Host "Command: `"$exe`" $arguments"
