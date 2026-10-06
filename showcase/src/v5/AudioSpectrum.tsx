import React from 'react';
import {useSceneTime} from './Timing';
import {Arrow,Block,C,DotFlow,Frame,Line,Panel,Svg,Text,pointsPath,ramp} from './Design';
import data from '../v4/audio-analysis.json';

export const AudioSpectrum: React.FC = () => {
 const {f,frame,duration}=useSceneTime('audio');const step=f<110?0:f<220?1:f<315?2:3;
 const sample=data[Math.min(frame,data.length-1)];
 const barValues=[...sample.bars[0],...sample.bars[1].slice().reverse()];
 return <Frame title="音频可视化：浮点采样与 FFT" duration={duration} steps={['采集声音','FFT 频谱分析','对数频带聚合','更新频谱柱条']} active={step} caption={[
  'CSCore 回环采集系统音频，分别处理左右声道',
  '每声道取 4096 个浮点采样，经 FFT 得到频谱',
  '在 40–4000 Hz 内，按对数频率划分每侧 32 个频带',
  '频带幅值经平滑、对数压缩与限高，更新 64 根频谱柱'
 ][step]} note="采样格式：32 位浮点\n示例采样率：48 kHz\n窗口：4096 个采样\n频率间隔：约 11.7 Hz">
 <Svg>
 {step===0&&<>
  <Text x={80} y={45} size={28}>系统播放 → WASAPI Loopback</Text>
  {[0,1].map(ch=><g key={ch}><Text x={64} y={187+ch*195} size={28} color={C.green}>{ch?'R':'L'}</Text><Line x1={116} y1={178+ch*195} x2={1230} y2={178+ch*195}/><path d={pointsPath((ch?sample.rightWave:sample.wave).map((v,i)=>[120+i*8.55,178+ch*195+v*150]))} stroke={C.green} fill="none" strokeWidth={3}/></g>)}
  <rect x={116} y={97} width={1090} height={355} fill={C.pale} opacity={.2} stroke={C.green} strokeWidth={3}/><Text x={660} y={522} anchor="middle" size={32}>4096 个浮点采样 · 每窗口约 85.3 ms</Text>
 </>}
 {step===1&&<g>
  <Text x={283} y={73} anchor="middle" size={27}>时间域</Text><Text x={974} y={73} anchor="middle" size={27}>频率域</Text>
  <Line x1={65} y1={309} x2={510} y2={309}/><path d={pointsPath(sample.wave.map((v,i)=>[65+i*3.48,309+v*150]))} stroke={C.green} fill="none" strokeWidth={3}/>
  <Arrow x1={545} y1={300} x2={698} y2={300}/><Text x={624} y={245} anchor="middle" size={32} weight={700}>FFT</Text>
  {sample.spectrum.map((v,i)=><rect key={i} x={744+i*4.1} y={437-Math.min(275,v*230)} width={2.6} height={Math.min(275,v*230)} fill={C.green}/>)}
  <Line x1={740} y1={440} x2={1230} y2={440}/><Text x={740} y={489} size={23}>低频</Text><Text x={1227} y={489} anchor="end" size={23}>高频</Text>
  <Text x={644} y={580} anchor="middle" size={25} color={C.muted}>浮点采样 → 正频率谱 → 频带幅值</Text>
 </g>}
 {step===2&&<g>
  <Text x={71} y={58} size={28}>对数频率轴</Text>
  {Array.from({length:32},(_,i)=>{const value=sample.bars[0][i];return <g key={i}><rect x={80+i*36.1} y={120} width={33} height={190} fill={i===8?C.warm:C.pale} opacity={.65}/><rect x={83+i*36.1} y={305-value*157} width={27} height={value*157} fill={i===8?C.warm:C.green}/></g>;})}
  {[40,100,400,1000,4000].map(v=><Text key={v} x={80+Math.log(v/40)/Math.log(100)*1155} y={353} anchor="middle" size={22}>{v} Hz</Text>)}
  <Arrow x1={387} y1={382} x2={387} y2={456} color={C.warm}/><Block x={346} y={480-sample.bars[0][8]*70} w={80} h={sample.bars[0][8]*70+35} color={C.warm}/>
  <Text x={545} y={449} size={29}>选中频带 → 取平均幅值</Text><Text x={545} y={494} size={25} color={C.warm}>频带 09 · 约 126–146 Hz</Text><Text x={545} y={541} size={25} color={C.muted}>平滑 → 对数压缩 → 动态缩放 → 限高</Text>
 </g>}
 {step===3&&<g>
  <Text x={350} y={80} anchor="middle" size={27}>左声道 · 32 个频带</Text><Text x={994} y={80} anchor="middle" size={27}>右声道 · 32 个频带</Text>
  <Line x1={663} y1={125} x2={663} y2={487} dash/>
  {barValues.map((v,i)=>{const x=76+i*18.1+(i>=32?28:0),h=40+v*230;return <Block key={i} x={x} y={445-h} w={10} h={h} depth={6} color={i===8||i===55?C.warm:C.green}/>;})}
  <Line x1={60} y1={465} x2={1272} y2={465}/><Text x={80} y={509} size={22}>40 Hz</Text><Text x={622} y={509} anchor="end" size={22}>4000 Hz</Text><Text x={710} y={509} size={22}>4000 Hz</Text><Text x={1245} y={509} anchor="end" size={22}>40 Hz</Text>
  <Text x={662} y={592} anchor="middle" size={26}>左右声道独立更新，频带排列保持对称</Text>
 </g>}
 </Svg></Frame>;
};
