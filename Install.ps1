param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Paths.ps1')
$root=Split-Path $PSScriptRoot -Parent
$game=Get-SpeakerGamePaths $root
Assert-SpeakerGameClosed $game
$payload=Join-Path $PSScriptRoot 'payload'
$tools=Join-Path $PSScriptRoot 'build/standalone'
$pluginRelative='BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.Plugin.dll'
$bootstrapRelative='BepInEx/patchers/OneWaySpeaker.Bootstrap.dll'
$manifest=Join-Path $PSScriptRoot 'standalone-installed.json'
$state=if(Test-Path -LiteralPath $manifest){Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json}else{$null}
foreach($asset in @('speaker.png','speaker-control.png','365里路.mp3','365里路_王老菊版.mp3','365里路_王老菊特别版.mp3')){
    $path=Join-Path $PSScriptRoot ('assets/'+$asset)
    if(!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0){throw ('Speaker asset missing or empty: '+$asset)}
}
$plugin=Join-Path $payload $pluginRelative
if(!(Test-Path -LiteralPath $plugin)){throw 'Speaker plugin payload missing. Extract the complete standalone archive.'}
if(!(Test-Path -LiteralPath (Join-Path $payload $bootstrapRelative))){throw 'Speaker path bootstrap missing. Extract the complete standalone archive.'}
$nativeHashes=@{}
foreach($name in @('World.dll','Commons.dll','Camp.dll')){$nativeHashes[$name]=(Get-FileHash -LiteralPath (Join-Path $game.Managed $name)).Hash}
& (Join-Path $tools 'CheckStandalone.exe') $game.Managed $plugin
if($LASTEXITCODE -ne 0){throw 'Standalone speaker compatibility check failed; no files changed. For old static mods, verify game files with Steam first.'}
$changes=@()
function Add-SpeakerChange([string]$From,[string]$To,[bool]$Delete=$false){$script:changes+=@{From=$From;To=$To;Delete=$Delete}}
foreach($file in Get-ChildItem -LiteralPath $payload -File -Recurse){
    $relative=$file.FullName.Substring($payload.Length+1)
    $target=Join-Path $root $relative
    if(Test-Path -LiteralPath $target){
        $hash=(Get-FileHash -LiteralPath $target).Hash
        $same=$hash -eq (Get-FileHash -LiteralPath $file.FullName).Hash
        $owned=($relative.Replace('\','/') -in @($pluginRelative,$bootstrapRelative)) -and $state -and $state.files.$relative -eq $hash
        if(!$same -and !$owned){throw ('Different shared loader or unowned plugin file already exists: '+$relative+'. No files changed.')}
    }
    Add-SpeakerChange $file.FullName $target
}
# Earlier speaker releases used the DPS optional compatibility layer. Remove only
# its proven speaker runtime and speaker record; do not modify any DPS-owned file.
$legacyPath=Join-Path $PSScriptRoot 'installed.json'
$legacy=if(Test-Path -LiteralPath $legacyPath){Get-Content -LiteralPath $legacyPath -Raw|ConvertFrom-Json}else{$null}
$legacyDll=Join-Path $root 'BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.dll'
if(Test-Path -LiteralPath $legacyDll){
    $legacyHash=(Get-FileHash -LiteralPath $legacyDll).Hash
    $known=$false
    $legacyBuild=Join-Path $PSScriptRoot 'build/OneWaySpeaker.dll'
    if((Test-Path -LiteralPath $legacyBuild) -and (Get-FileHash -LiteralPath $legacyBuild).Hash -eq $legacyHash){$known=$true}
    $dpsRecord=Join-Path $root 'DPSMod/installed.json'
    if(Test-Path -LiteralPath $dpsRecord){$dpsState=Get-Content -LiteralPath $dpsRecord -Raw|ConvertFrom-Json;if($dpsState.files.'BepInEx\plugins\OneWaySpeaker\OneWaySpeaker.dll' -eq $legacyHash){$known=$true}}
    if(!$known){throw 'Unrecognized legacy OneWaySpeaker.dll. No files changed; preserve it and remove the old speaker layer before installing.'}
    Add-SpeakerChange '' $legacyDll $true
}
if($legacy){
    if($legacy.version -ne '1.3.0' -or $legacy.loadingMode -notin @('bepinex-memory','disabled')){throw 'Old static speaker record detected. Verify game files with Steam and preserve/remove the old speaker record before installing.'}
    Add-SpeakerChange '' $legacyPath $true
}
if($CheckOnly){Write-Output 'PASS standalone speaker compatibility and install check; no DPS dependency, no files changed.';return}
Assert-SpeakerGameClosed $game
foreach($name in $nativeHashes.Keys){if((Get-FileHash -LiteralPath (Join-Path $game.Managed $name)).Hash -ne $nativeHashes[$name]){throw 'Game updated during validation. Retry after Steam finishes.'}}
$work=Join-Path $PSScriptRoot ('build/install-standalone-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force|Out-Null
try{
    $next=[ordered]@{version='1.5.1';loadingMode='bepinex-plugin';pluginPath=$pluginRelative;bootstrapPath=$bootstrapRelative;enabled=$true;installedAt=[DateTimeOffset]::Now.ToString('o');files=[ordered]@{};checkedGameAssemblies=$nativeHashes}
    foreach($change in $changes){if(!$change.Delete){$relative=$change.To.Substring($root.Length+1);$next.files[$relative]=(Get-FileHash -LiteralPath $change.From).Hash}}
    $next|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $work 'standalone-installed.json') -Encoding UTF8
    Add-SpeakerChange (Join-Path $work 'standalone-installed.json') $manifest
    $snapshot=Join-Path $PSScriptRoot ('backup/standalone-install-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $snapshot -Force|Out-Null
    $index=0
    foreach($change in $changes){$change.Existed=Test-Path -LiteralPath $change.To;$change.Backup=Join-Path $snapshot ([string]$index++);if($change.Existed){Copy-Item -LiteralPath $change.To -Destination $change.Backup}}
    $changes|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $snapshot 'rollback-map.json') -Encoding UTF8
    try{
        foreach($change in $changes){
            if($change.Delete){Remove-Item -LiteralPath $change.To -Force;continue}
            New-Item -ItemType Directory -Path (Split-Path $change.To -Parent) -Force|Out-Null
            Copy-Item -LiteralPath $change.From -Destination $change.To -Force
            if((Get-FileHash -LiteralPath $change.From).Hash -ne (Get-FileHash -LiteralPath $change.To).Hash){throw 'Installed file hash mismatch'}
        }
    }catch{
        foreach($change in $changes){if($change.Existed){Copy-Item -LiteralPath $change.Backup -Destination $change.To -Force}elseif(Test-Path -LiteralPath $change.To){Remove-Item -LiteralPath $change.To -Force}}
        throw
    }
    Write-Output 'Installed standalone speaker v1.5.1. No DPS installed or modified; official game DLLs and personal data untouched.'
    Write-Output ('Rollback snapshot: '+$snapshot)
}finally{
    $bound=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'build')).TrimEnd('\')+'\';$absolute=[IO.Path]::GetFullPath($work)
    if(!$absolute.StartsWith($bound,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notmatch '^install-standalone-[0-9a-f]{32}$'){throw 'Unsafe install work path'}
    if(Test-Path -LiteralPath $absolute){
        $links=@(Get-Item -LiteralPath $absolute;Get-ChildItem -LiteralPath $absolute -Recurse -Force)|Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0}
        if($links){throw 'Unexpected link in install work directory'}
        Remove-Item -LiteralPath $absolute -Recurse -Force
    }
}
