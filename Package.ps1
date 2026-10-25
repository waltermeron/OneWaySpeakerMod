$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=Split-Path $PSScriptRoot -Parent
$zipPath=Join-Path $root 'OneWaySpeakerMod_v1.3.0_0.8.0.3.zip'
if(Test-Path $zipPath){throw 'Release archive already exists; choose a new version.'}
$files=@('README.md','ART_PROMPT.txt','VALIDATION.md','compatibility.json','Build.ps1','Install.ps1','Update.ps1','Uninstall.ps1','Package.ps1','安装.cmd','更新.cmd','卸载.cmd','assets/speaker.png','assets/speaker-preview.png','assets/speaker-original.png','assets/365里路.mp3','assets/speaker-control.png','assets/speaker-control-original.png','assets/speaker-control-preview.png','CONTROL_ART_PROMPT.txt','build/Mono.Cecil.dll','build/Mono.Cecil.LICENSE.txt','build/OneWaySpeaker.dll')
$files+=Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs'|ForEach-Object{'src/'+$_.Name}
$archive=[IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($relative in $files){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $PSScriptRoot $relative),('SpeakerMod/'+$relative),[IO.Compression.CompressionLevel]::Optimal)|Out-Null}}finally{$archive.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($zipPath)
try {
 if($archive.Entries.Count -ne $files.Count){throw 'Archive count mismatch'}
 foreach($entry in $archive.Entries){
  if($entry.FullName -match '/backup/|SaveData|/history/|test-state|installed.json|runtime-v1'){throw 'Private state in package'}
  $relative=$entry.FullName.Substring('SpeakerMod/'.Length)
  $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','');if($hash -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $relative)).Hash){throw ('Archive mismatch: '+$relative)}}finally{$stream.Dispose();$sha.Dispose()}
 }
}finally{$archive.Dispose()}
$hash=Get-FileHash -LiteralPath $zipPath
($hash.Hash+'  '+[IO.Path]::GetFileName($zipPath))|Set-Content -Encoding ascii ($zipPath+'.sha256.txt')
Write-Output ('Packaged and verified '+$files.Count+' files: '+$zipPath)



