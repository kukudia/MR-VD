import React from 'react';
import {useCurrentFrame} from 'remotion';
import {C,Frame,Line,Panel,Svg,Text,pointsPath,ramp} from './Design';

export const Beat: React.FC = () => {
 const f=useCurrentFrame(); const step=f<75?0:f<145?1:f<225?2:3;
 const scan=80+1160*Math.min(1,f/250);
 const energy=(x:number)=>{let n=.15+.015*Math.sin(x*.024);for(let i=0;i<7;i++)n+=.67*Math.exp(-Math.pow((x-(145+i*160))/17,2));n+=.53*Math.exp(-Math.pow((x-677)/12,2));return n;};
 return <Frame part={5} title="从能量突变，找到有效节拍" kicker="BEAT DETECTION" duration={300} steps={['频段能量','自适应阈值','置信度与冷却','节拍间隔 → BPM']} active={step} caption={['跟踪 Kick、Snare 与 Bass 频段的能量变化','历史均值与标准差共同形成自适应阈值','能量突增通过置信度与冷却判断，才计为有效节拍','有效节拍间隔进入 BPM 候选评分与平滑'][step]} note="图中为节拍判定示例\n120 BPM · 每拍 0.5 秒">
 <Svg>
  <Text x={70} y={40} size={27}>频段能量</Text>
  {[0,1,2].map(i=><Line key={i} x1={80} y1={120+i*95} x2={1240} y2={120+i*95}/>)}
  <path d={pointsPath(Array.from({length:581},(_,i)=>[80+i*2,365-energy(80+i*2)*265]))} fill="none" stroke={C.green} strokeWidth={4}/>
  <g opacity={ramp(f,65,85)}><path d={pointsPath(Array.from({length:160},(_,i)=>[80+i*7.29,234-9*Math.sin(i*.06)]))} fill="none" stroke={C.warm} strokeWidth={2.5} strokeDasharray="8 7"/><Text x={1190} y={205} anchor="end" size={24} color={C.warm}>自适应阈值</Text></g>
  <Line x1={scan} y1={95} x2={scan} y2={390} color={C.ink}/>
  <Line x1={80} y1={430} x2={1240} y2={430}/>
  {Array.from({length:7},(_,i)=>{const x=145+i*160;return <g key={i} opacity={scan>x?1:.15}><circle cx={x} cy={430} r={11} fill={C.green}/><Text x={x} y={478} anchor="middle" size={21}>{(i*.5).toFixed(1)}s</Text></g>;})}
  <g opacity={ramp(f,150,170)}><rect x={625} y={92} width={88} height={285} fill={C.warm} opacity={.1}/><Text x={754} y={93} size={24} color={C.warm}>冷却期内的重复峰值</Text><path d="M754 106 L700 139" fill="none" stroke={C.warm}/><Text x={754} y={125} size={21} color={C.warm}>不计入 BPM</Text></g>
  <Panel x={235} y={521} w={860} h={91} accent><Text x={665} y={578} anchor="middle" size={32} weight={600}>{step===3?'示例：60 ÷ 0.5 s = 120 BPM':'能量越过阈值 + 正向突增 + 置信度 + 冷却'}</Text></Panel>
 </Svg></Frame>;
};
