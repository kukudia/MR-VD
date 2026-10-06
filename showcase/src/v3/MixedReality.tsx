import React from 'react';
import {useCurrentFrame} from 'remotion';
import {Frame,C,Svg,Text,Arrow,ramp} from './Design';
import {Room} from './Room';

export const MixedReality: React.FC = () => {
 const f=useCurrentFrame(); const step=f<100?0:f<230?1:f<330?2:3;
 const split=ramp(f,180,225)*(1-ramp(f,300,340));
 return <Frame part={1} title="现实与虚拟，在同一空间" kicker="MIXED REALITY" duration={450} steps={['现实环境','虚拟内容','分层合成','头部追踪']} active={step} caption={['桌椅与墙面来自头显透视画面','屏幕、频谱与信息面板由 Unity 渲染','透视层位于虚拟内容下方，透明区域显露现实','头部移动改变观察视角，虚拟屏幕保留空间位置'][step]} note="灰色：现实环境示意\n绿色：虚拟内容">
  <div style={{opacity:1-split*.92}}><Room virtual={ramp(f,95,140)} layers={split} turn={ramp(f,335,425)*.27}/></div>
  <div style={{position:'absolute',inset:0}}><Svg>
   <Text x={75} y={40} size={25} color={C.muted}>现实环境 / Passthrough</Text>
   <g opacity={ramp(f,120,145)}><Text x={770} y={40} color={C.green}>虚拟内容 / Unity</Text></g>
   <g opacity={split}>
    {[0,1,2].map(i=><g key={i} transform={`translate(${65+i*434},145)`}>
     <path d="M0 50 L300 0 L358 245 L58 295 Z" fill={i===1?'#e2eee4':'#d9dfd6'} stroke={i===1?C.green:C.grey} strokeWidth={2}/>
     {i!==1&&<g><path d="M43 152 L163 132 L195 165 L75 185 Z M75 185 L75 236 M175 169 L175 220" fill="none" stroke={C.muted} strokeWidth={5}/><path d="M230 170 L301 158 L315 202 L244 214 Z" fill={C.grey}/></g>}
     {i!==0&&<g><path d="M80 95 L252 66 L280 198 L108 227 Z" fill={C.ink}/><path d="M90 102 L246 76 L269 191 L115 216 Z" fill={C.pale}/><path d="M117 126 L225 108 M123 153 L232 135 M129 180 L239 163" stroke={C.green} strokeWidth={6}/></g>}
     <Text x={178} y={356} anchor="middle" size={26}>{['透视画面','虚拟内容 + 透明区域','合成视图'][i]}</Text>
    </g>)}
    <Text x={477} y={296} anchor="middle" size={37} color={C.green}>+</Text><Text x={910} y={296} anchor="middle" size={37} color={C.green}>=</Text>
    <Text x={657} y={595} anchor="middle" size={25}>透明区域显示透视环境，虚拟屏幕区域保持不透明</Text>
   </g>
   <g opacity={ramp(f,338,363)}><path d="M500 550 Q670 610 840 550" fill="none" stroke={C.green} strokeWidth={2}/><circle cx={500+340*ramp(f,345,425)} cy={550+30*Math.sin(Math.PI*ramp(f,345,425))} r={10} fill={C.green}/><Text x={670} y={625} anchor="middle" size={23}>视点移动</Text></g>
  </Svg></div>
 </Frame>;
};
