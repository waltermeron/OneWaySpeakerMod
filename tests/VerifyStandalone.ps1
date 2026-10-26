param([Parameter(Mandatory=$true)][string]$Archive)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$speakerRoot=Split-Path $PSScriptRoot -Parent
$root=Split-Path $speakerRoot -Parent
$dataName='One Way Tickets_Data'
$managedSource=Join-Path $root ($dataName+'/Managed')
$archivePath=(Get-Item -LiteralPath $Archive).FullName
$fixture=Join-Path $speakerRoot ('build/verify-standalone-'+[Guid]::NewGuid().ToString('N'))
$pluginRelative='BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.Plugin.dll'
$bootstrapRelative='BepInEx/patchers/OneWaySpeaker.Bootstrap.dll'

function Assert([bool]$Condition,[string]$Message){if(!$Condition){throw $Message}}
function Hash([string]$Path){return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
function Tree-Hashes([string]$Path){
    $result=@{}
    foreach($file in Get-ChildItem -LiteralPath $Path -File -Recurse){$result[$file.FullName.Substring($Path.Length+1)]=Hash $file.FullName}
    return $result
}
function Assert-Tree([string]$Path,[hashtable]$Before,[string]$Label){
    $after=Tree-Hashes $Path
    Assert ($after.Count -eq $Before.Count) ($Label+': file count changed')
    foreach($key in $Before.Keys){Assert ($after.ContainsKey($key) -and $after[$key] -eq $Before[$key]) ($Label+': file changed: '+$key)}
}
function Invoke-Entry([string]$Path,[switch]$CheckOnly){
    $global:LASTEXITCODE=0
    if($CheckOnly){& $Path -CheckOnly}else{& $Path}
    if($LASTEXITCODE -ne 0){throw ('Entry failed: '+$Path)}
}
function Expect-Rejection([string]$Path,[string]$Label){
    $caseRoot=Split-Path (Split-Path $Path -Parent) -Parent
    $before=Tree-Hashes $caseRoot
    $rejected=$false
    try{Invoke-Entry $Path -CheckOnly|Out-Host}catch{$rejected=$true;Write-Output ($Label+' rejected: '+$_.Exception.Message)}
    Assert $rejected ($Label+' was accepted')
    Assert-Tree $caseRoot $before ($Label+' rejection')
}
function New-Fixture([string]$Name){
    $destination=Join-Path $fixture $Name
    New-Item -ItemType Directory -Path $destination -Force|Out-Null
    $zip=[IO.Compression.ZipFile]::OpenRead($archivePath)
    try{
        $bound=[IO.Path]::GetFullPath($destination).TrimEnd('\')+'\'
        foreach($entry in $zip.Entries){
            $target=[IO.Path]::GetFullPath((Join-Path $destination $entry.FullName))
            Assert ($target.StartsWith($bound,[StringComparison]::OrdinalIgnoreCase)) ('Unsafe archive entry: '+$entry.FullName)
            Assert ($entry.FullName -notmatch '(^|[/\\])DPSMod([/\\]|$)|OneWayDps[^/\\]*\.dll$') ('DPS included in independent speaker package: '+$entry.FullName)
            Assert ([IO.Path]::GetFileName($entry.FullName) -notin @('World.dll','Commons.dll','Camp.dll','installed.json','standalone-installed.json','history.json','settings.json')) ('Game/private file included: '+$entry.FullName)
        }
    }finally{$zip.Dispose()}
    [IO.Compression.ZipFile]::ExtractToDirectory($archivePath,$destination)
    Assert (!(Test-Path -LiteralPath (Join-Path $destination 'DPSMod'))) 'Independent package contains a DPSMod folder'
    Assert (!(Test-Path -LiteralPath (Join-Path $destination $pluginRelative))) 'Package enables its plugin before validated installation'
    $managed=Join-Path $destination ($dataName+'/Managed')
    New-Item -ItemType Directory -Path $managed,(Join-Path $destination 'SaveData'),(Join-Path $destination 'DPSMod/history'),(Join-Path $destination 'BepInEx/plugins') -Force|Out-Null
    Get-ChildItem -LiteralPath $managedSource -File -Filter '*.dll'|Copy-Item -Destination $managed
    $official=Tree-Hashes $managed
    $private=@{}
    foreach($relative in @('SaveData/save_00.dat','DPSMod/settings.json','DPSMod/history/history.json','BepInEx/plugins/other-plugin.keep')){
        $path=Join-Path $destination $relative
        [IO.File]::WriteAllText($path,'independent fixture data must survive '+$Name)
        $private[$relative]=Hash $path
    }
    foreach($relative in @('SpeakerMod/assets/speaker.png','SpeakerMod/assets/speaker-control.png','SpeakerMod/assets/365里路.mp3',('SpeakerMod/payload/'+$pluginRelative),('SpeakerMod/payload/'+$bootstrapRelative))){
        Assert (Test-Path -LiteralPath (Join-Path $destination $relative) -PathType Leaf) ('Missing independent-package file: '+$relative)
        Assert ((Get-Item -LiteralPath (Join-Path $destination $relative)).Length -gt 0) ('Empty independent-package file: '+$relative)
    }
    return [pscustomobject]@{Root=$destination;Managed=$managed;Official=$official;Private=$private;Dps=@{}}
}
function Assert-Invariants($Case){
    Assert-Tree $Case.Managed $Case.Official 'Official Managed assemblies'
    foreach($relative in $Case.Private.Keys){Assert ((Hash (Join-Path $Case.Root $relative)) -eq $Case.Private[$relative]) ('Personal/other-plugin fixture file changed: '+$relative)}
}
function Assert-Dps($Case){foreach($relative in $Case.Dps.Keys){Assert ((Hash (Join-Path $Case.Root $relative)) -eq $Case.Dps[$relative]) ('Speaker changed DPS: '+$relative)}}
function Assert-Installed($Case){
    $speaker=Get-Content -LiteralPath (Join-Path $Case.Root 'SpeakerMod/standalone-installed.json') -Raw|ConvertFrom-Json
    Assert ($speaker.version -eq '1.4.0') 'Standalone speaker version is not 1.4.0'
    if($speaker.pluginPath){Assert ($speaker.pluginPath.Replace('\','/') -eq $pluginRelative) 'Standalone manifest has an incorrect plugin path'}
    Assert ($speaker.bootstrapPath.Replace('\','/') -eq $bootstrapRelative) 'Standalone manifest has an incorrect bootstrap path'
    $expected=Hash (Join-Path $Case.Root ('SpeakerMod/payload/'+$pluginRelative))
    Assert ((Hash (Join-Path $Case.Root $pluginRelative)) -eq $expected) 'Installed standalone plugin differs from payload'
    Assert ((Hash (Join-Path $Case.Root $bootstrapRelative)) -eq (Hash (Join-Path $Case.Root ('SpeakerMod/payload/'+$bootstrapRelative)))) 'Installed bootstrap differs from payload'
    Assert (!(Test-Path -LiteralPath (Join-Path $Case.Root 'BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.dll'))) 'Legacy DPS-managed speaker runtime remains active'
    Assert (!(Test-Path -LiteralPath (Join-Path $Case.Root 'SpeakerMod/installed.json'))) 'Legacy speaker record remains and could reactivate DPS optional speaker'
    Assert-Invariants $Case
    Assert-Dps $Case
}
function Uninstall-Speaker($Case){
    $coreBefore=Tree-Hashes (Join-Path $Case.Root 'BepInEx/core')
    Invoke-Entry (Join-Path $Case.Root 'SpeakerMod/Uninstall.ps1')|Out-Host
    Assert (!(Test-Path -LiteralPath (Join-Path $Case.Root $pluginRelative))) 'Standalone uninstall left its plugin enabled'
    Assert (!(Test-Path -LiteralPath (Join-Path $Case.Root $bootstrapRelative))) 'Standalone uninstall left its encoding bootstrap enabled'
    Assert-Tree (Join-Path $Case.Root 'BepInEx/core') $coreBefore 'Shared BepInEx core'
    Assert-Invariants $Case
}

try{
    New-Item -ItemType Directory -Path $fixture -Force|Out-Null
    $clean=New-Fixture 'no-dps'
    $entry=Join-Path $clean.Root 'SpeakerMod/Install.ps1'
    $music=Join-Path $clean.Root 'SpeakerMod/assets/365里路.mp3';$musicBytes=[IO.File]::ReadAllBytes($music)
    Remove-Item -LiteralPath $music -Force
    try{Expect-Rejection $entry 'Missing speaker media'}finally{[IO.File]::WriteAllBytes($music,$musicBytes)}
    $world=Join-Path $clean.Managed 'World.dll'
    [IO.File]::WriteAllText($world,'invalid fixture assembly')
    try{Expect-Rejection $entry 'Invalid game assembly'}finally{Copy-Item -LiteralPath (Join-Path $managedSource 'World.dll') -Destination $world -Force}
    $before=Tree-Hashes $clean.Root
    Invoke-Entry $entry -CheckOnly|Out-Host
    Assert-Tree $clean.Root $before 'Fresh standalone check-only'
    Invoke-Entry $entry|Out-Host
    Assert-Installed $clean
    Assert (!(Test-Path -LiteralPath (Join-Path $clean.Root 'BepInEx/plugins/OneWayDps'))) 'Standalone install added DPS'
    Assert (!(Test-Path -LiteralPath (Join-Path $clean.Root 'BepInEx/patchers/OneWayDps.Preloader.dll'))) 'Standalone install added a DPS preloader'
    $before=Tree-Hashes $clean.Root
    Invoke-Entry $entry -CheckOnly|Out-Host
    Assert-Tree $clean.Root $before 'Installed standalone check-only'
    Invoke-Entry (Join-Path $clean.Root 'SpeakerMod/Update.ps1')|Out-Host
    Assert-Installed $clean
    $native=Join-Path $clean.Root 'winhttp.dll';$nativeBytes=[IO.File]::ReadAllBytes($native)
    [IO.File]::WriteAllText($native,'unknown shared loader')
    try{Expect-Rejection $entry 'Different shared loader'}finally{[IO.File]::WriteAllBytes($native,$nativeBytes)}
    Uninstall-Speaker $clean

    $coexist=New-Fixture 'with-dps'
    foreach($relative in @('winhttp.dll','doorstop_config.ini','.doorstop_version','BepInEx/patchers/OneWayDps.Preloader.dll','BepInEx/plugins/OneWayDps/OneWayDps.dll','BepInEx/plugins/OneWayDps/OneWayDps.Loader.dll','BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.dll','DPSMod/installed.json','SpeakerMod/installed.json')){
        $target=Join-Path $coexist.Root $relative
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
        Copy-Item -LiteralPath (Join-Path $root $relative) -Destination $target -Force
        if($relative -like '*OneWayDps*' -or $relative -eq 'DPSMod/installed.json'){$coexist.Dps[$relative]=Hash $target}
    }
    Copy-Item -LiteralPath (Join-Path $root 'BepInEx/core') -Destination (Join-Path $coexist.Root 'BepInEx/core') -Recurse
    foreach($name in @('Uninstall.ps1','UninstallLoader.ps1','GamePaths.ps1')){Copy-Item -LiteralPath (Join-Path $root ('DPSMod/'+$name)) -Destination (Join-Path $coexist.Root 'DPSMod')}
    $entry=Join-Path $coexist.Root 'SpeakerMod/Install.ps1'
    $before=Tree-Hashes $coexist.Root
    Invoke-Entry $entry -CheckOnly|Out-Host
    Assert-Tree $coexist.Root $before 'Coexistence migration check-only'
    Invoke-Entry $entry|Out-Host
    Assert-Installed $coexist
    Invoke-Entry (Join-Path $coexist.Root 'SpeakerMod/Update.ps1')|Out-Host
    Assert-Installed $coexist
    # DPS can be removed first without disabling the independent speaker plugin.
    $speakerHash=Hash (Join-Path $coexist.Root $pluginRelative)
    Invoke-Entry (Join-Path $coexist.Root 'DPSMod/Uninstall.ps1')|Out-Host
    Assert ((Hash (Join-Path $coexist.Root $pluginRelative)) -eq $speakerHash) 'DPS uninstall changed the independent speaker'
    Uninstall-Speaker $coexist
    Write-Output 'PASS standalone release: no DPS bundled/installed; fresh/check/update, safe media/assembly/shared-loader rejection, current DPS speaker migration, independent uninstall, official DLLs/private data/other plugins/shared core preserved.'
}finally{
    $base=[IO.Path]::GetFullPath((Join-Path $speakerRoot 'build')).TrimEnd('\')+'\';$absolute=[IO.Path]::GetFullPath($fixture)
    if(!$absolute.StartsWith($base,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notmatch '^verify-standalone-[0-9a-f]{32}$'){throw 'Unsafe standalone fixture cleanup path'}
    if(Test-Path -LiteralPath $absolute){
        $links=@(Get-Item -LiteralPath $absolute;Get-ChildItem -LiteralPath $absolute -Recurse -Force)|Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0}
        if($links){throw 'Refusing recursive standalone fixture cleanup because it contains links'}
        Remove-Item -LiteralPath $absolute -Recurse -Force
    }
}
