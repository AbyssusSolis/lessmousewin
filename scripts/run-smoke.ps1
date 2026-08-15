$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
dotnet run --project "$root\smoke\Smoke.csproj" -c Release
