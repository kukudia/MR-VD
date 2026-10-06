import React from 'react';
import {AbsoluteFill, useCurrentFrame} from 'remotion';
import {clamp, ramp} from './Design';

// Match the reference outro's original type sizes, weight, spacing and alignment.
// The palette is sampled from the supplied exported JPEG rather than the old video source.
export const DemoTitleCard: React.FC = () => {
  const f = useCurrentFrame();
  const visibility = Math.min(ramp(f, 0, 15), 1 - ramp(f, 105, 119));
  return <AbsoluteFill style={{background:'#f1f2ec',color:'#202c30',fontFamily:'Microsoft YaHei, sans-serif',justifyContent:'center',alignItems:'center'}}>
    <div style={{opacity: clamp(visibility), textAlign:'center'}}>
      <div style={{fontSize:74,fontWeight:600}}>混合现实虚拟桌面</div>
      <div style={{fontSize:30,color:'#687573',marginTop:32}}>实机演示DEMO片段</div>
    </div>
  </AbsoluteFill>;
};
