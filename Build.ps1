$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$managed=Join-Path $root 'One Way Tickets_Data/Managed'
$csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$names=@('mscorlib','System','System.Core','netstandard','World','AutoCodes','Commons','Foundations','UnityEngine.CoreModule','UnityEngine.AudioModule','UnityEngine.ImageConversionModule','UnityEngine.UI','UnityEngine.UIModule','UnityEngine.UnityWebRequestModule','UnityEngine.UnityWebRequestAudioModule')
$refs=@($names|ForEach-Object{'/reference:'+(Join-Path $managed ($_+'.dll'))})
& $csc /nologo /noconfig /codepage:65001 /target:library /nostdlib+ /optimize+ ('/out:'+(Join-Path $PSScriptRoot 'build/OneWaySpeaker.dll')) $refs (Join-Path $PSScriptRoot 'src/Speaker.cs')
if($LASTEXITCODE -ne 0){throw 'Speaker compilation failed'}
& $csc /nologo ('/reference:'+(Join-Path $PSScriptRoot 'build/Mono.Cecil.dll')) ('/out:'+(Join-Path $PSScriptRoot 'build/Patch.exe')) (Join-Path $PSScriptRoot 'src/Patch.cs')
if($LASTEXITCODE -ne 0){throw 'Patcher compilation failed'}
$inputDir=if(Test-Path (Join-Path $PSScriptRoot 'installed.json')){Join-Path $PSScriptRoot 'backup'}else{$managed}
& (Join-Path $PSScriptRoot 'build/Patch.exe') $inputDir (Join-Path $PSScriptRoot 'build/OneWaySpeaker.dll') (Join-Path $PSScriptRoot 'build')
if($LASTEXITCODE -ne 0){throw 'Patch generation failed'}

