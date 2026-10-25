$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$managed=Join-Path $root 'One Way Tickets_Data/Managed'
if(Get-Process -Name 'One Way Tickets' -ErrorAction SilentlyContinue){throw 'Close the game before uninstalling.'}
$manifest=Join-Path $PSScriptRoot 'installed.json'
if(!(Test-Path $manifest)){throw 'No installed manifest.'}
$state=Get-Content $manifest -Raw|ConvertFrom-Json
foreach($f in $state.files.PSObject.Properties){
 if((Get-FileHash -LiteralPath (Join-Path $managed $f.Name)).Hash -ne $f.Value.after){throw ('File changed after installation; refusing overwrite: '+$f.Name)}
 if((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot ('backup/'+$f.Name))).Hash -ne $f.Value.before){throw ('Backup hash mismatch: '+$f.Name)}
}
foreach($f in $state.files.PSObject.Properties){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('backup/'+$f.Name)) -Destination (Join-Path $managed $f.Name) -Force}
Remove-Item -LiteralPath (Join-Path $managed 'OneWaySpeaker.dll') -Force
Move-Item -LiteralPath $manifest -Destination (Join-Path $PSScriptRoot 'last-install.json') -Force
Write-Output 'Speaker Mod removed. Previous DPS Mod restored. Saves unchanged.'
