"""Reproducible original score and 4096-point teaching data; no recorded music."""
from pathlib import Path
import json
import wave
import shutil
import subprocess
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
SR = 48000
seconds = 120
rng = np.random.default_rng(403)
audio = np.zeros((SR * seconds, 2), dtype=np.float32)

def add(start, signal, pan=0):
    at = int(start * SR)
    end = min(len(audio), at + len(signal))
    if end <= at:
        return
    gains = np.array([np.sqrt((1-pan)/2), np.sqrt((1+pan)/2)])
    audio[at:end] += signal[:end-at, None] * gains

# Slow harmonic bed with sparse mallet notes. All oscillators have deterministic phases.
chords = [[48,55,60,64],[45,52,57,60],[41,48,53,57],[43,50,55,59]]
for bar in range(60):
    chord = chords[(bar//2)%4]
    t = np.arange(int(2.7*SR))/SR
    env = np.minimum(1,t/.22)*np.exp(-t/1.15)*np.minimum(1,(2.7-t)/.45)
    for j,note in enumerate(chord):
        hz=440*2**((note-69)/12)
        add(bar*2, .026*env*(np.sin(2*np.pi*hz*t)+.16*np.sin(4*np.pi*hz*t)),(j-1.5)/2)
    for j in range(4):
        q=np.arange(int(.7*SR))/SR
        note=chord[(j+bar)%4]+24
        hz=440*2**((note-69)/12)
        mallet=.035*np.exp(-q*7)*(1-np.exp(-q*180))*np.sin(2*np.pi*hz*q)
        add(bar*2+j*.5,mallet,(-1 if j%2 else 1)*.35)

for beat in range(240):
    t=np.arange(int(.3*SR))/SR
    # 120 BPM kick anchors the beat explanation at 88 seconds.
    kick=.105*np.sin(2*np.pi*(48*t+55*.025*(1-np.exp(-t/.025))))*np.exp(-t*19)*(1-np.exp(-t*700))
    add(beat*.5,kick)
    if beat%2:
        noise=rng.normal(0,1,len(t)).astype(np.float32)
        add(beat*.5,.016*noise*np.exp(-t*55))
    hat_t=np.arange(int(.09*SR))/SR
    noise=rng.normal(0,1,len(hat_t)).astype(np.float32)
    add(beat*.5+.25,.007*np.diff(noise,prepend=0)*np.exp(-hat_t*80),.25)

t=np.arange(len(audio))/SR
fade=np.minimum(1,t/2)*np.minimum(1,(seconds-t)/3)
audio*=fade[:,None]
audio*=.65/max(float(np.max(np.abs(audio))),.0001)
dest=ROOT/'public/v4'
dest.mkdir(parents=True,exist_ok=True)
with wave.open(str(dest/'score.wav'),'wb') as out:
    out.setnchannels(2);out.setsampwidth(2);out.setframerate(SR)
    out.writeframes((audio*32767).astype('<i2').tobytes())

edges=np.geomspace(40,4000,33)
window=np.hanning(4096)
smooth=np.zeros((2,2049))
frames=[]
for f in range(660):
    start=int((66+f/30)*SR)
    samples=audio[start:start+4096]
    spectrum=np.abs(np.fft.rfft(samples*window[:,None],axis=0)).T/4096
    smooth=.5*smooth+.5*spectrum
    bands=[]
    for channel in range(2):
        row=[]
        for low,high in zip(edges[:-1],edges[1:]):
            first=max(1,int(np.floor(low*4096/SR)))
            last=max(first,int(np.ceil(high*4096/SR))-1)
            row.append(float(np.mean(smooth[channel,first:last+1])))
        bands.append(row)
    bands=np.array(bands)
    heights=np.log1p(bands*650)
    heights=np.clip(heights/(np.max(heights)+.05),.035,1)
    frames.append({'bars':np.round(heights,4).tolist(),
        'wave':np.round(samples[::32,0]/.65,4).tolist(),
        'rightWave':np.round(samples[::32,1]/.65,4).tolist(),
        'spectrum':np.round(np.log1p(smooth[0,1:342:3]*700),4).tolist()})
(ROOT/'src/v4/audio-analysis.json').write_text(json.dumps(frames,separators=(',',':')),encoding='utf-8')
ffmpeg = shutil.which('ffmpeg')
if not ffmpeg:
    candidate = Path.home()/'.codex/bin/ffmpeg.exe'
    ffmpeg = str(candidate) if candidate.exists() else None
if not ffmpeg:
    raise RuntimeError('FFmpeg is required to encode public/v4/score.m4a')
subprocess.run([ffmpeg, '-y', '-hide_banner', '-loglevel', 'error', '-i',
    str(dest/'score.wav'), '-c:a', 'aac', '-b:a', '192k', str(dest/'score.m4a')],check=True)
print(json.dumps({'samples':len(audio),'peak':float(np.max(np.abs(audio))),'frames':len(frames),'fft':4096,'sampleRate':SR}))
