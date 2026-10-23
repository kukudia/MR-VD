import React from 'react';
import {AbsoluteFill,useCurrentFrame} from 'remotion';
import {C,FONT,ramp} from './Design';
import {Room} from './Room';

export const Bookend:React.FC<{outro?:boolean}> = ({outro=false}) => {
 const f=useCurrentFrame();
 return <AbsoluteFill style={{background:C.bg,fontFamily:FONT,color:C.ink}}>
  <div style={{position:'absolute',left:90,top:100,opacity:ramp(f,0,15)}}>
   <div style={{fontSize:25,letterSpacing:4,color:C.green}}>{outro?'REALITY + DESKTOP + DATA':'MIXED REALITY VIRTUAL DESKTOP'}</div>
   <div style={{fontSize:100,fontWeight:700,marginTop:15,letterSpacing:-3}}>MR-VD</div>
   <div style={{fontSize:30,marginTop:15,color:C.muted}}>{outro?'桌面、声音与信息，进入同一空间':'桌面捕获 · 音频分析 · 空间信息'}</div>
  </div>
  <div style={{position:'absolute',left:435,top:343,opacity:ramp(f,10,32)}}><Room compact virtual={outro?1:ramp(f,20,65)} turn={ramp(f,0,120)*.08}/></div>
 </AbsoluteFill>;
};
