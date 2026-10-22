param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../../Temp/AudioVisualResearch/ReferenceAudio')
)

$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot 'ReferenceAudioGenerator.cs'
if (-not ('ReferenceAudioGenerator' -as [type])) {
    Add-Type -Path $sourcePath
}
$targetPath = [IO.Path]::GetFullPath($OutputDirectory)
[ReferenceAudioGenerator]::Generate($targetPath)
Get-ChildItem -LiteralPath $targetPath -Filter '*.wav' | Select-Object Name, Length
