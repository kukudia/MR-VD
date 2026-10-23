import React from 'react';
import {AbsoluteFill, interpolate, useCurrentFrame} from 'remotion';

export const C = {bg: '#f3f4ef', ink: '#203b35', muted: '#71817a', line: '#d4ddd5', green: '#26947b', pale: '#c6dfd2', warm: '#c58a55', white: '#ffffff', grey: '#b7c1b9'};
export const FONT = '"Microsoft YaHei", "Segoe UI", sans-serif';
export const clamp = (n: number) => Math.max(0, Math.min(1, n));
export const ramp = (f: number, a: number, b: number) => {const t = clamp((f-a)/(b-a)); return t*t*(3-2*t);};
export const mix = (a: number,b: number,t: number) => a+(b-a)*t;
export const pointsPath = (p: number[][]) => p.map((q,i)=>`${i?'L':'M'}${q[0].toFixed(2)},${q[1].toFixed(2)}`).join(' ');

export const Text: React.FC<{x:number;y:number;size?:number;color?:string;anchor?:'start'|'middle'|'end';weight?:number;children:React.ReactNode}> = ({x,y,size=25,color=C.ink,anchor='start',weight=400,children}) => <text x={x} y={y} fill={color} fontSize={size} fontWeight={weight} textAnchor={anchor} fontFamily={FONT}>{children}</text>;
export const Svg: React.FC<{children:React.ReactNode}> = ({children}) => <svg width={1320} height={640} viewBox="0 0 1320 640" style={{overflow:'visible'}}>{children}</svg>;
export const Line: React.FC<{x1:number;y1:number;x2:number;y2:number;color?:string;dash?:boolean}> = ({x1,y1,x2,y2,color=C.line,dash}) => <line x1={x1} y1={y1} x2={x2} y2={y2} stroke={color} strokeWidth={2} strokeDasharray={dash?'7 7':undefined}/>;
export const Arrow: React.FC<{x1:number;y1:number;x2:number;y2:number;color?:string;progress?:number}> = ({x1,y1,x2,y2,color=C.green,progress=1}) => {const a=Math.atan2(y2-y1,x2-x1);return <g opacity={progress}><Line {...{x1,y1,x2,y2,color}}/><path d={`M${x2-12*Math.cos(a-.5)},${y2-12*Math.sin(a-.5)} L${x2},${y2} L${x2-12*Math.cos(a+.5)},${y2-12*Math.sin(a+.5)}`} fill="none" stroke={color} strokeWidth={2.5}/></g>;};
export const DotFlow: React.FC<{x1:number;y1:number;x2:number;y2:number;frame:number;color?:string}> = ({x1,y1,x2,y2,frame,color=C.green}) => <>{[0,1,2].map(i=>{const t=((frame/90+i/3)%1+1)%1;return <circle key={i} cx={mix(x1,x2,t)} cy={mix(y1,y2,t)} r={5} fill={color}/>;})}</>;
export const Cursor: React.FC<{x:number;y:number;scale?:number;color?:string}> = ({x,y,scale=1,color=C.ink}) => <path transform={`translate(${x} ${y}) scale(${scale})`} d="M0 0 L0 34 L9 25 L17 43 L24 39 L16 22 L29 22 Z" fill={color} stroke={C.white} strokeWidth={2} strokeLinejoin="round"/>;

export const Frame: React.FC<{part:number;title:string;kicker:string;duration:number;steps:string[];active:number;caption:string;note?:string;children:React.ReactNode}> = ({part,title,kicker,duration,steps,active,caption,note,children}) => {
 const f=useCurrentFrame();
 const visibility=Math.min(ramp(f,0,14),1-ramp(f,duration-12,duration-1));
 return <AbsoluteFill style={{background:C.bg,color:C.ink,fontFamily:FONT}}>
  <div style={{opacity:visibility}}>
   <div style={{position:'absolute',left:88,top:54,fontSize:22,letterSpacing:2,color:C.green}}>MR-VD <span style={{color:C.muted,marginLeft:20}}> / {kicker}</span></div>
   <div style={{position:'absolute',left:84,top:96,fontSize:62,fontWeight:700,letterSpacing:-1}}>{title}</div>
   <div style={{position:'absolute',right:90,top:68,fontSize:22,color:C.muted}}>0{part} / 06</div>
   <div style={{position:'absolute',left:54,top:244,width:1320,height:640}}>{children}</div>
   <div style={{position:'absolute',left:1432,top:287,width:380,borderLeft:`1px solid ${C.line}`,paddingLeft:34}}>
    {steps.map((s,i)=><div key={s} style={{display:'flex',alignItems:'baseline',gap:15,marginBottom:37,color:i===active?C.ink:C.muted,opacity:i===active?1:.53}}><span style={{fontSize:20,color:i===active?C.green:C.muted,fontWeight:700}}>{String(i+1).padStart(2,'0')}</span><span style={{fontSize:29,fontWeight:i===active?700:400,lineHeight:1.55}}>{s}</span></div>)}
    {note&&<div style={{fontSize:22,lineHeight:1.8,color:C.muted,marginTop:45,whiteSpace:'pre-line'}}>{note.replace(/\\n/g,'\n')}</div>}
   </div>
   <div style={{position:'absolute',left:90,top:923,fontSize:30,lineHeight:1.5,fontWeight:500}}>{caption}</div>
  </div>
  <div style={{position:'absolute',left:90,right:90,bottom:63,height:3,background:C.line}}><div style={{width:`${100*clamp(f/(duration-1))}%`,height:'100%',background:C.green}}/></div>
 </AbsoluteFill>;
};

export const Panel: React.FC<{x:number;y:number;w:number;h:number;children?:React.ReactNode;accent?:boolean}> = ({x,y,w,h,children,accent}) => <g><rect x={x} y={y} width={w} height={h} rx={10} fill={accent?'#e5eee5':'#f9faf6'} stroke={accent?C.green:C.line} strokeWidth={1.5}/>{children}</g>;

// Isometric boxes keep parallel edges exact; all labels stay in screen space.
export const Block: React.FC<{x:number;y:number;w:number;h:number;depth?:number;color?:string}> = ({x,y,w,h,depth=18,color=C.green}) => <g><path d={`M${x},${y} l${depth},${-depth*.6} h${w} l${-depth},${depth*.6} Z`} fill={C.pale}/><path d={`M${x+w},${y} l${depth},${-depth*.6} v${h} l${-depth},${depth*.6} Z`} fill={C.ink}/><rect x={x} y={y} width={w} height={h} fill={color}/></g>;
