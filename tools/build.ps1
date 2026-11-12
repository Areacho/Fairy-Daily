param([switch]$Test,[switch]$RenderCheck,[string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework 4.8 is required.' }
$vendorRoot = Join-Path $taskRoot 'vendor'
$package = Join-Path $vendorRoot 'qrcoder.1.8.1.nupkg'
$unpacked = Join-Path $vendorRoot 'qrcoder.1.8.1'
$qrDll = Join-Path $unpacked 'lib\net40\QRCoder.dll'
if (!(Test-Path -LiteralPath $qrDll)) {
    New-Item -ItemType Directory -Force -Path $vendorRoot | Out-Null
    $ProgressPreference = 'SilentlyContinue'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri 'https://api.nuget.org/v3-flatcontainer/qrcoder/1.8.1/qrcoder.1.8.1.nupkg' -OutFile $package -UseBasicParsing
    $actual = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    if ($actual -ne 'D3FE5C80C742B6A2EF8A31734C06600E2D674806A97F36F4AF97B0D2871DE309') { throw 'QRCoder checksum verification failed.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($package, $unpacked)
}
$dist = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $taskRoot 'dist' }
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$webviewVersion='1.0.4314.35'
$webviewPackage=Join-Path $vendorRoot ("microsoft.web.webview2.$webviewVersion.nupkg")
$webviewRoot=Join-Path $vendorRoot ("microsoft.web.webview2.$webviewVersion")
if(!(Test-Path -LiteralPath (Join-Path $webviewRoot 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'))) {
    if(!(Test-Path -LiteralPath $webviewPackage)) {
        [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$webviewVersion/microsoft.web.webview2.$webviewVersion.nupkg" -OutFile $webviewPackage -UseBasicParsing
    }
    if((Get-FileHash -LiteralPath $webviewPackage -Algorithm SHA256).Hash -ne '90778DEB1B77E36292828B4B5833C9255C27533F40E0DDD55891AE718944EDC4'){throw 'WebView2 checksum verification failed.'}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($webviewPackage,$webviewRoot)
}
$webviewReferences=@()
foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')) {
    $source=Join-Path $webviewRoot ("lib\net462\$name")
    $webviewReferences+=("/r:$source")
    Copy-Item -LiteralPath $source -Destination (Join-Path $dist $name) -Force
}
foreach($arch in @('x64','x86')) {
    $loaderDirectory=Join-Path $dist ("runtimes\win-$arch\native")
    New-Item -ItemType Directory -Path $loaderDirectory -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $webviewRoot "runtimes\win-$arch\native\WebView2Loader.dll") -Destination (Join-Path $loaderDirectory 'WebView2Loader.dll') -Force
}
$sources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$references = @('/r:System.dll', '/r:System.Core.dll', '/r:System.Security.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Web.Extensions.dll', '/r:System.Net.Http.dll', '/r:System.Xml.dll', '/r:System.Xml.Linq.dll')
$references+=$webviewReferences
$exe = Join-Path $dist 'MiyouDaily.exe'
$iconTool=Join-Path $vendorRoot 'CreateIcon.exe'
& $compiler /nologo /target:exe /r:System.Drawing.dll "/out:$iconTool" (Join-Path $PSScriptRoot 'CreateIcon.cs')
if ($LASTEXITCODE -ne 0) { throw 'Icon tool compilation failed.' }
$iconPath=Join-Path $taskRoot 'assets\fairy.ico'
& $iconTool $iconPath
if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }
$assets=@(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'assets') -Filter '*.png' | ForEach-Object { '/resource:{0},MiyouDaily.{1}' -f $_.FullName,$_.Name })
$assets+=@(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'assets') -Filter 'MiyouUI-*.ttf' | ForEach-Object { '/resource:{0},MiyouDaily.{1}' -f $_.FullName,$_.Name })
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /langversion:5 "/out:$exe" "/win32icon:$iconPath" "/win32manifest:$PSScriptRoot\MiyouDaily.manifest" $references "/r:$qrDll" "/resource:$qrDll,QRCoder.dll" "/resource:$iconPath,MiyouDaily.fairy.ico" $assets $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'tools\MiyouDaily.exe.config') -Destination "$exe.config" -Force
Write-Output "Built: $exe"
if ($Test -or $RenderCheck) {
    $testExe = Join-Path $dist 'MiyouDaily.Tests.exe'
    $testSources = $sources | Where-Object { [IO.Path]::GetFileName($_) -ne 'Program.cs' }
    $testFiles=@(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'tests') -Filter '*.cs' | ForEach-Object FullName)
    & $compiler /nologo /target:exe /codepage:65001 /langversion:5 "/out:$testExe" "/win32manifest:$PSScriptRoot\MiyouDaily.manifest" $references "/r:$qrDll" "/resource:$qrDll,QRCoder.dll" "/resource:$iconPath,MiyouDaily.fairy.ico" "/resource:$taskRoot\tests\heybox-native-v14.json,MiyouDaily.heybox-native-v14.json" $assets $testSources $testFiles
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    Copy-Item -LiteralPath (Join-Path $taskRoot 'tools\MiyouDaily.exe.config') -Destination "$testExe.config" -Force
    if ($RenderCheck) { & $testExe --render-check } else { & $testExe }
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
