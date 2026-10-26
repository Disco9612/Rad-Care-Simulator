import asyncio,json,pathlib,subprocess,wave,math,array
import edge_tts,imageio_ffmpeg
root=pathlib.Path('D:/Rad-Care-Simulator'); doc=root/'Documentation/HospitalTreatment/Voice'; path=doc/'manifest.json'
m=json.loads(path.read_text(encoding='utf-8-sig')); voices={'nurse':'ko-KR-InJoonNeural','patient':'ko-KR-HyunsuMultilingualNeural'}
async def main():
 for line in m['clips']:
  if line['role'] not in voices: continue
  voice=voices[line['role']]; tmp=root/'Temp'/f"{line['key']}-male.mp3"
  await edge_tts.Communicate(line['text'],voice).save(str(tmp))
  out=root/'Assets/Scripts/Hospital_Treatment/Audio'/f"{line['key']}.wav"
  subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-loglevel','error','-i',str(tmp),'-ar','24000','-ac','1',str(out)],check=True)
  with wave.open(str(out)) as w:
   samples=array.array('h',w.readframes(w.getnframes())); rms=math.sqrt(sum(x*x for x in samples)/len(samples)); duration=len(samples)/w.getframerate()
  assert duration>.2 and rms>20
  line.update(voice=voice,provider='Microsoft Edge online TTS',synthetic=True)
  print(line['key'],voice,round(duration,2),round(rms),flush=True)
 m['roleVoices']={'guide':'Microsoft Heami Desktop',**voices}
 path.write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf-8')
asyncio.run(main())
