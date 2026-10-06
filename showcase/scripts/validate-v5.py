from pathlib import Path
import json
import av
from PIL import Image, ImageDraw
import numpy as np

root=Path(__file__).resolve().parents[2]
video=root/'.showcase-work/MR-VD-Showcase-v5.mp4'
review=root/'.showcase-work/v5-review'; review.mkdir(exist_ok=True)
targets=[0,2,4,6,10,14,18,22,24,26,30,34,38,42,46,50,54,58,62,66,70,74,78,82,86,90,94,98,102,106,110,114,116,118,119]
frames=0; selected=[]; blank_frames=[]
with av.open(str(video)) as source:
    v=source.streams.video[0]
    info={'width':v.width,'height':v.height,'fps':str(v.average_rate),'videoCodec':v.codec_context.name,'containerDurationSeconds':source.duration/av.time_base}
    for frame in source.decode(video=0):
        if frames%30==0 and frames//30 in targets:
            sec=frames//30; im=frame.to_image(); im.save(review/f'final-{sec:03d}s.jpg',quality=92); im.thumbnail((640,360)); selected.append((sec,im))
        # All shots have opaque titles/diagrams, including exact chapter boundaries.
        tiny=np.asarray(frame.to_image().resize((240,135)))
        if int((tiny.min(axis=2)<140).sum())<80:
            blank_frames.append(frames)
        frames+=1
info['decodedVideoFrames']=frames
info['blankFrames']=blank_frames
assert not blank_frames, blank_frames
assert frames==3600 and info['width']==1920 and info['height']==1080 and info['fps']=='30', info
samples=0; peak=0.; ss=0.; n=0
with av.open(str(video)) as source:
    stream=source.streams.audio[0]; info.update(audioCodec=stream.codec_context.name,audioSampleRate=stream.rate,audioChannels=stream.channels)
    for frame in source.decode(audio=0):
        a=frame.to_ndarray().astype('float64')
        if frame.format.name.startswith('s16'): a/=32768
        peak=max(peak,float(abs(a).max())); ss+=float((a*a).sum()); n+=a.size; samples+=frame.samples
info.update(decodedAudioSamples=samples,audioPeak=peak,audioRms=(ss/n)**.5)
assert samples>=48000*119.9 and 0<peak<1,info
for batch in range((len(selected)+11)//12):
    sub=selected[batch*12:(batch+1)*12]; montage=Image.new('RGB',(1280,390*((len(sub)+1)//2)),'#e8e9e3'); draw=ImageDraw.Draw(montage)
    for i,(sec,im) in enumerate(sub):
        x=i%2*640;y=i//2*390; montage.paste(im,(x,y)); draw.text((x+12,y+367),f'{sec:03d}s',fill='#203b35')
    montage.save(review/f'final-contact-{batch+1}.jpg',quality=92)
(review/'validation-v5.json').write_text(json.dumps(info,indent=2),encoding='utf-8'); print(json.dumps(info))
