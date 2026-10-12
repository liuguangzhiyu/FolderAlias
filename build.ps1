param([switch]$Tests)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The built-in .NET Framework C# compiler was not found.' }
$src = Join-Path $PSScriptRoot 'src'
$sources = @(Get-ChildItem -LiteralPath $src -Filter '*.cs' | ForEach-Object FullName)
$exe = Join-Path $PSScriptRoot 'FolderAlias.exe'
& $compiler /nologo /codepage:65001 /optimize+ /platform:x64 /target:winexe "/out:$exe" "/win32manifest:$src\app.manifest" "/win32icon:$PSScriptRoot\assets\FolderAlias.ico" /reference:System.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll @sources
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Write-Output "Built: $exe"
if ($Tests) {
    $testExe = Join-Path $PSScriptRoot 'tests\FolderAlias.Tests.exe'
    $core = @('Native.cs','IniDocument.cs','AliasEngine.cs','MenuIntegration.cs') | ForEach-Object { Join-Path $src $_ }
    & $compiler /nologo /codepage:65001 /optimize+ /platform:x64 /target:exe "/out:$testExe" /reference:System.dll /reference:System.Core.dll /reference:System.Xml.dll @core "$PSScriptRoot\tests\CoreTests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Tests build failed.' }
    Write-Output "Built: $testExe"
}
