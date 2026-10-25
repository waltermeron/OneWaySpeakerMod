$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$managed=Join-Path $root 'One Way Tickets_Data/Managed'
if(Get-Process -Name 'One Way Tickets' -ErrorAction SilentlyContinue){throw 'Close the game before updating.'}
$manifest=Join-Path $PSScriptRoot 'installed.json'
if(!(Test-Path $manifest)){& (Join-Path $PSScriptRoot 'Install.ps1');return}
$state=Get-Content $manifest -Raw|ConvertFrom-Json
foreach($file in $state.files.PSObject.Properties){
 if((Get-FileHash -LiteralPath (Join-Path $managed $file.Name)).Hash -ne $file.Value.after){throw ('Installed file changed: '+$file.Name)}
 if((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot ('backup/'+$file.Name))).Hash -ne $file.Value.before){throw ('Original backup mismatch: '+$file.Name)}
}
$compat=Get-Content (Join-Path $PSScriptRoot 'compatibility.json') -Raw|ConvertFrom-Json
foreach($name in @('AutoCodes.dll','Foundations.dll')){if((Get-FileHash -LiteralPath (Join-Path $managed $name)).Hash -ne $compat.files.$name){throw ('Game updated: '+$name)}}
& (Join-Path $PSScriptRoot 'Build.ps1')
$snapshot=Join-Path $PSScriptRoot ('backup/before-update-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $snapshot|Out-Null
$names=@('World.dll','Commons.dll','Camp.dll','OneWayDps.dll','OneWaySpeaker.dll')
foreach($name in $names){Copy-Item -LiteralPath (Join-Path $managed $name) -Destination (Join-Path $snapshot $name)}
Copy-Item -LiteralPath $manifest -Destination (Join-Path $snapshot 'installed.json')
try {
 foreach($name in $names){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('build/'+$name)) -Destination (Join-Path $managed $name) -Force}
 $state.version='1.3.0'
 foreach($file in $state.files.PSObject.Properties){$file.Value.after=(Get-FileHash -LiteralPath (Join-Path $managed $file.Name)).Hash}
 $state|ConvertTo-Json -Depth 6|Set-Content -Encoding utf8 $manifest
}catch{
 foreach($name in $names){Copy-Item -LiteralPath (Join-Path $snapshot $name) -Destination (Join-Path $managed $name) -Force}
 Copy-Item -LiteralPath (Join-Path $snapshot 'installed.json') -Destination $manifest -Force
 throw
}
Write-Output 'Speaker Mod updated to v1.3.0. Original pre-mod backup preserved. User saves/history/settings untouched.'


