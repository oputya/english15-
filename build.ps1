$ErrorActionPreference = 'Stop'
$frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkRoot 'csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'C# compiler from .NET Framework was not found.' }
$sourcePaths = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$outputDirectory = Join-Path $PSScriptRoot 'dist'
$executablePath = Join-Path $outputDirectory 'DeskHost.exe'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
& $compilerPath /nologo /target:winexe "/out:$executablePath" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll $sourcePaths
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE). Close the running EXE before rebuilding." }
Write-Output "Built $executablePath"
