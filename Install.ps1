$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$managed=Join-Path $root 'One Way Tickets_Data/Managed'
if(Get-Process -Name 'One Way Tickets' -ErrorAction SilentlyContinue){throw 'Close the game before installing.'}
if(Test-Path (Join-Path $PSScriptRoot 'installed.json')){throw 'Already installed. Uninstall before reinstalling.'}
$compat=Get-Content (Join-Path $PSScriptRoot 'compatibility.json') -Raw|ConvertFrom-Json
foreach($f in $compat.files.PSObject.Properties){if((Get-FileHash -LiteralPath (Join-Path $managed $f.Name)).Hash -ne $f.Value){throw ('Game or DPS Mod version changed: '+$f.Name)}}
& (Join-Path $PSScriptRoot 'Build.ps1')
$backup=Join-Path $PSScriptRoot 'backup'
New-Item -ItemType Directory -Path $backup -Force|Out-Null
$state=[ordered]@{version='1.3.0';gameVersion='0.8.0.3';buildId='26197073';files=[ordered]@{}}
foreach($n in @('World.dll','Commons.dll','Camp.dll','OneWayDps.dll')){
 $path=Join-Path $managed $n
 Copy-Item -LiteralPath $path -Destination (Join-Path $backup $n) -Force
 $state.files[$n]=[ordered]@{before=(Get-FileHash -LiteralPath $path).Hash;after=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot ('build/'+$n))).Hash}
}
try {
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'build/OneWaySpeaker.dll') -Destination $managed -Force
 foreach($n in $state.files.Keys){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('build/'+$n)) -Destination (Join-Path $managed $n) -Force}
 $state|ConvertTo-Json -Depth 6|Set-Content -Encoding utf8 (Join-Path $PSScriptRoot 'installed.json')
}catch{
 foreach($n in $state.files.Keys){Copy-Item -LiteralPath (Join-Path $backup $n) -Destination (Join-Path $managed $n) -Force}
 throw
}
Write-Output 'Speaker Mod installed. Original DPS Mod assemblies are preserved in SpeakerMod/backup.'


