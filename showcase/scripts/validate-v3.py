"""Decode the complete deliverable and extract a chronological review sheet."""
from pathlib import Path
import json
import av
import numpy as np
from PIL import Image, ImageDraw

root=Path(__file__).resolve().parents[2]
video=root/'.showcase-work/MR-VD-Showcase-v3.mp4'
review=root/'.showcase-work/v3-review'
review.mkdir(exist_ok=True)
targets=[2,5,8,11,13,16,18,20,23,26,28,30,33,35,38,41,44,47,49,52,55,58,61,63,65,68,71,73,75,78,81,84,85,88]
frames=0
selected=[]
with av.open(str(video)) as source:
    v=source.streams.video[0]
    info={'width':v.width,'height':v.height,'fps':str(v.average_rate),'videoCodec':v.codec_context.name,
          'containerDurationSeconds':source.duration/av.time_base}
    for frame in source.decode(video=0):
        if frames%30==0 and frames//30 in targets:
            sec=frames//30
            im=frame.to_image()
            im.save(review/f'final-{sec:02d}s.jpg',quality=92)
            im.thumbnail((640,360))
            selected.append((sec,im))
        frames+=1
info['decodedVideoFrames']=frames
assert frames==2700 and info['width']==1920 and info['height']==1080 and info['fps']=='30',info
samples=0;peak=0.;square_sum=0.;nvalues=0
with av.open(str(video)) as source:
    stream=source.streams.audio[0]
    info.update(audioCodec=stream.codec_context.name,audioSampleRate=stream.rate,audioChannels=stream.channels)
    for frame in source.decode(audio=0):
        a=frame.to_ndarray().astype(np.float64)
        if frame.format.name.startswith('s16'):a/=32768
        peak=max(peak,float(np.max(np.abs(a))))
        square_sum+=float(np.sum(a*a));nvalues+=a.size;samples+=frame.samples
info.update(decodedAudioSamples=samples,audioPeak=peak,audioRms=(square_sum/nvalues)**.5)
assert samples>=48000*89.9 and 0<peak<1,info
for batch in range((len(selected)+11)//12):
    subset=selected[batch*12:(batch+1)*12]
    montage=Image.new('RGB',(1280,390*((len(subset)+1)//2)),'#e8e9e3')
    draw=ImageDraw.Draw(montage)
    for i,(sec,im) in enumerate(subset):
        x=i%2*640;y=i//2*390
        montage.paste(im,(x,y));draw.text((x+12,y+367),f'{sec:02d}s',fill='#203b35')
    montage.save(review/f'final-contact-{batch+1}.jpg',quality=92)
(review/'validation.json').write_text(json.dumps(info,indent=2),encoding='utf-8')
print(json.dumps(info))
