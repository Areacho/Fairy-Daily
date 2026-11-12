param([switch]$Test,[switch]$SkipBuild,[string]$ExecutablePath)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
if(!$SkipBuild){if($ExecutablePath){throw 'Use -SkipBuild with -ExecutablePath.'};if($Test){& (Join-Path $PSScriptRoot 'build.ps1') -Test}else{& (Join-Path $PSScriptRoot 'build.ps1')}}
$dist=Join-Path $taskRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force|Out-Null
if(!$ExecutablePath){$ExecutablePath=Join-Path $dist 'MiyouDaily.exe'}
$ExecutablePath=[IO.Path]::GetFullPath($ExecutablePath)
if([Reflection.AssemblyName]::GetAssemblyName($ExecutablePath).Name -ne 'MiyouDaily'){throw 'Unexpected application assembly.'}
$files=[ordered]@{'MiyouDaily.exe'=$ExecutablePath;'MiyouDaily.exe.config'=(Join-Path $PSScriptRoot 'MiyouDaily.exe.config')}
foreach($name in @('README.md','THIRD-PARTY-NOTICES.txt','FAIRY-NOTICE.txt','FAIRY-LICENSE.txt','HEYBOX-LICENSE.txt','WEBVIEW2-LICENSE.txt')){$files[$name]=Join-Path $taskRoot $name}
$files['NotoSansSC-OFL.txt']=Join-Path $taskRoot 'assets\NotoSansSC-OFL.txt'
$webview=Join-Path $taskRoot 'vendor\microsoft.web.webview2.1.0.4314.35'
foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){$files[$name]=Join-Path $webview "lib\net462\$name"}
foreach($arch in @('x64','x86')){$files["runtimes/win-$arch/native/WebView2Loader.dll"]=Join-Path $webview "runtimes\win-$arch\native\WebView2Loader.dll"}
$expected=@{}
foreach($name in $files.Keys){if(!(Test-Path -LiteralPath $files[$name] -PathType Leaf)){throw "Missing package input: $name"};$expected[$name]=(Get-FileHash -LiteralPath $files[$name] -Algorithm SHA256).Hash}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archivePath=Join-Path $dist 'MiyouDaily-Windows.zip'
$stream=[IO.File]::Open($archivePath,[IO.FileMode]::Create,[IO.FileAccess]::ReadWrite)
$archive=New-Object IO.Compression.ZipArchive -ArgumentList $stream,([IO.Compression.ZipArchiveMode]::Create),$false
try{foreach($name in $files.Keys){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$files[$name],$name,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}}finally{$archive.Dispose();$stream.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($archivePath)
try{
    if($archive.Entries.Count -ne $files.Count){throw 'Unexpected package entries.'}
    foreach($entry in $archive.Entries){$inputStream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create();try{$actual=[BitConverter]::ToString($sha.ComputeHash($inputStream)).Replace('-','')}finally{$sha.Dispose();$inputStream.Dispose()};if($actual -ne $expected[$entry.FullName]){throw "Package integrity check failed: $($entry.FullName)"}}
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
'{0}  MiyouDaily-Windows.zip' -f $hash|Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII
Write-Output ('Verified {0} package files: {1}' -f $files.Count,$archivePath)
Write-Output ('SHA256: '+$hash)
