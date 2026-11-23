param(
    [Parameter(Mandatory = $true)][string[]]$SourceFiles,
    [Parameter(Mandatory = $true)][string]$AssetsDirectory,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$MainClass
)

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'Could not find the Windows .NET Framework C# compiler.'
}

$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($output)) | Out-Null

$target = '/target:winexe'
$entryPoint = @()
if ($MainClass) {
    $target = '/target:exe'
    $entryPoint = @("/main:$MainClass")
}

$icon = Join-Path $AssetsDirectory 'images\app.ico'
$resources = [ordered]@{
    'data\skill-dict.json' = 'skills'
    'data\crafted-dict.json' = 'crafted'
    'data\recommendations.json' = 'recommendations'
    'data\skill-prices.json' = 'skillPrices'
    'data\decoration-dict.json' = 'decorations'
    'images\weapon-thumbnails.png' = 'weaponThumbnails'
}
$resourceOptions = foreach ($resource in $resources.GetEnumerator()) {
    $path = Join-Path $AssetsDirectory $resource.Key
    "/resource:$path,$($resource.Value)"
}

$compilerOptions = @(
    '/nologo'
    '/codepage:65001'
    '/optimize+'
    $target
    "/win32icon:$icon"
    "/out:$output"
    '/r:System.Web.Extensions.dll'
    '/r:System.IO.Compression.dll'
    '/r:System.Xml.Linq.dll'
)
& $compiler @compilerOptions @entryPoint @resourceOptions @SourceFiles
if ($LASTEXITCODE -ne 0) {
    throw "C# compilation failed with exit code $LASTEXITCODE."
}
Write-Output $output
