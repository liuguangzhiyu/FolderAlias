param(
    [Parameter(Mandatory=$true)][string]$Tcc,
    [Parameter(Mandatory=$true)][string]$SignTool,
    [Parameter(Mandatory=$true)][string]$KeyDirectory,
    [Parameter(Mandatory=$true)][string]$BuildDirectory,
    [Parameter(Mandatory=$true)][string]$OutputExe
)
$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskBuild = [IO.Path]::GetFullPath($BuildDirectory)
if (Test-Path -LiteralPath $taskBuild) { throw 'Choose a new empty build directory.' }
$payload = Join-Path $taskBuild 'payload'
New-Item -ItemType Directory -Path (Join-Path $payload 'Assets') -Force | Out-Null
$source = Join-Path $PSScriptRoot 'src'
$common = @('/nologo','/codepage:65001','/optimize+','/platform:x64','/r:System.dll','/r:System.Core.dll','/r:System.Xml.dll')
$app = Join-Path $payload 'FolderAlias.exe'
& $csc @common /target:winexe "/out:$app" "/win32manifest:$source\app.manifest" "/win32icon:$PSScriptRoot\assets\FolderAlias.ico" /r:System.Drawing.dll /r:System.Windows.Forms.dll "$source\*.cs"
if ($LASTEXITCODE) { throw 'Application compilation failed.' }
& $Tcc -shared -Wall -o "$payload\FolderAliasMenu.dll" "$PSScriptRoot\native\FolderAliasMenu.c" "$PSScriptRoot\native\ole32.def"
if ($LASTEXITCODE) { throw 'Native menu compilation failed.' }
$builder = Join-Path $taskBuild 'BuildIdentity.exe'
& $csc @common /target:exe "/out:$builder" "$PSScriptRoot\packaging\BuildIdentity.cs"
if ($LASTEXITCODE) { throw 'Package builder compilation failed.' }
& $builder "$PSScriptRoot\packaging\AppxManifest.xml" "$payload\FolderAlias.Identity.msix"
if ($LASTEXITCODE) { throw 'Package creation failed.' }
Copy-Item -LiteralPath (Join-Path $KeyDirectory 'Publisher.cer') -Destination $payload
Copy-Item -LiteralPath "$PSScriptRoot\README.md" -Destination $payload
Copy-Item -LiteralPath "$PSScriptRoot\LICENSE.txt" -Destination $payload
Copy-Item -LiteralPath "$PSScriptRoot\assets\Logo.png" -Destination "$payload\Assets\Logo.png"
$secret = Import-Clixml -LiteralPath (Join-Path $KeyDirectory 'password.dpapi.xml')
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    foreach ($artifact in @($app, "$payload\FolderAliasMenu.dll", "$payload\FolderAlias.Identity.msix")) {
        & $SignTool sign /fd SHA256 /f (Join-Path $KeyDirectory 'publisher.pfx') /p $password $artifact
        if ($LASTEXITCODE) { throw 'Payload signing failed.' }
    }
    Add-Type -AssemblyName System.IO.Compression
    $zipPath = Join-Path $taskBuild 'Payload.zip'
    $zipStream = [IO.File]::Create($zipPath)
    $zip = [IO.Compression.ZipArchive]::new($zipStream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $payload -Recurse -File) {
            $name = $file.FullName.Substring($payload.Length + 1).Replace('\','/')
            $entry = $zip.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
            $entryStream = $entry.Open(); $inputStream = $file.OpenRead()
            try { $inputStream.CopyTo($entryStream) } finally { $inputStream.Dispose(); $entryStream.Dispose() }
        }
    } finally { $zip.Dispose(); $zipStream.Dispose() }
    $output = [IO.Path]::GetFullPath($OutputExe)
    & $csc @common /target:winexe /main:FolderAlias.Setup.Program "/out:$output" "/win32manifest:$source\app.manifest" "/win32icon:$PSScriptRoot\assets\FolderAlias.ico" /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "/resource:$zipPath,Payload.zip" "/resource:$payload\Publisher.cer,Publisher.cer" "$PSScriptRoot\installer\*.cs" "$source\Native.cs" "$source\MenuIntegration.cs"
    if ($LASTEXITCODE) { throw 'Setup compilation failed.' }
    & $SignTool sign /fd SHA256 /f (Join-Path $KeyDirectory 'publisher.pfx') /p $password $output
    if ($LASTEXITCODE) { throw 'Setup signing failed.' }
    Write-Output ('Built offline installer: ' + $output)
    Get-FileHash -Algorithm SHA256 -LiteralPath $output
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $password = $null
}
