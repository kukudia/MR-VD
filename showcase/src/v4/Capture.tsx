import React from 'react';
import {useSceneTime} from './Timing';
import {Arrow,Block,C,Cursor,DotFlow,Frame,Line,Panel,Svg,Text,mix,ramp} from './Design';

const Pixels: React.FC<{x:number;y:number;w:number;h:number;frame:number;scan?:boolean}> = ({x,y,w,h,frame,scan}) => <g>
 <rect x={x-9} y={y-9} width={w+18} height={h+18} rx={9} fill={C.ink}/>
 {Array.from({length:160},(_,i)=>{const col=i%16,row=Math.floor(i/16);const active=col<3||row<2;return <rect key={i} x={x+col*w/16} y={y+row*h/10} width={w/16-.7} height={h/10-.7} fill={active?C.pale:(col+row)%5===0?'#b2d2ba':'#e4eee0'}/>;})}
 {scan&&<rect x={x} y={y+(frame%85)/85*h} width={w} height={5} fill={C.green}/>}
</g>;

export const Capture: React.FC = () => {
 const {f,frame,duration}=useSceneTime('capture'); const step=f<100?0:f<195?1:f<270?2:3;
 const mx= mix(.2,.72,ramp(f,290,415)),my=mix(.32,.61,ramp(f,290,415));
 return <Frame title="桌面捕获：像素与鼠标映射" duration={duration} steps={['整帧捕获','像素与透明度','上传屏幕纹理','鼠标位置映射']} active={step} caption={[
  'GDI 复制完整桌面帧，并将系统光标绘入画面',
  'BGRA 每像素占 4 字节，Alpha 设为 255 保持不透明',
  '像素上传为 BGRA32 纹理，再显示到空间屏幕',
  '鼠标相对位置随纹理缩放，在虚拟屏幕上保持一致'
 ][step]} note={step===3?'示例分辨率：1920 × 1080\n坐标原点：左上角':undefined}>
 <Svg>
 {step===0&&<>
  <Text x={85} y={45} size={27}>Windows 桌面</Text><Pixels x={90} y={130} w={465} h={270} frame={f} scan/>
  <Cursor x={360} y={280} scale={.8}/><Text x={310} y={463} anchor="middle" size={24}>桌面画面与系统光标</Text>
  <Arrow x1={590} y1={270} x2={755} y2={270}/><DotFlow x1={590} y1={270} x2={755} y2={270} frame={f}/>
  <Text x={675} y={229} anchor="middle" size={22}>BitBlt</Text>
  <Block x={800} y={130} w={390} h={270} color={C.pale}/>
  {Array.from({length:12},(_,i)=><rect key={i} x={826} y={155+i*19} width={310*ramp(f-i*3,12,45)} height={8} fill={C.green} opacity={.5}/>)}
  <Text x={995} y={463} anchor="middle" size={24}>GetDIBits → 像素缓冲区</Text>
  <Text x={650} y={568} anchor="middle" color={C.muted}>整帧复制 · 像素网格为放大示意</Text>
 </>}
 {step===1&&<g opacity={.3+.7*ramp(f,100,112)}>
  <Pixels x={72} y={123} w={390} h={260} frame={f}/>
  <rect x={218} y={227} width={26} height={27} stroke={C.warm} strokeWidth={4} fill="none"/>
  <path d="M245 230 L540 136 M245 254 L540 398" stroke={C.warm} fill="none" strokeWidth={1.5}/>
  <Text x={803} y={80} anchor="middle" size={30}>一个像素 · BGRA32</Text>
  {['B','G','R','A'].map((s,i)=><g key={s}><Block x={550+i*162} y={170} w={133} h={165} color={i===3?C.green:'#bdd0c2'}/><Text x={616+i*162} y={229} anchor="middle" size={38} weight={700} color={i===3?C.white:C.ink}>{s}</Text><Text x={616+i*162} y={285} anchor="middle" size={32} color={i===3?C.white:C.ink}>{[186,212,197,255][i]}</Text><Text x={616+i*162} y={392} anchor="middle" size={23}>1 字节</Text></g>)}
  <Text x={290} y={455} anchor="middle" size={24}>放大选中的像素</Text>
  <Text x={820} y={480} anchor="middle" size={29}>帧数据大小 = 宽 × 高 × 4 字节</Text>
 </g>}
 {step===2&&<g opacity={.3+.7*ramp(f,195,207)}>
  <Block x={130} y={180} w={255} h={170} color={C.pale}/><Text x={257} y={408} anchor="middle">BGRA32 缓冲区</Text>
  <Arrow x1={425} y1={265} x2={647} y2={265}/><DotFlow x1={425} y1={265} x2={647} y2={265} frame={f}/><Text x={535} y={218} anchor="middle" size={22}>Apply → GPU</Text>
  <g transform="translate(710 100) skewY(-5)"><Pixels x={0} y={0} w={460} h={300} frame={f}/><Cursor x={230} y={170} scale={.8}/></g>
  <Text x={920} y={480} anchor="middle">RawImage 显示桌面纹理</Text>
 </g>}
 {step===3&&<g opacity={.3+.7*ramp(f,270,282)}>
  <Text x={305} y={42} anchor="middle" size={27}>捕获区域 · 1920 × 1080</Text><Text x={990} y={42} anchor="middle" size={27}>虚拟屏幕 · 同一纹理</Text>
  <Pixels x={72} y={107} w={480} h={270} frame={f}/><Pixels x={750} y={152} w={420} h={236.25} frame={f}/>
  <Line x1={72} y1={107+my*270} x2={72+mx*480} y2={107+my*270} color={C.warm} dash/><Line x1={72+mx*480} y1={107} x2={72+mx*480} y2={107+my*270} color={C.warm} dash/>
  <circle cx={72} cy={107} r={5} fill={C.warm}/><Text x={57} y={87} size={22} color={C.warm}>(0, 0)</Text>
  <Line x1={750} y1={152+my*236.25} x2={750+mx*420} y2={152+my*236.25} color={C.warm} dash/>
  <Line x1={750+mx*420} y1={152} x2={750+mx*420} y2={152+my*236.25} color={C.warm} dash/>
  <circle cx={750+mx*420} cy={152+my*236.25} r={12} fill={C.warm} opacity={.23}/>
  <Cursor x={72+mx*480} y={107+my*270} scale={.7}/><Cursor x={750+mx*420} y={152+my*236.25} scale={.61}/>
  <Arrow x1={592} y1={263} x2={704} y2={263}/><Text x={70} y={426} size={25}>x = {Math.round(mx*1920)}　y = {Math.round(my*1080)}</Text>
  <Text x={750} y={439} size={25}>横向 {Math.round(mx*100)}%　纵向 {Math.round(my*100)}%</Text>
  <Panel x={105} y={489} w={1100} h={90}><Text x={655} y={544} anchor="middle" size={28}>归一化坐标 =（鼠标坐标 − 捕获区域原点）÷ 捕获尺寸</Text></Panel>
 </g>}
 </Svg></Frame>;
};
