function Get-SpeakerGamePaths([string]$Root){
    $absolute=[IO.Path]::GetFullPath($Root).TrimEnd('\')
    $data=@(Get-ChildItem -LiteralPath $absolute -Directory -Filter '*_Data'|Where-Object {
        (Test-Path -LiteralPath (Join-Path $_.FullName 'Managed/World.dll')) -and
        (Test-Path -LiteralPath (Join-Path $_.FullName 'Managed/Commons.dll')) -and
        (Test-Path -LiteralPath (Join-Path $_.FullName 'Managed/UnityEngine.CoreModule.dll'))
    })
    if($data.Count -ne 1){throw 'Extract into the game root; cannot uniquely find its Unity data directory.'}
    $name=$data[0].Name.Substring(0,$data[0].Name.Length-5)
    [pscustomobject]@{Root=$absolute;DataName=$data[0].Name;Managed=Join-Path $data[0].FullName 'Managed';ProcessName=$name;Executable=Join-Path $absolute ($name+'.exe')}
}
function Assert-SpeakerGameClosed($Game){
    foreach($process in @(Get-Process -Name $Game.ProcessName -ErrorAction SilentlyContinue)){
        if(!$process.Path -or [IO.Path]::GetFullPath($process.Path).Equals($Game.Executable,[StringComparison]::OrdinalIgnoreCase)){throw 'Save and close this game installation before changing plugins.'}
    }
}
