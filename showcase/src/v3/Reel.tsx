import React from 'react';
import {AbsoluteFill,Audio,Sequence,staticFile} from 'remotion';
import {Bookend} from './Bookends';
import {MixedReality} from './MixedReality';
import {Capture} from './Capture';
import {Buffers} from './Buffers';
import {AudioSpectrum} from './AudioSpectrum';
import {Beat} from './Beat';
import {Information} from './Information';

export const MRVDShowcaseV3:React.FC=()=> <AbsoluteFill>
 <Sequence from={0} durationInFrames={120} name="片头"><Bookend/></Sequence>
 <Sequence from={120} durationInFrames={450} name="01 MR 环境"><MixedReality/></Sequence>
 <Sequence from={570} durationInFrames={450} name="02 桌面与鼠标"><Capture/></Sequence>
 <Sequence from={1020} durationInFrames={420} name="03 线程与内存"><Buffers/></Sequence>
 <Sequence from={1440} durationInFrames={480} name="04 音频频谱"><AudioSpectrum/></Sequence>
 <Sequence from={1920} durationInFrames={300} name="05 节拍检测"><Beat/></Sequence>
 <Sequence from={2220} durationInFrames={360} name="06 信息采集"><Information/></Sequence>
 <Sequence from={2580} durationInFrames={120} name="收尾"><Bookend outro/></Sequence>
 <Audio src={staticFile('v3/score.m4a')} volume={.72}/>
</AbsoluteFill>;
