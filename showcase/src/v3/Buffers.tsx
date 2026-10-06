import React from 'react';
import {useCurrentFrame} from 'remotion';
import {Arrow,Block,C,DotFlow,Frame,Line,Panel,Svg,Text,clamp,mix,ramp} from './Design';

export const Buffers: React.FC = () => {
 const f=useCurrentFrame();const step=f<100?0:f<220?1:f<340?2:3;
 // Physical allocations A/B/C keep their identity as their roles are exchanged.
 const states=[[0,1,2],[1,0,2],[1,2,0],[2,1,0],[2,0,1],[0,2,1],[0,1,2]];
 const t=clamp((f-70)/38);const index=Math.min(5,Math.floor(Math.max(0,(f-70)/38)));
 const phase=f>=298?1:ramp(Math.max(0,f-70)%38,5,25);const state=f<70?states[0]:states[index];const next=f<70?states[0]:states[index+1];
 const release=ramp(f,366,400);
 return <Frame part={3} title="捕获与渲染，各自推进" kicker="THREADS & MEMORY" duration={420} steps={['后台捕获','完整帧交接','主线程上传','复用与释放']} active={step} caption={['捕获线程写入独立内存，Unity 主线程继续渲染','短锁保护指针交换，只发布完整帧','主线程复制像素并 Apply；较旧待处理帧可被新帧替换','三块缓冲区反复复用；停止捕获后释放资源'][step]} note="三块独立内存\n指针交换交接所有权\n纹理 API 留在主线程">
 <Svg>
  <Text x={50} y={53} size={27} color={C.green}>捕获线程</Text><Line x1={225} y1={44} x2={1225} y2={44}/>
  {[0,1,2,3,4,5,6].map(i=><rect key={i} x={250+i*132} y={26} width={76} height={36} rx={4} fill={i<Math.floor(f/24)%8?C.green:C.pale}/>)}
  <Text x={50} y={588} size={27}>Unity 主线程</Text><Line x1={260} y1={579} x2={1225} y2={579}/>
  {[0,1,2,3,4,5,6,7,8].map(i=><rect key={i} x={282+i*102} y={561} width={51} height={36} rx={4} fill={i<Math.floor(f/18)%10?C.ink:C.pale}/>)}
  {['Writing','Pending','Uploading'].map((s,i)=><g key={s}><Text x={245+i*402} y={164} anchor="middle" size={29} weight={600}>{s}</Text><Text x={245+i*402} y={206} anchor="middle" size={22} color={C.muted}>{['后台写入','最新完整帧','供主线程复制'][i]}</Text></g>)}
  {[0,1,2].map(id=>{const oldSlot=state.indexOf(id),newSlot=next.indexOf(id);const x=mix(120+oldSlot*402,120+newSlot*402,phase);const y=280+(oldSlot===newSlot?0:(oldSlot<newSlot?-1:1)*Math.sin(phase*Math.PI)*48);return <g key={id} opacity={1-release}><Block x={x} y={y} w={240} h={90} color={['#88bba6','#a6c9b4','#c1d8c7'][id]}/><Text x={x+120} y={y+60} anchor="middle" size={35} weight={700}>{String.fromCharCode(65+id)}</Text></g>;})}
  <g opacity={1-release}><Arrow x1={385} y1={454} x2={490} y2={454}/><Arrow x1={787} y1={454} x2={892} y2={454}/><Text x={640} y={505} anchor="middle" size={25}>{step===0?'每次捕获只写入 Writing':step===1?'交换的是指针，不是整块像素内存':'MemCpy → Unity 纹理内存 → Apply'}</Text></g>
  <g opacity={release}><Panel x={223} y={250} w={875} h={168} accent><Text x={660} y={313} anchor="middle" size={32} weight={600}>停止 → 捕获线程退出 → 释放缓冲区</Text><Text x={660} y={370} anchor="middle" size={25}>原生资源释放 · Unity 纹理销毁</Text></Panel></g>
 </Svg></Frame>;
};
