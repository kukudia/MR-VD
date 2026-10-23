import React from 'react';
import {ThreeCanvas} from '@remotion/three';
import {useCurrentFrame} from 'remotion';
import {C,clamp,ramp} from './Design';

const Box: React.FC<{p:[number,number,number];s:[number,number,number];color?:string;opacity?:number}> = ({p,s,color=C.grey,opacity=1}) => <mesh position={p}><boxGeometry args={s}/><meshStandardMaterial color={color} transparent={opacity<1} opacity={opacity} roughness={.85}/></mesh>;
export const Room: React.FC<{virtual?:number;layers?:number;turn?:number;compact?:boolean}> = ({virtual=1,layers=0,turn=0,compact=false}) => {
 const f=useCurrentFrame();
 return <ThreeCanvas width={1320} height={640} orthographic camera={{position:[0,0,16],zoom:compact?67:75,near:.1,far:60}} style={{width:1320,height:640}}>
  <ambientLight intensity={1.1}/><directionalLight position={[-3,8,10]} intensity={2}/>
  <group rotation={[.27,-.35+turn,0]} position={[0,-.1,0]}>
   <Box p={[0,-2,0]} s={[11,.12,5.5]} color="#dce1d9"/>
   <Box p={[0,.1,-2.6]} s={[11,4.2,.1]} color="#e7eae3"/>
   <Box p={[-5.4,-.2,0]} s={[.1,3.5,5.3]} color="#e0e4dc"/>
   <Box p={[-2.7,-.5,-1.1]} s={[3.1,.15,1.5]} color="#b7c4b9"/>
   {[-4,-1.4].flatMap(x=>[-1.7,-.5].map(z=><Box key={`${x}${z}`} p={[x,-1.2,z]} s={[.1,1.4,.1]} color="#a0aea5"/>))}
   <Box p={[3,-1.2,-1.2]} s={[2.8,1.3,1.3]} color="#c3cdc2"/>
   <Box p={[3,-.2,-1.7]} s={[2.8,1,.3]} color="#ccd4c9"/>
   <Box p={[4.5,-.7,1.3]} s={[.55,2.4,.55]} color="#c1cec0"/>
   <mesh position={[4.5,.75,1.3]}><icosahedronGeometry args={[.7,1]}/><meshStandardMaterial color="#9faf9d" roughness={1}/></mesh>
   {virtual>.001&&<group scale={Math.max(.001,virtual)} position={[0,.15,layers*2]}>
    <Box p={[0,0,0]} s={[4.15,2.45,.09]} color={C.ink}/>
    <Box p={[0,0,.06]} s={[3.95,2.25,.04]} color="#e7f1e8"/>
    <Box p={[-1.53,0,.1]} s={[.65,2,.025]} color={C.pale}/>
    <Box p={[.4,.62,.12]} s={[2.7,.5,.025]} color="#a9cfbc"/>
    <Box p={[-.2,-.31,.12]} s={[1.45,1,.025]} color="#fffef8"/>
    <Box p={[1.23,-.31,.12]} s={[1.1,1,.025]} color="#d2e4d4"/>
    <Box p={[-.1,-1.43,0]} s={[4.5,.035,.2]} color={C.pale}/>
    {Array.from({length:32},(_,i)=>{const h=.12+.48*(.5+.5*Math.sin(i*.65+f*.08));return <Box key={i} p={[-2.12+i*.135,-1.42+h/2,0]} s={[.075,h,.09]} color={C.green}/>;})}
    {[-1,1].map(sign=><group key={sign} position={[sign*3,.3,.1]} rotation={[0,sign*-.16,0]}><Box p={[0,0,0]} s={[1.3,1.65,.07]} color="#f7faf2"/>{[0,1,2,3].map(i=><Box key={i} p={[0,.5-i*.33,.07]} s={[.9,.12,.03]} color={i===0?C.green:C.pale}/>)}</group>)}
   </group>}
  </group>
 </ThreeCanvas>;
};
