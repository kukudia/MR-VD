import React from 'react';
import {AbsoluteFill,useCurrentFrame} from 'remotion';
import {C,FONT,ramp} from './Design';
import {Room} from './Room';

export const Bookend:React.FC<{outro?:boolean}> = ({outro=false}) => {
 const f=useCurrentFrame();
 return <AbsoluteFill style={{background:C.bg,fontFamily:FONT,color:C.ink}}>
  <div style={{position:'absolute',left:90,top:100,opacity:ramp(f,0,15)}}>
   <div style={{fontSize:76,fontWeight:700,marginTop:15,letterSpacing:-2}}>MR-VD · 混合现实桌面</div>
   <div style={{fontSize:30,marginTop:15,color:C.muted}}>{outro?'在现实空间中查看桌面、声音与信息':'桌面捕获 · 音频分析 · 信息采集'}</div>
  </div>
  <div style={{position:'absolute',left:435,top:343,opacity:ramp(f,10,32)}}><Room compact virtual={outro?1:ramp(f,20,65)} turn={ramp(f,0,120)*.08}/></div>
 </AbsoluteFill>;
};
