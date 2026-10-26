$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$candidates=@(Get-ChildItem -LiteralPath $root -Directory -Filter '*_Data'|Where-Object {Test-Path -LiteralPath (Join-Path $_.FullName 'Managed/World.dll')})
if($candidates.Count -ne 1){throw 'Cannot uniquely identify the Unity game data directory'}
$managed=Join-Path $candidates[0].FullName 'Managed'
$core=Join-Path $root 'BepInEx/core'
if(!(Test-Path -LiteralPath (Join-Path $core 'BepInEx.dll'))){$core=Join-Path $PSScriptRoot 'payload/BepInEx/core'}
foreach($name in @('BepInEx.dll','0Harmony.dll','Mono.Cecil.dll')){if(!(Test-Path -LiteralPath (Join-Path $core $name))){throw ('Missing standalone build dependency: '+$name)}}
$out=Join-Path $PSScriptRoot 'build/standalone'
New-Item -ItemType Directory -Path $out -Force|Out-Null
$csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$names=@('mscorlib','System','System.Core','netstandard','World','AutoCodes','Commons','Foundations','UnityEngine','UnityEngine.CoreModule','UnityEngine.AudioModule','UnityEngine.ImageConversionModule','UnityEngine.UI','UnityEngine.UIModule','UnityEngine.UnityWebRequestModule','UnityEngine.UnityWebRequestAudioModule')
$refs=@($names|ForEach-Object{'/reference:'+(Join-Path $managed ($_+'.dll'))})
$refs+=@('BepInEx.dll','0Harmony.dll'|ForEach-Object{'/reference:'+(Join-Path $core $_)})
& $csc /nologo /noconfig /codepage:65001 /target:library /nostdlib+ /optimize+ ('/out:'+(Join-Path $out 'OneWaySpeaker.Plugin.dll')) $refs (Join-Path $PSScriptRoot 'src/Speaker.cs') (Join-Path $PSScriptRoot 'standalone/Plugin.cs')
if($LASTEXITCODE -ne 0){throw 'Standalone speaker compilation failed'}
foreach($name in @('Mono.Cecil.dll','BepInEx.dll','0Harmony.dll')){Copy-Item -LiteralPath (Join-Path $core $name) -Destination $out -Force}
& $csc /nologo /codepage:65001 /target:library ('/reference:'+(Join-Path $out 'BepInEx.dll')) ('/reference:'+(Join-Path $out 'Mono.Cecil.dll')) ('/out:'+(Join-Path $out 'OneWaySpeaker.Bootstrap.dll')) (Join-Path $PSScriptRoot 'standalone/EncodingBootstrap.cs')
if($LASTEXITCODE -ne 0){throw 'Standalone encoding bootstrap compilation failed'}
& $csc /nologo /codepage:65001 ('/reference:'+(Join-Path $out 'Mono.Cecil.dll')) ('/out:'+(Join-Path $out 'CheckStandalone.exe')) (Join-Path $PSScriptRoot 'standalone/CheckStandalone.cs')
if($LASTEXITCODE -ne 0){throw 'Standalone compatibility checker compilation failed'}
& (Join-Path $out 'CheckStandalone.exe') $managed (Join-Path $out 'OneWaySpeaker.Plugin.dll')
if($LASTEXITCODE -ne 0){throw 'Current game API check failed'}
$pluginPayload=Join-Path $PSScriptRoot 'payload/BepInEx/plugins/OneWaySpeaker'
New-Item -ItemType Directory -Path $pluginPayload -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $out 'OneWaySpeaker.Plugin.dll') -Destination $pluginPayload -Force
$patcherPayload=Join-Path $PSScriptRoot 'payload/BepInEx/patchers'
New-Item -ItemType Directory -Path $patcherPayload -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $out 'OneWaySpeaker.Bootstrap.dll') -Destination $patcherPayload -Force
Write-Output 'Built standalone speaker v1.4.0. Game DLLs and DPS files were not modified.'
