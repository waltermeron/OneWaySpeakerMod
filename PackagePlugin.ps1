param([string]$OutputArchive)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=Split-Path $PSScriptRoot -Parent
if(!$OutputArchive){$OutputArchive=Join-Path $root 'OneWaySpeakerMod_v1.4.0_Standalone_BepInEx_x64.zip'}
$OutputArchive=[IO.Path]::GetFullPath($OutputArchive)
if(Test-Path -LiteralPath $OutputArchive){throw 'Release archive already exists; use a new output name.'}
$stage=Join-Path $PSScriptRoot ('build/package-standalone-'+[Guid]::NewGuid().ToString('N'))
function Stage-SpeakerFile([string]$Relative){
    $target=Join-Path $stage ('SpeakerMod/'+$Relative)
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $Relative) -Destination $target
}
try{
    New-Item -ItemType Directory -Path $stage -Force|Out-Null
    foreach($dir in @('assets','standalone','tests','payload','licenses','vendor/source')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $dir) -File -Recurse){Stage-SpeakerFile $file.FullName.Substring($PSScriptRoot.Length+1)}
    }
    foreach($relative in @('README.md','ART_PROMPT.txt','CONTROL_ART_PROMPT.txt','VALIDATION.md','PLUGIN_RELEASE_VALIDATION.md','THIRD_PARTY_NOTICES.md','Paths.ps1','BuildStandalone.ps1','Install.ps1','Update.ps1','Uninstall.ps1','安装.cmd','更新.cmd','卸载.cmd','PackagePlugin.ps1','src/Speaker.cs','build/standalone/OneWaySpeaker.Plugin.dll','build/standalone/CheckStandalone.exe','build/standalone/Mono.Cecil.dll','build/standalone/BepInEx.dll','build/standalone/0Harmony.dll')){Stage-SpeakerFile $relative}
    $instructions=@'
老菊の音箱 Mod v1.4.0 独立插件版

本包包含音箱、贴图、歌曲和 BepInEx 5.4.23.6 x64，不包含 DPS，也不要求安装 DPS。
已验证目标：游戏 v0.8.0.3 / Steam Build 26211032，Windows x64 Unity Mono。

1. 完全退出游戏。
2. 将整个压缩包解压到 One Way Tickets.exe 所在目录。
3. 双击 SpeakerMod\安装.cmd；更新时运行 SpeakerMod\更新.cmd。
4. 卸载音箱运行 SpeakerMod\卸载.cmd，其他插件和共享加载器保留。

与 DPS 同时使用时，共用一套 BepInEx，两个插件分别工作。
旧 v1.3.0 音箱兼容层会在检查、备份后停用，不会修改 DPS 文件。
旧静态 DLL 覆盖版请先用 Steam 验证游戏文件，恢复官方游戏文件，再安装。

歌曲位于 SpeakerMod\assets\365里路.mp3，退出游戏后可用真正的 MP3 同名替换。
解压更新会覆盖同名歌曲，请先备份自定义歌曲。
游戏接口兼容的小更新通常不必重装；无法保证所有未来更新兼容。
详细说明见 SpeakerMod\README.md，启动日志位于 BepInEx\LogOutput.log。
本包不包含游戏 DLL、用户存档、个人设置、统计历史或本机安装记录。
'@
    $instructions|Set-Content -LiteralPath (Join-Path $stage '音箱安装说明.txt') -Encoding UTF8
    $pluginHash=(Get-FileHash -LiteralPath (Join-Path $stage 'SpeakerMod/build/standalone/OneWaySpeaker.Plugin.dll')).Hash
    @{version='1.4.0';distribution='standalone-speaker';loadingMode='bepinex-plugin';loader='BepInEx 5.4.23.6 x64';testedGameVersion='0.8.0.3';testedSteamBuild='26211032';pluginSha256=$pluginHash;requiresDps=$false;includesDps=$false;includesSpeakerAssets=$true;includesMusic=$true;includesGameAssemblies=$false;includesPersonalData=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $stage 'SpeakerMod/package.json') -Encoding UTF8
    foreach($file in Get-ChildItem -LiteralPath $stage -File -Recurse){
        $relative=$file.FullName.Substring($stage.Length+1)
        if($relative -match '(^|[\\/])DPSMod([\\/]|$)' -or $file.Name -like 'OneWayDps*' -or $file.Name -in @('World.dll','Commons.dll','Camp.dll','history.json','history.json.bak','settings.json','installed.json','standalone-installed.json') -or $file.Extension -eq '.dat'){throw ('DPS, private or game file in standalone package: '+$relative)}
    }
    foreach($required in @('payload/winhttp.dll','payload/BepInEx/core/BepInEx.dll','payload/BepInEx/patchers/OneWaySpeaker.Bootstrap.dll','payload/BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.Plugin.dll','assets/speaker.png','assets/speaker-control.png','assets/365里路.mp3')){if(!(Test-Path -LiteralPath (Join-Path $stage ('SpeakerMod/'+$required)))){throw ('Missing package file: '+$required)}}
    if((Get-FileHash -LiteralPath (Join-Path $stage 'SpeakerMod/payload/BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.Plugin.dll')).Hash -ne $pluginHash){throw 'Plugin payload mismatch'}
    [IO.Compression.ZipFile]::CreateFromDirectory($stage,$OutputArchive,[IO.Compression.CompressionLevel]::Optimal,$false)
    $zip=[IO.Compression.ZipFile]::OpenRead($OutputArchive)
    try{
        foreach($entry in $zip.Entries){
            $source=Join-Path $stage $entry.FullName;$stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
            try{if([BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') -ne (Get-FileHash -LiteralPath $source).Hash){throw ('Archive hash mismatch: '+$entry.FullName)}}finally{$stream.Dispose();$sha.Dispose()}
        }
        if($zip.Entries.Count -ne @(Get-ChildItem -LiteralPath $stage -File -Recurse).Count){throw 'Archive entry count mismatch'}
    }finally{$zip.Dispose()}
    ((Get-FileHash -LiteralPath $OutputArchive).Hash+'  '+[IO.Path]::GetFileName($OutputArchive))|Set-Content -LiteralPath ($OutputArchive+'.sha256.txt') -Encoding UTF8
    Write-Output ('Packaged standalone speaker, no DPS, all entries hash-verified: '+$OutputArchive)
}finally{
    $bound=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'build')).TrimEnd('\')+'\';$absolute=[IO.Path]::GetFullPath($stage)
    if(!$absolute.StartsWith($bound,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notmatch '^package-standalone-[0-9a-f]{32}$'){throw 'Unsafe package cleanup path'}
    if(Test-Path -LiteralPath $absolute){
        $links=@(Get-Item -LiteralPath $absolute;Get-ChildItem -LiteralPath $absolute -Recurse -Force)|Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0}
        if($links){throw 'Unexpected link in package staging'}
        Remove-Item -LiteralPath $absolute -Recurse -Force
    }
}
