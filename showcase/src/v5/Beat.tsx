import React from 'react';
import {useSceneTime} from './Timing';
import {C,Frame,Line,Panel,Svg,Text,pointsPath} from './Design';

// Reserve the right side for explanations so peaks and scan lines never cover text.
const plotX = (x:number) => 80+(x-80)*.79;

export const Beat: React.FC = () => {
 const {f,duration}=useSceneTime('beat');
 const step=f<75?0:f<145?1:f<225?2:3;
 const scan=80+1160*Math.min(1,f/250);
 const energy=(x:number)=>{
  let n=.15+.015*Math.sin(x*.024);
  for(let i=0;i<7;i++) n+=.67*Math.exp(-Math.pow((x-(145+i*160))/17,2));
  return n+.53*Math.exp(-Math.pow((x-677)/12,2));
 };
 return <Frame title="节拍检测：能量变化与 BPM" duration={duration}
  steps={['频段能量','自适应阈值','置信度与冷却','节拍间隔 → BPM']} active={step}
  caption={['分别跟踪底鼓、军鼓与低音频段的能量','根据历史能量的均值与标准差，动态调整检测阈值','结合能量突增、置信度和冷却时间，筛选有效节拍','对节拍间隔进行候选评分，平滑更新 BPM'][step]}
  note="节拍判定示意 · 慢放\n120 BPM · 间隔 0.5 秒">
 <Svg>
  <Text x={80} y={40} size={27}>频段能量</Text>
  {[0,1,2].map(i=><Line key={i} x1={80} y1={120+i*95} x2={plotX(1240)} y2={120+i*95}/>)}
  {step>=2&&<rect x={plotX(625)} y={92} width={88*.79} height={285} fill={C.warm} opacity={.14}/>}
  <path d={pointsPath(Array.from({length:581},(_,i)=>[plotX(80+i*2),365-energy(80+i*2)*265]))} fill="none" stroke={C.green} strokeWidth={4}/>
  {step>=1&&<>
   <path d={pointsPath(Array.from({length:160},(_,i)=>[plotX(80+i*7.29),234-9*Math.sin(i*.06)]))} fill="none" stroke={C.warm} strokeWidth={2.5} strokeDasharray="8 7"/>
   <Line x1={1025} y1={221} x2={1061} y2={221} color={C.warm} dash/>
   <Text x={1080} y={230} size={26} color={C.warm} weight={600}>自适应阈值</Text>
   <Text x={1080} y={270} size={22} color={C.muted}>随历史能量变化</Text>
  </>}
  {step>=2&&<>
   <rect x={1030} y={321} width={25} height={25} fill={C.warm} opacity={.35}/>
   <Text x={1080} y={343} size={24} color={C.warm}>冷却期重复峰值</Text>
   <Text x={1080} y={382} size={22} color={C.muted}>不计入 BPM</Text>
  </>}
  <Line x1={plotX(scan)} y1={95} x2={plotX(scan)} y2={390} color={C.ink}/>
  <Line x1={80} y1={430} x2={plotX(1240)} y2={430}/>
  {Array.from({length:7},(_,i)=>{
   const x=145+i*160;
   return <g key={i} opacity={scan>x?1:.22}>
    <circle cx={plotX(x)} cy={430} r={10} fill={C.green}/>
    <Text x={plotX(x)} y={478} anchor="middle" size={21}>{(i*.5).toFixed(1)} s</Text>
   </g>;
  })}
  <Panel x={180} y={521} w={960} h={91} accent>
   <Text x={660} y={578} anchor="middle" size={28} weight={600}>
    {step===3?'示例：60 ÷ 0.5 s = 120 BPM':'超过阈值 → 检查突增 → 置信度与冷却 → 确认节拍'}
   </Text>
  </Panel>
 </Svg></Frame>;
};
