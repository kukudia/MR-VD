import React from 'react';
import {AbsoluteFill,Audio,Sequence,staticFile} from 'remotion';
import {Bookend} from './Bookends';
import {MixedReality} from './MixedReality';
import {Capture} from './Capture';
import {Buffers} from './Buffers';
import {AudioSpectrum} from './AudioSpectrum';
import {Beat} from './Beat';
import {Information} from './Information';

export const MRVDShowcaseV4:React.FC=()=> <AbsoluteFill>
 <Sequence from={0} durationInFrames={120} name="片头"><Bookend/></Sequence>
 <Sequence from={120} durationInFrames={600} name="01 MR 环境"><MixedReality/></Sequence>
 <Sequence from={720} durationInFrames={660} name="02 桌面与鼠标"><Capture/></Sequence>
 <Sequence from={1380} durationInFrames={600} name="03 线程与内存"><Buffers/></Sequence>
 <Sequence from={1980} durationInFrames={660} name="04 音频频谱"><AudioSpectrum/></Sequence>
 <Sequence from={2640} durationInFrames={420} name="05 节拍检测"><Beat/></Sequence>
 <Sequence from={3060} durationInFrames={420} name="06 信息采集"><Information/></Sequence>
 <Sequence from={3480} durationInFrames={120} name="收尾"><Bookend outro/></Sequence>
 <Audio src={staticFile('v4/score.m4a')} volume={.72}/>
</AbsoluteFill>;
