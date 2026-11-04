"""Generate original, deterministic hospital Foley approximations (stdlib only)."""
from pathlib import Path
import math, random, wave, array, uuid
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Resources/HospitalTreatment/SFX'
RATE=44100
rng=random.Random(3701)
def make(name,duration,kind):
 n=int(RATE*duration); data=[]; low=0.; prev=0.
 for i in range(n):
  t=i/RATE; noise=rng.uniform(-1,1); low+=.13*(noise-low)
  if kind=='water':
   x=.55*low+.09*math.sin(2*math.pi*107*t+2*math.sin(2*math.pi*3*t))
   x*=.8+.2*math.sin(2*math.pi*4*t)
  elif kind in ('cloth','tape','waste'):
   env=sum(math.exp(-((t-c)/.045)**2) for c in ([.06,.18,.3] if kind=='cloth' else [.04,.09,.15,.22,.29,.36]))
   x=(noise-low)*env*.22+low*.18*env
   if kind=='waste': x+=.15*math.sin(2*math.pi*130*t)*math.exp(-12*t)
  else:
   freq={'metal':1100,'plastic':430,'click':850,'switch':260}[kind]
   x=(.6*math.sin(2*math.pi*freq*t)+.2*math.sin(2*math.pi*freq*2.71*t))*math.exp(-t*(22 if kind=='metal' else 45))+noise*.2*math.exp(-65*t)
  fade=min(1,t/.008,(duration-t)/.025)
  data.append(x*max(0,fade))
 peak=max(abs(x) for x in data); scale=.65/max(peak,1e-9)
 pcm=array.array('h',(round(x*scale*32767) for x in data))
 p=OUT/(name+'.wav')
 with wave.open(str(p),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(pcm.tobytes())
 if not Path(str(p)+'.meta').exists():
  Path(str(p)+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nAudioImporter:\n  serializedVersion: 7\n  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n    preloadAudioData: 1\n  platformSettingOverrides: {}\n  forceToMono: 1\n  normalize: 0\n  ambisonic: 0\n  loadInBackground: 0\n  preloadAudioData: 1\n  3D: 1\n',encoding='utf-8')
 print(name,len(data),f'peak={peak*scale:.2f}')
OUT.mkdir(parents=True,exist_ok=True)
for args in [('cloth',.42,'cloth'),('tape',.48,'tape'),('waste',.5,'waste'),('metal',.32,'metal'),('plastic',.19,'plastic'),('click',.12,'click'),('switch',.18,'switch')]:make(*args)

# Irrigation is recorded Foley; rebuild separately with build_recorded_irrigation.py.
