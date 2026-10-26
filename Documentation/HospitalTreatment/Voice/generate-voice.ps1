Add-Type -AssemblyName System.Speech
$voiceManifest=Get-Content Documentation/HospitalTreatment/Voice/manifest.json -Raw -Encoding UTF8 | ConvertFrom-Json
$speaker=New-Object System.Speech.Synthesis.SpeechSynthesizer
$speaker.SelectVoice('Microsoft Heami Desktop')
$format=New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(24000,[System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen,[System.Speech.AudioFormat.AudioChannel]::Mono)
try {
 foreach($line in $voiceManifest.clips){
  if($line.role -eq 'nurse' -or $line.role -eq 'patient'){continue}
  $speaker.Rate=if($line.role -eq 'patient'){-1}else{0}
  $out=[IO.Path]::GetFullPath(('Assets/Scripts/Hospital_Treatment/Audio/'+$line.key+'.wav'))
  $speaker.SetOutputToWaveFile($out,$format)
  $speaker.Speak($line.text)
  $speaker.SetOutputToNull()
 }
} finally {$speaker.Dispose()}
