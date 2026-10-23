import React from 'react';
import {useCurrentFrame} from 'remotion';
import {Arrow,Block,C,DotFlow,Frame,Line,Panel,Svg,Text,pointsPath,ramp} from './Design';

const Weather: React.FC<{x:number;y:number}> = ({x,y}) => <g transform={`translate(${x} ${y})`}><circle cx={35} cy={28} r={25} fill={C.warm}/>{Array.from({length:8},(_,i)=>{const a=i*Math.PI/4;return <line key={i} x1={35+Math.cos(a)*33} y1={28+Math.sin(a)*33} x2={35+Math.cos(a)*42} y2={28+Math.sin(a)*42} stroke={C.warm} strokeWidth={3}/>;})}<path d="M14 77 C-14 74 -8 43 14 45 C10 11 65 8 73 40 C107 36 117 76 88 77 Z" fill={C.pale}/></g>;
const Chip:React.FC<{x:number;y:number;label:string}> = ({x,y,label}) => <g><Block x={x} y={y} w={124} h={104} color={C.pale}/>{Array.from({length:6},(_,i)=><g key={i}><Line x1={x-13} y1={y+12+i*15} x2={x} y2={y+12+i*15} color={C.green}/><Line x1={x+124} y1={y+12+i*15} x2={x+137} y2={y+12+i*15} color={C.green}/></g>)}<Text x={x+62} y={y+62} anchor="middle" size={27} weight={600}>{label}</Text></g>;
export const Information: React.FC = () => {
 const f=useCurrentFrame();const step=f<80?0:f<175?1:f<285?2:3;
 return <Frame part={6} title="信息采集，汇入空间面板" kicker="INFORMATION & TELEMETRY" duration={360} steps={['本地时间与地点','经纬度查询天气','计算机性能采样','面板与历史曲线']} active={step} caption={[
  '时间读取本地时钟；地点来自 IP 定位，可配置手动坐标',
  '经纬度传给 Open-Meteo，解析温度、天气、湿度与风速',
  '采集 CPU、GPU、内存和网络数据，转换为可读指标',
  '信息按各自周期更新，性能采样追加到历史曲线'
 ][step]} note="数值为演示数据\n传感器不可用时显示 N/A">
 <Svg>
 {step===0&&<>
  <Panel x={65} y={96} w={445} h={355}><Text x={285} y={161} anchor="middle" size={25}>本地时钟 / DateTime.Now</Text><Text x={285} y={275} anchor="middle" size={68} weight={600}>14:30:{String(Math.floor(f/30)).padStart(2,'0')}</Text><Text x={285} y={363} anchor="middle" size={25} color={C.muted}>日期 · 时间 · 时区</Text></Panel>
  <Text x={610} y={283} anchor="middle" size={38} color={C.muted}>+</Text>
  <Panel x={715} y={96} w={525} h={355}><Text x={978} y={161} anchor="middle" size={25}>IP 地理位置 / ipwho.is</Text><path d="M975 206 C915 206 915 280 975 322 C1035 280 1035 206 975 206 Z" fill={C.green}/><circle cx={975} cy={244} r={13} fill={C.bg}/><Text x={978} y={378} anchor="middle" size={28}>城市名称 + 经纬度</Text></Panel>
  <Text x={650} y={552} anchor="middle" size={26} color={C.muted}>IP 定位表示近似地点</Text>
 </>}
 {step===1&&<g opacity={.3+.7*ramp(f,80,92)}>
  <Panel x={60} y={150} w={340} h={250}><Text x={230} y={222} anchor="middle" size={27}>位置数据</Text><Text x={230} y={289} anchor="middle" size={31}>纬度 / 经度</Text><Text x={230} y={352} anchor="middle" size={23} color={C.muted}>IP 查询或手动配置</Text></Panel>
  <Arrow x1={425} y1={265} x2={594} y2={265}/><DotFlow x1={425} y1={265} x2={594} y2={265} frame={f}/><Text x={510} y={219} anchor="middle" size={22}>请求</Text>
  <Panel x={630} y={91} w={610} h={398} accent><Text x={937} y={153} anchor="middle" size={27}>Open-Meteo → 天气快照</Text><Weather x={713} y={232}/><Text x={916} y={298} size={72} weight={600}>28°</Text><Text x={916} y={344} size={27}>多云</Text><Text x={935} y={430} anchor="middle" size={24}>湿度 65%　风速 8 km/h</Text></Panel>
  <Text x={650} y={567} anchor="middle" size={25} color={C.muted}>请求 → JSON 解析 → 保存快照 → 更新 UI</Text>
 </g>}
 {step>=2&&<g opacity={.3+.7*ramp(f,175,190)}>
  {[['CPU','LibreHardwareMonitor'],['GPU','LibreHardwareMonitor'],['RAM','GlobalMemoryStatusEx'],['NET','网卡字节差 / 时间差']].map(([name,source],i)=><g key={name}><Chip x={74+i*316} y={85} label={name}/><Text x={135+i*316} y={237} anchor="middle" size={19} color={C.muted}>{source}</Text><Arrow x1={135+i*316} y1={266} x2={135+i*316} y2={320}/><DotFlow x1={135+i*316} y1={266} x2={135+i*316} y2={320} frame={f-i*14}/></g>)}
  {['CPU','GPU','内存','网络'].map((name,i)=>{const age=Math.max(0,f-180);const value=[32,47,58,8][i]+Math.round(Math.sin(Math.floor(age/30)*.8+i)*[5,7,2,1][i]);return <Panel key={name} x={35+i*316} y={341} w={279} h={213} accent={step===3}>
   <Text x={58+i*316} y={386} size={24}>{name}</Text><Text x={286+i*316} y={435} anchor="end" size={42} weight={600}>{value}{i===3?' Mb/s':'%'}</Text>
   <path d={pointsPath(Array.from({length:121},(_,j)=>[56+i*316+j*1.975,511-20*Math.sin(j*.07+i)-10*Math.sin(j*.215+i)]))} fill="none" stroke={C.green} strokeWidth={2.5} pathLength={1} strokeDasharray={1} strokeDashoffset={1-ramp(f,200,320)}/>
  </Panel>;})}
  <Text x={650} y={615} anchor="middle" size={24} color={C.muted}>{step===2?'CPU / GPU 负载 · 物理内存占用 · 网络吞吐率':'独立更新周期 · 有限历史样本 · 异常值显示 N/A'}</Text>
 </g>}
 </Svg></Frame>;
};
