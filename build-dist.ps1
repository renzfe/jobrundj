<#
.SYNOPSIS
  Builds the jobrundj distribution package for job developers.

.DESCRIPTION
  Publishes jobrundj (Release) into release\jobrundj-<version>\, adds the sample jobs
  and the job developer guide, then creates release\jobrundj-<version>.zip.

.EXAMPLE
  .\build-dist.ps1
  .\build-dist.ps1 -Runtime win-x64 -SelfContained
#>
param(
    # optional RID (win-x64, linux-x64, osx-arm64...). Empty = portable framework-dependent
    [string]$Runtime = "",
    # include the .NET runtime (requires -Runtime)
    [switch]$SelfContained,
    # delete an existing package folder with the same name (close it in VS Code first)
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "jobrundj\jobrundj.csproj"

[xml]$csproj = Get-Content $project
$version = ($csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
$name = "jobrundj-$version"
if ($Runtime) { $name += "-$Runtime" }

$releaseDir = Join-Path $root "release"
$outDir = Join-Path $releaseDir $name
$zipFile = Join-Path $releaseDir "$name.zip"

if (Test-Path $outDir) {
    if (-not $Clean) {
        throw "$outDir already exists (it may contain jobs you are working on). Rename/remove it or run with -Clean."
    }
    Remove-Item $outDir -Recurse -Force
}
if (Test-Path $zipFile) { Remove-Item $zipFile -Force }

$publishArgs = @("publish", $project, "-c", "Release", "-o", $outDir,
                 "-p:SatelliteResourceLanguages=en")
if ($Runtime) {
    $publishArgs += @("-r", $Runtime, "--self-contained", $SelfContained.IsPresent.ToString().ToLower())
}

Write-Host "Publishing $name ..."
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# sample jobs + guide
Copy-Item (Join-Path $root "docs\JobExamples\JobTest.*") (Join-Path $outDir "jobs") -Force
Copy-Item (Join-Path $root "jobrundj\dist\DEVELOPER_GUIDE.md") (Join-Path $outDir "DEVELOPER_GUIDE.md") -Force
# context files for AI assistants (AGENTS.md: shared standard, CLAUDE.md: imports it for Claude Code)
Copy-Item (Join-Path $root "jobrundj\dist\AGENTS.md") (Join-Path $outDir "AGENTS.md") -Force
Copy-Item (Join-Path $root "jobrundj\dist\CLAUDE.md") (Join-Path $outDir "CLAUDE.md") -Force
# scripts to create desktop shortcuts that run a job (Windows / macOS + Linux)
Copy-Item (Join-Path $root "jobrundj\dist\new-shortcut.ps1") (Join-Path $outDir "new-shortcut.ps1") -Force
Copy-Item (Join-Path $root "jobrundj\dist\new-shortcut.sh") (Join-Path $outDir "new-shortcut.sh") -Force
New-Item -ItemType Directory -Force (Join-Path $outDir "externaldll") | Out-Null

Compress-Archive -Path $outDir -DestinationPath $zipFile

Write-Host ""
Write-Host "Package folder: $outDir"
Write-Host "Package zip:    $zipFile"
