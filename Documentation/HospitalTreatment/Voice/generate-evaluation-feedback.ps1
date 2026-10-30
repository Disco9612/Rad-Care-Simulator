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
    $speaker.SetOutputToWaveFile([IO.Path]::GetFullPath((Join-Path $outputDirectory 'equipment_not_for_step.wav')), $format)
    $speaker.Speak('현재 단계에서는 이 장비를 사용하지 않습니다.')
    $speaker.SetOutputToNull()
} finally { $speaker.Dispose() }
