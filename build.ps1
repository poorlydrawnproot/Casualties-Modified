$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $root 'src'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw 'Could not find the .NET Framework C# compiler (csc.exe).'
}

$running = Get-Process -Name 'CasualtiesModified' -ErrorAction SilentlyContinue
if ($running) {
    throw 'Close Casualties Modified before rebuilding.'
}

$lib = Join-Path $root 'Newtonsoft.Json.dll'
if (-not (Test-Path $lib)) {
    throw 'Missing Newtonsoft.Json.dll beside build.ps1.'
}

$sources = @(
    'AssemblyInfo.cs',
    'SaveCore.cs',
    'LiquidCore.cs',
    'LiquidRegistry.Generated.cs',
    'LimbCatalog.cs',
    'TuiCore.cs',
    'TuiApp.cs',
    'TuiFields.cs',
    'TuiItems.cs',
    'TuiLiquids.cs',
    'TuiMenus.cs',
    'TuiProgram.cs'
) | ForEach-Object { Join-Path $srcDir $_ }

$references = @(
    'System.dll',
    'System.Core.dll',
    'System.IO.Compression.dll',
    'System.Xml.dll',
    'System.Xml.Linq.dll',
    'System.Runtime.Serialization.dll',
    'System.Data.dll',
    'System.Numerics.dll'
) | ForEach-Object { '/r:' + $_ }

$cscArgs = @(
    '/nologo',
    '/target:exe',
    '/optimize+',
    '/platform:anycpu',
    ('/out:' + (Join-Path $root 'CasualtiesModified.exe')),
    ('/r:' + $lib)
) + $references + $sources

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

Write-Host ('Built ' + (Join-Path $root 'CasualtiesModified.exe'))
