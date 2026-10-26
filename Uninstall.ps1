$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Paths.ps1')
$root=Split-Path $PSScriptRoot -Parent
$game=Get-SpeakerGamePaths $root
Assert-SpeakerGameClosed $game
$manifest=Join-Path $PSScriptRoot 'standalone-installed.json'
if(!(Test-Path -LiteralPath $manifest)){throw 'No standalone speaker install record. No files changed.'}
$state=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json
if($state.loadingMode -ne 'bepinex-plugin' -or $state.pluginPath -ne 'BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.Plugin.dll'){throw 'Unknown speaker install record. No files changed.'}
$owned=@('BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.Plugin.dll','BepInEx/patchers/OneWaySpeaker.Bootstrap.dll')
foreach($relative in $owned){
    $path=Join-Path $root $relative;$key=$relative.Replace('/','\')
    if((Test-Path -LiteralPath $path) -and (Get-FileHash -LiteralPath $path).Hash -ne $state.files.$key){throw ('Speaker file changed after installation. No files removed: '+$relative)}
}
$backup=Join-Path $PSScriptRoot ('backup/standalone-uninstall-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backup -Force|Out-Null
Copy-Item -LiteralPath $manifest -Destination (Join-Path $backup 'standalone-installed.json')
$present=@()
foreach($relative in $owned){
    $path=Join-Path $root $relative
    if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination (Join-Path $backup ([IO.Path]::GetFileName($path)));$present+=$relative}
}
try{
    foreach($relative in $present){Remove-Item -LiteralPath (Join-Path $root $relative) -Force}
    $state.enabled=$false
    $state|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $manifest -Encoding UTF8
}catch{
    foreach($relative in $present){Copy-Item -LiteralPath (Join-Path $backup ([IO.Path]::GetFileName($relative))) -Destination (Join-Path $root $relative) -Force}
    Copy-Item -LiteralPath (Join-Path $backup 'standalone-installed.json') -Destination $manifest -Force
    throw
}
Write-Output 'Standalone speaker disabled. Shared BepInEx, DPS, other plugins, assets and all personal data preserved.'
