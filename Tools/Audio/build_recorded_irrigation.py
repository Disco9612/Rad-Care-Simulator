"""Build hospital irrigation audio from ezwa's public-domain syringe recording.
Requires numpy and imageio-ffmpeg. See Sources/irrigation-license.txt.
"""
from pathlib import Path
import subprocess, wave
import numpy as np
import imageio_ffmpeg
root=Path(__file__).resolve().parents[2]
source=root/'Tools/Audio/Sources/Emptying_syringe_in_water_slow.ogg'
raw=subprocess.check_output([imageio_ffmpeg.get_ffmpeg_exe(),'-v','error','-i',str(source),'-af','highpass=f=220,lowpass=f=6500','-ac','1','-ar','44100','-f','f32le','pipe:1'])
x=np.frombuffer(raw,dtype='<f4').copy()
# Remove near-silent recording margins, preserve natural liquid transients.
frame=441
rms=np.array([np.sqrt(np.mean(x[i:i+frame]**2)) for i in range(0,len(x),frame)])
active=np.where(rms>rms.max()*.08)[0]
x=x[max(0,int(active[0]*frame)-441):min(len(x),int((active[-1]+1)*frame)+441)]
x-=x.mean()
# Overlap recorded flow segments, avoiding the previous tonal oscillator and gaps.
n=min(3528,len(x)//5); out=x.copy()
fade=np.linspace(0,1,n,endpoint=False)
for gain in [ .93,1.02,.97,1,.94,1.01,.96,1,.98,1.01,.95,1.0 ]:
 y=x*gain
 out=np.concatenate([out[:-n],out[-n:]*(1-fade)+y[:n]*fade,y[n:]])
# Seamless wrap using the same crossfade; no repeated silent fade-out.
out[:n]=out[-n:]*(1-fade)+out[:n]*fade
out=out[:-n]
out*=.40/np.max(np.abs(out))
p=root/'Assets/Resources/HospitalTreatment/SFX/irrigation.wav'
with wave.open(str(p),'wb') as w:
 w.setnchannels(1);w.setsampwidth(2);w.setframerate(44100);w.writeframes((out*32767).astype('<i2').tobytes())
print('Duration',round(len(out)/44100,3),'peak',round(float(abs(out).max()),3),'RMS',round(float(np.sqrt(np.mean(out**2))),4),'seam',round(float(abs(out[0]-out[-1])),5))
