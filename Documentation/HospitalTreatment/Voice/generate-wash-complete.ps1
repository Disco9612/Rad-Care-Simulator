param([string]$voiceSourceRoot = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$outputDirectory = Join-Path $voiceSourceRoot '../../../Assets/Resources/HospitalTreatment'
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $speaker.SelectVoice('Microsoft Heami Desktop')
    $speaker.Rate = 0
    $format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(24000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
    $speaker.SetOutputToWaveFile([IO.Path]::GetFullPath((Join-Path $outputDirectory 'second_wash_complete.wav')), $format)
    $speaker.Speak('두 번째 세척이 완료되었습니다. 트리거를 놓고 주사기를 카트의 원래 자리에 놓으세요.')
    $speaker.SetOutputToNull()
} finally { $speaker.Dispose() }
