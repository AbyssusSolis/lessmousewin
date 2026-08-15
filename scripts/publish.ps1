$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "publish\win-x64"
dotnet publish "$root\LessMouseWin\LessMouseWin.csproj" -c Release -r win-x64 --self-contained true -o $out
Write-Host "Published to $out"
Write-Host "Run: $out\LessMouseWin.exe"
