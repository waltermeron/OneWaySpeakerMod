param([Parameter(Mandatory=$true)][string]$Archive,[switch]$WithDps,[int]$TimeoutSeconds=120)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$speakerRoot=Split-Path $PSScriptRoot -Parent;$root=Split-Path $speakerRoot -Parent
$id=[Guid]::NewGuid().ToString('N');$mode=if($WithDps){'with-dps'}else{'no-dps'}
$fixture=Join-Path $speakerRoot ('build/runtime-standalone-'+$id)
$log=Join-Path $speakerRoot ('build/unity-standalone-'+$mode+'-'+$id+'.log')
$links=@('MonoBleedingEdge','One Way Tickets_Data/StreamingAssets','One Way Tickets_Data/Plugins','One Way Tickets_Data/Resources')
$unityProcess=$null
try{
    New-Item -ItemType Directory -Path $fixture -Force|Out-Null
    $archivePath=(Get-Item -LiteralPath $Archive).FullName
    $zip=[IO.Compression.ZipFile]::OpenRead($archivePath)
    try{
        $bound=[IO.Path]::GetFullPath($fixture).TrimEnd('\')+'\'
        foreach($entry in $zip.Entries){
            $target=[IO.Path]::GetFullPath((Join-Path $fixture $entry.FullName))
            if(!$target.StartsWith($bound,[StringComparison]::OrdinalIgnoreCase)){throw ('Unsafe archive entry: '+$entry.FullName)}
        }
    }finally{$zip.Dispose()}
    [IO.Compression.ZipFile]::ExtractToDirectory($archivePath,$fixture)
    foreach($name in @('One Way Tickets.exe','UnityPlayer.dll')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $fixture}
    $data=Join-Path $fixture 'One Way Tickets_Data'
    New-Item -ItemType Directory -Path (Join-Path $data 'Managed') -Force|Out-Null
    Get-ChildItem -LiteralPath (Join-Path $root 'One Way Tickets_Data') -File|Copy-Item -Destination $data
    Get-ChildItem -LiteralPath (Join-Path $root 'One Way Tickets_Data/Managed') -File|Copy-Item -Destination (Join-Path $data 'Managed')
    foreach($relative in $links){New-Item -ItemType Junction -Path (Join-Path $fixture $relative) -Target (Join-Path $root $relative)|Out-Null}
    if($WithDps){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $root 'DPSMod/payload') -File -Recurse){
            $relative=$file.FullName.Substring((Join-Path $root 'DPSMod/payload').Length+1)
            $target=Join-Path $fixture $relative
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target
        }
    }
    $global:LASTEXITCODE=0
    & (Join-Path $fixture 'SpeakerMod/Install.ps1')
    if($LASTEXITCODE -ne 0){throw 'Standalone Unity fixture installation failed'}
    $unityProcess=Start-Process -FilePath (Join-Path $fixture 'One Way Tickets.exe') -WorkingDirectory $fixture -WindowStyle Hidden -ArgumentList @('--speaker-self-test','-batchmode','-logFile',('"'+$log+'"')) -PassThru
    Write-Output ('Unity isolated test '+$mode+' pid='+$unityProcess.Id+' log='+$log)
    $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while(!$unityProcess.WaitForExit(1000)){if([DateTime]::UtcNow -gt $deadline){throw 'Standalone Unity fixture timed out'}}
    $exitCode=$unityProcess.ExitCode
    $text=Get-Content -LiteralPath $log -Raw
    if($exitCode -ne 0 -or $text -notmatch '\[OneWaySpeaker TEST\] ALL PASSED'){throw ('Standalone Unity self-test failed ('+$exitCode+'). See '+$log)}
    if($WithDps){
        if($text -notmatch 'DPS v1\.2\.0 enabled' -or $text -notmatch 'Optional speaker=False'){throw ('DPS did not load independently without its legacy speaker layer. See '+$log)}
    }elseif($text -match 'OneWayDps Preloader|DPS v1\.2\.0 enabled'){throw 'DPS unexpectedly loaded in the standalone-only fixture'}
    Write-Output ('PASS Unity standalone '+$mode+': ALL PASSED; exit='+$exitCode+'; '+$log)
}finally{
    if($unityProcess){
        $unityProcess.Refresh()
        if(!$unityProcess.HasExited){
            $expected=[IO.Path]::GetFullPath((Join-Path $fixture 'One Way Tickets.exe'))
            if(!$unityProcess.Path -or ![IO.Path]::GetFullPath($unityProcess.Path).Equals($expected,[StringComparison]::OrdinalIgnoreCase)){throw 'Refusing to stop a process outside this exact fixture'}
            Stop-Process -Id $unityProcess.Id -Force
            $unityProcess.WaitForExit(10000)|Out-Null
            if(!$unityProcess.HasExited){throw 'Fixture process still running; retaining test directory'}
        }
    }
    $base=[IO.Path]::GetFullPath((Join-Path $speakerRoot 'build')).TrimEnd('\')+'\';$absolute=[IO.Path]::GetFullPath($fixture)
    if(!$absolute.StartsWith($base,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notmatch '^runtime-standalone-[0-9a-f]{32}$'){throw 'Unsafe Unity fixture cleanup path'}
    if(Test-Path -LiteralPath $absolute){
        foreach($relative in $links){
            $link=Join-Path $absolute $relative
            if(Test-Path -LiteralPath $link){
                $item=Get-Item -LiteralPath $link -Force
                if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0){throw ('Expected junction missing: '+$link)}
                # Framework Directory.Delete without recursion removes only the junction.
                # Windows PowerShell 5 Remove-Item can throw NullReferenceException here.
                [IO.Directory]::Delete($link)
            }
        }
        $remainingLinks=@(Get-Item -LiteralPath $absolute;Get-ChildItem -LiteralPath $absolute -Recurse -Force)|Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0}
        if($remainingLinks){throw 'Refusing recursive cleanup of an unexpected link'}
        Remove-Item -LiteralPath $absolute -Recurse -Force
    }
}
