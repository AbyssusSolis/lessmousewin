$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
dotnet build "$root\LessMouseWin\LessMouseWin.csproj" -c Release
