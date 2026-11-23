param([string]$OutputPath)

$ErrorActionPreference = 'Stop'
$publicDirectory = Split-Path -Parent $PSScriptRoot
$publicSources = Join-Path $publicDirectory 'src'
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $workspace = Split-Path -Parent $publicDirectory
    $OutputPath = Join-Path $workspace 'dist\public\MHWs Local Tools.exe'
}

$sources = @(Get-ChildItem -LiteralPath $publicSources -File -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$sources += Join-Path $publicSources 'Properties\Edition.cs'
$assets = Join-Path $publicDirectory 'assets'
& (Join-Path $PSScriptRoot 'Compile.ps1') -SourceFiles $sources -AssetsDirectory $assets -OutputPath $OutputPath
