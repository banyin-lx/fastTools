$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

New-Item -ItemType Directory -Force ".dotnet", ".nuget\packages", ".appdata\NuGet" | Out-Null

$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:NUGET_PACKAGES = Join-Path $repoRoot ".nuget\packages"
$env:APPDATA = Join-Path $repoRoot ".appdata"

dotnet build "src\FastTools.App\FastTools.App.csproj" --configfile "NuGet.Config"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build "plugins\FastTools.Plugin.SystemCommands\FastTools.Plugin.SystemCommands.csproj" --configfile "NuGet.Config"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build "plugins\FastTools.Plugin.WebSearch\FastTools.Plugin.WebSearch.csproj" --configfile "NuGet.Config"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
