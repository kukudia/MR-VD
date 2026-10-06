import React from 'react';
import {useSceneTime} from './Timing';
import {Frame,C,Svg,Text,Arrow,ramp} from './Design';
import {Room} from './Room';

export const MixedReality: React.FC = () => {
 const {f,frame,duration}=useSceneTime('mr'); const step=f<100?0:f<230?1:f<330?2:3;
 const split=step===2?1:0;
 return <Frame title="混合现实：现实空间与虚拟内容" duration={duration} steps={['现实环境','虚拟内容','分层合成','头部追踪']} active={step} caption={['头显透视显示现实环境，桌椅与墙面仍然可见','Unity 在现实空间中呈现虚拟屏幕、频谱与信息面板','透视画面位于底层，虚拟层的透明区域保留现实画面','头部移动时视角随之更新，虚拟屏幕保持空间位置'][step]} note="灰色：现实环境示意\n绿色：虚拟内容">
  {step!==2&&<Room virtual={step===0?0:1} turn={ramp(f,335,425)*.27}/>}
  <div style={{position:'absolute',inset:0}}><Svg>
   <Text x={75} y={40} size={25} color={C.muted}>现实环境 / Passthrough</Text>
   <g opacity={step>=1?1:0}><Text x={770} y={40} color={C.green}>虚拟内容 / Unity</Text></g>
   <g opacity={split}>
    {[0,1,2].map(i=><g key={i} transform={`translate(${65+i*434},145)`}>
     <path d="M0 50 L300 0 L358 245 L58 295 Z" fill={i===1?'#e2eee4':'#d9dfd6'} stroke={i===1?C.green:C.grey} strokeWidth={2}/>
     {i!==1&&<g><path d="M43 152 L163 132 L195 165 L75 185 Z M75 185 L75 236 M175 169 L175 220" fill="none" stroke={C.muted} strokeWidth={5}/><path d="M230 170 L301 158 L315 202 L244 214 Z" fill={C.grey}/></g>}
     {i!==0&&<g><path d="M80 95 L252 66 L280 198 L108 227 Z" fill={C.ink}/><path d="M90 102 L246 76 L269 191 L115 216 Z" fill={C.pale}/><path d="M117 126 L225 108 M123 153 L232 135 M129 180 L239 163" stroke={C.green} strokeWidth={6}/></g>}
     <Text x={178} y={356} anchor="middle" size={26}>{['透视画面','虚拟层 · 其余区域透明','合成视图'][i]}</Text>
    </g>)}
    <Text x={477} y={296} anchor="middle" size={37} color={C.green}>+</Text><Text x={910} y={296} anchor="middle" size={37} color={C.green}>=</Text>
    <Text x={657} y={595} anchor="middle" size={25}>透明区域显示现实环境，屏幕区域显示桌面纹理</Text>
   </g>
   <g opacity={step===3?1:0}><path d="M500 550 Q670 610 840 550" fill="none" stroke={C.green} strokeWidth={2}/><circle cx={500+340*ramp(f,345,425)} cy={550+120*ramp(f,345,425)*(1-ramp(f,345,425))} r={10} fill={C.green}/><Text x={670} y={625} anchor="middle" size={23}>视点移动</Text></g>
  </Svg></div>
 </Frame>;
};
