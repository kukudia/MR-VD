import React from 'react';
import {useSceneTime} from './Timing';
import {Arrow,Block,C,DotFlow,Frame,Line,Panel,Svg,Text,smoothPath,ramp} from './Design';

const Weather: React.FC<{x:number;y:number}> = ({x,y}) => <g transform={`translate(${x} ${y})`}><circle cx={35} cy={28} r={25} fill={C.warm}/>{Array.from({length:8},(_,i)=>{const a=i*Math.PI/4;return <line key={i} x1={35+Math.cos(a)*33} y1={28+Math.sin(a)*33} x2={35+Math.cos(a)*42} y2={28+Math.sin(a)*42} stroke={C.warm} strokeWidth={3}/>;})}<path d="M14 77 C-14 74 -8 43 14 45 C10 11 65 8 73 40 C107 36 117 76 88 77 Z" fill={C.pale}/></g>;
const Chip:React.FC<{x:number;y:number;label:string}> = ({x,y,label}) => <g><Block x={x} y={y} w={124} h={104} color={C.pale}/>{Array.from({length:6},(_,i)=><g key={i}><Line x1={x-13} y1={y+12+i*15} x2={x} y2={y+12+i*15} color={C.green}/><Line x1={x+124} y1={y+12+i*15} x2={x+137} y2={y+12+i*15} color={C.green}/></g>)}<Text x={x+62} y={y+62} anchor="middle" size={27} weight={600}>{label}</Text></g>;
export const Information: React.FC = () => {
 const {f,frame,duration}=useSceneTime('information');const step=f<80?0:f<175?1:f<285?2:3;
 return <Frame title="信息采集：时间、天气与性能" duration={duration} steps={['时间与位置来源','按位置查询天气','采集计算机性能','刷新数值与曲线']} active={step} caption={[
  '读取本地时间，通过 IP 定位或手动坐标确定地点',
  '按经纬度查询 Open-Meteo，获取温度、湿度与风速',
  '读取 CPU、GPU 负载、物理内存占用与网络吞吐率',
  '各类信息按独立周期刷新，性能采样写入历史曲线'
 ][step]} note="数值为演示数据\n传感器不可用时显示 N/A">
 <Svg>
 {step===0&&<>
  <Panel x={65} y={96} w={445} h={355}><Text x={285} y={161} anchor="middle" size={25}>本地时钟</Text><Text x={285} y={275} anchor="middle" size={68} weight={600}>14:30:{String(Math.floor(frame/30)).padStart(2,'0')}</Text><Text x={285} y={363} anchor="middle" size={25} color={C.muted}>日期 · 时间 · 时区</Text></Panel>
  <Text x={610} y={283} anchor="middle" size={38} color={C.muted}>+</Text>
  <Panel x={715} y={96} w={525} h={355}><Text x={978} y={161} anchor="middle" size={25}>IP 定位 / ipwho.is</Text><path d="M975 206 C915 206 915 280 975 322 C1035 280 1035 206 975 206 Z" fill={C.green}/><circle cx={975} cy={244} r={13} fill={C.bg}/><Text x={978} y={378} anchor="middle" size={28}>城市名称与经纬度</Text></Panel>
  <Text x={650} y={552} anchor="middle" size={26} color={C.muted}>IP 定位提供近似地点，可使用手动坐标</Text>
 </>}
 {step===1&&<g>
  <Panel x={60} y={150} w={340} h={250}><Text x={230} y={222} anchor="middle" size={27}>位置数据</Text><Text x={230} y={289} anchor="middle" size={31}>纬度 / 经度</Text><Text x={230} y={352} anchor="middle" size={23} color={C.muted}>IP 查询或手动配置</Text></Panel>
  <Arrow x1={425} y1={265} x2={594} y2={265}/><DotFlow x1={425} y1={265} x2={594} y2={265} frame={f}/><Text x={510} y={219} anchor="middle" size={22}>请求</Text>
  <Panel x={630} y={91} w={610} h={398} accent><Text x={937} y={153} anchor="middle" size={27}>Open-Meteo 天气数据</Text><Weather x={713} y={232}/><Text x={916} y={298} size={72} weight={600}>28°</Text><Text x={916} y={344} size={27}>多云</Text><Text x={935} y={430} anchor="middle" size={24}>湿度 65%　风速 8 km/h</Text></Panel>
  <Text x={650} y={567} anchor="middle" size={25} color={C.muted}>发送请求 → 解析结果 → 保存天气数据 → 刷新面板</Text>
 </g>}
 {step>=2&&<g>
  {[['CPU','LibreHardwareMonitor'],['GPU','LibreHardwareMonitor'],['RAM','GlobalMemoryStatusEx'],['NET','网卡字节差 / 时间差']].map(([name,source],i)=><g key={name}><Chip x={74+i*316} y={85} label={name}/><Text x={135+i*316} y={237} anchor="middle" size={19} color={C.muted}>{source}</Text><Arrow x1={135+i*316} y1={266} x2={135+i*316} y2={320}/><DotFlow x1={135+i*316} y1={266} x2={135+i*316} y2={320} frame={f-i*14}/></g>)}
  {['CPU','GPU','内存','网络'].map((name,i)=>{const age=Math.max(0,frame-215);const value=[32,47,58,8][i]+Math.round(Math.sin(Math.floor(age/30)*.8+i)*[5,7,2,1][i]);return <Panel key={name} x={35+i*316} y={341} w={279} h={213} accent={step===3}>
   <Text x={58+i*316} y={386} size={24}>{name}</Text><Text x={286+i*316} y={435} anchor="end" size={42} weight={600}>{value}{i===3?' Mb/s':'%'}</Text>
   <path d={smoothPath(Array.from({length:41},(_,j)=>[56+i*316+j*5.925,508-18*Math.sin(j*.21+i)-8*Math.sin(j*.645+i)]))} fill="none" stroke={C.green} strokeWidth={2.5} strokeLinecap="round" pathLength={1} strokeDasharray={1} strokeDashoffset={1-ramp(f,185,335)}/>
  </Panel>;})}
  <Text x={650} y={615} anchor="middle" size={24} color={C.muted}>{step===2?'CPU / GPU 负载 · 物理内存占用 · 网络吞吐率':'定时采样 · 保存历史 · 不可用数据标记 N/A'}</Text>
 </g>}
 </Svg></Frame>;
};
