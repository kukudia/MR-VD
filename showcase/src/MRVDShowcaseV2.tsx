import React, {useMemo} from 'react';
import {Audio, AbsoluteFill, Sequence, Easing, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig} from 'remotion';
import {ThreeCanvas} from '@remotion/three';
import {CatmullRomCurve3, Vector3} from 'three';

const COLORS = {
  background: '#f3f5f1',
  ink: '#1b2622',
  muted: '#708078',
  line: '#d4ddd7',
  accent: '#3c9c7e',
  accentSoft: '#a8d2c0',
  accentPale: '#e0efe8',
  warm: '#d99e73',
  dark3d: '#263b34',
};

const FONT = 'Arial, Microsoft YaHei, sans-serif';
const FPS = 30;
const SCENE_WIDTH = 1160;
const SCENE_HEIGHT = 780;

const clamp01 = (value: number) => Math.max(0, Math.min(1, value));

const useEntrance = () => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  return spring({frame, fps, config: {damping: 200, stiffness: 120}});
};

const PartProgress: React.FC<{index: number; total: number; label: string; duration: number}> = ({index, total, label, duration}) => {
  const frame = useCurrentFrame();
  const progress = clamp01(frame / Math.max(1, duration - 1));
  return <div style={{position: 'absolute', left: 110, right: 110, bottom: 54, zIndex: 10}}>
    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', color: COLORS.muted, fontSize: 18, letterSpacing: 2}}>
      <span>PART {String(index).padStart(2, '0')} / {String(total).padStart(2, '0')} · {label}</span>
      <span>{String(Math.round(progress * 100)).padStart(3, '0')}%</span>
    </div>
    <div style={{height: 3, marginTop: 12, background: COLORS.line, overflow: 'hidden'}}>
      <div style={{height: '100%', width: `${progress * 100}%`, background: COLORS.accent}} />
    </div>
  </div>;
};

const PartLayout: React.FC<{index: number; label: string; title: string; description: string; duration: number; children: React.ReactNode}> = ({index, label, title, description, duration, children}) => {
  const entrance = useEntrance();
  return <AbsoluteFill style={{background: COLORS.background, color: COLORS.ink, fontFamily: FONT}}>
    <div style={{position: 'absolute', left: 110, top: 100, width: 510, zIndex: 4, opacity: entrance, translate: `${(1 - entrance) * -28}px 0px`}}>
      <div style={{fontSize: 18, fontWeight: 700, letterSpacing: 4, color: COLORS.accent}}>MR-VD / {label}</div>
      <div style={{fontSize: 62, lineHeight: 1.12, fontWeight: 700, marginTop: 24, maxWidth: 510}}>{title}</div>
      <div style={{height: 1, width: 112, background: COLORS.accent, marginTop: 30}} />
      <div style={{fontSize: 25, lineHeight: 1.55, color: COLORS.muted, marginTop: 24, maxWidth: 490}}>{description}</div>
    </div>
    <div style={{position: 'absolute', left: 690, top: 100, width: SCENE_WIDTH, height: SCENE_HEIGHT, zIndex: 2}}>{children}</div>
    <PartProgress index={index} total={5} label={label} duration={duration} />
  </AbsoluteFill>;
};

const SceneCanvas: React.FC<{children: React.ReactNode}> = ({children}) => <ThreeCanvas width={SCENE_WIDTH} height={SCENE_HEIGHT} camera={{position: [0, 0, 8.5], fov: 38}} style={{width: SCENE_WIDTH, height: SCENE_HEIGHT}}>
  <ambientLight intensity={1.1} />
  <directionalLight position={[4, 6, 8]} intensity={2.2} color="#ffffff" />
  <pointLight position={[-4, 2, 4]} intensity={22} distance={12} color={COLORS.accent} />
  {children}
</ThreeCanvas>;

const SoftLine: React.FC<{points: [number, number, number][]; color?: string; radius?: number}> = ({points, color = COLORS.accent, radius = 0.025}) => {
  const curve = useMemo(() => new CatmullRomCurve3(points.map(([x, y, z]) => new Vector3(x, y, z)), false, 'centripetal'), [points]);
  return <mesh>
    <tubeGeometry args={[curve, 64, radius, 8, false]} />
    <meshStandardMaterial color={color} emissive={color} emissiveIntensity={0.25} roughness={0.38} />
  </mesh>;
};

const OrbitRing: React.FC<{radius: number; rotation: [number, number, number]; color?: string; offset?: number}> = ({radius, rotation, color = COLORS.accent, offset = 0}) => {
  const frame = useCurrentFrame();
  return <mesh rotation={[rotation[0], rotation[1] + frame * 0.004 + offset, rotation[2]]}>
    <torusGeometry args={[radius, 0.018, 12, 96]} />
    <meshStandardMaterial color={color} emissive={color} emissiveIntensity={0.4} roughness={0.3} />
  </mesh>;
};

const SpatialScreenScene: React.FC = () => {
  const frame = useCurrentFrame();
  const breathe = 1 + Math.sin(frame * 0.055) * 0.035;
  const cards = [-1.35, 0, 1.35];
  return <SceneCanvas>
    <group scale={breathe} rotation={[0.08, Math.sin(frame * 0.02) * 0.16, 0]}>
      <mesh position={[0, 0, 0]}>
        <boxGeometry args={[3.4, 2.2, 0.16]} />
        <meshStandardMaterial color="#f9fbf8" roughness={0.24} metalness={0.08} />
      </mesh>
      <mesh position={[0, 0, 0.1]}>
        <boxGeometry args={[3.05, 1.85, 0.025]} />
        <meshStandardMaterial color={COLORS.accentPale} emissive={COLORS.accentPale} emissiveIntensity={0.35} roughness={0.35} />
      </mesh>
      {cards.map((x, i) => <mesh key={x} position={[x, 0.38 + Math.sin(frame * 0.045 + i) * 0.03, 0.16]}>
        <boxGeometry args={[0.72, 0.52, 0.04]} />
        <meshStandardMaterial color={i === 1 ? COLORS.accent : '#d8e7df'} emissive={i === 1 ? COLORS.accent : '#ffffff'} emissiveIntensity={i === 1 ? 0.22 : 0.08} />
      </mesh>)}
      <OrbitRing radius={2.65} rotation={[1.1, 0.2, 0]} />
      <OrbitRing radius={2.95} rotation={[0.1, 1.15, 0]} color={COLORS.accentSoft} offset={1.7} />
      <SoftLine points={[[-3.1, -1.35, 0], [-1.7, -1.0, 0.2], [0, -1.22, 0.4], [1.7, -1.0, 0.2], [3.1, -1.35, 0]]} color={COLORS.accentSoft} radius={0.018} />
    </group>
  </SceneCanvas>;
};

const AudioFieldScene: React.FC = () => {
  const frame = useCurrentFrame();
  const bars = Array.from({length: 28});
  return <SceneCanvas>
    <group rotation={[0, Math.sin(frame * 0.018) * 0.12, 0]}>
      {bars.map((_, i) => {
        const x = (i - (bars.length - 1) / 2) * 0.19;
        const wave = 0.5 + 0.5 * Math.sin(frame * 0.14 + Math.abs(i - 13.5) * 0.38) * Math.cos(frame * 0.045 + i * 0.18);
        const h = 0.35 + wave * 2.35;
        const mirrored = i < 14 ? COLORS.accent : COLORS.dark3d;
        return <mesh key={i} position={[x, h / 2 - 1.15, 0]}>
          <boxGeometry args={[0.11, h, 0.16]} />
          <meshStandardMaterial color={mirrored} emissive={mirrored} emissiveIntensity={i < 14 ? 0.4 : 0.08} roughness={0.25} />
        </mesh>;
      })}
      <SoftLine points={[[-2.75, -0.95, 0.1], [-1.5, -0.5, 0.15], [-0.6, 0.25, 0.15], [0, 0.0, 0.15], [0.8, 0.9, 0.15], [1.7, 0.15, 0.15], [2.7, 0.62, 0.15]]} color={COLORS.warm} radius={0.032} />
      <OrbitRing radius={3.1} rotation={[Math.PI / 2, 0, 0]} color={COLORS.accentSoft} />
    </group>
  </SceneCanvas>;
};

const StageRigScene: React.FC = () => {
  const frame = useCurrentFrame();
  const lights = Array.from({length: 18});
  return <SceneCanvas>
    <group rotation={[0.08, frame * 0.006, 0]}>
      <mesh position={[0, 0, -0.1]}>
        <boxGeometry args={[2.7, 1.6, 0.14]} />
        <meshStandardMaterial color={COLORS.dark3d} roughness={0.32} metalness={0.18} />
      </mesh>
      <mesh position={[0, 0, 0.0]}>
        <boxGeometry args={[2.38, 1.28, 0.025]} />
        <meshStandardMaterial color={COLORS.accentPale} emissive={COLORS.accentPale} emissiveIntensity={0.55} />
      </mesh>
      {lights.map((_, i) => {
        const theta = (i / lights.length) * Math.PI * 2;
        const x = Math.cos(theta) * 3.15;
        const y = Math.sin(theta) * 1.8;
        const z = Math.sin(theta * 2 + frame * 0.025) * 0.35;
        const pulse = 0.75 + 0.25 * Math.sin(frame * 0.1 + i * 0.7);
        return <group key={i} position={[x, y, z]}>
          <mesh scale={pulse}>
            <sphereGeometry args={[0.12, 20, 20]} />
            <meshStandardMaterial color={i % 3 === 0 ? COLORS.accent : COLORS.warm} emissive={i % 3 === 0 ? COLORS.accent : COLORS.warm} emissiveIntensity={1.2} />
          </mesh>
          <mesh rotation={[0, 0, -theta]} position={[-x * 0.18, -y * 0.18, -0.05]}>
            <coneGeometry args={[0.18, 1.8, 16, 1, true]} />
            <meshStandardMaterial color={i % 3 === 0 ? COLORS.accent : COLORS.warm} transparent opacity={0.08 + pulse * 0.05} depthWrite={false} />
          </mesh>
        </group>;
      })}
      <OrbitRing radius={3.65} rotation={[Math.PI / 2, 0, 0]} color={COLORS.accentSoft} />
    </group>
  </SceneCanvas>;
};

const DashboardScene: React.FC = () => {
  const frame = useCurrentFrame();
  const panels = [[-1.35, 0.8], [1.35, 0.8], [-1.35, -0.8], [1.35, -0.8]];
  return <SceneCanvas>
    <group rotation={[0.08, Math.sin(frame * 0.024) * 0.18, 0]}>
      {panels.map(([x, y], i) => {
        const lift = interpolate((frame - i * 8) / 20, [0, 1], [-0.7, 0], {extrapolateLeft: 'clamp', extrapolateRight: 'clamp'});
        const color = i === 0 ? COLORS.accent : i === 1 ? COLORS.warm : COLORS.dark3d;
        return <group key={i} position={[x, y + lift, 0]} rotation={[0, i % 2 === 0 ? -0.08 : 0.08, 0]}>
          <mesh>
            <boxGeometry args={[1.45, 0.72, 0.1]} />
            <meshStandardMaterial color="#ffffff" roughness={0.3} metalness={0.04} />
          </mesh>
          <mesh position={[0, 0, 0.07]}>
            <boxGeometry args={[1.15, 0.12, 0.02]} />
            <meshStandardMaterial color={color} emissive={color} emissiveIntensity={0.3} />
          </mesh>
          <mesh position={[-0.32, -0.17, 0.07]} scale={[0.55 + 0.15 * Math.sin(frame * 0.05 + i), 0.08, 1]}>
            <boxGeometry args={[0.82, 0.08, 0.02]} />
            <meshStandardMaterial color={COLORS.accentSoft} />
          </mesh>
        </group>;
      })}
      <SoftLine points={[[-2.65, 0, 0], [-1.2, 0, 0.5], [0, 0, 0.7], [1.2, 0, 0.5], [2.65, 0, 0]]} color={COLORS.accent} radius={0.02} />
      <OrbitRing radius={3.2} rotation={[0.9, 0.2, 0]} color={COLORS.accentSoft} />
    </group>
  </SceneCanvas>;
};

const NetworkScene: React.FC = () => {
  const frame = useCurrentFrame();
  const nodes: [number, number, number][] = [[-2.5, 0.8, 0], [-1.25, -0.8, 0.2], [0, 0.8, 0.3], [1.25, -0.8, 0.2], [2.5, 0.8, 0]];
  return <SceneCanvas>
    <group rotation={[0, frame * 0.006, 0]}>
      {nodes.slice(0, -1).map((point, i) => <SoftLine key={i} points={[point, nodes[i + 1]]} color={i % 2 === 0 ? COLORS.accent : COLORS.warm} radius={0.035} />)}
      {nodes.map(([x, y, z], i) => <group key={i} position={[x, y, z]}>
        <mesh scale={1 + 0.08 * Math.sin(frame * 0.08 + i)}>
          <sphereGeometry args={[0.28, 24, 24]} />
          <meshStandardMaterial color={i === 2 ? COLORS.accent : COLORS.dark3d} emissive={i === 2 ? COLORS.accent : COLORS.dark3d} emissiveIntensity={0.45} />
        </mesh>
        <mesh position={[0, 0, -0.08]}>
          <torusGeometry args={[0.43, 0.018, 8, 48]} />
          <meshStandardMaterial color={i === 2 ? COLORS.accent : COLORS.accentSoft} emissive={COLORS.accent} emissiveIntensity={0.25} />
        </mesh>
      </group>)}
      <OrbitRing radius={3.5} rotation={[Math.PI / 2, 0, 0]} color={COLORS.accentSoft} />
    </group>
  </SceneCanvas>;
};

const Intro: React.FC = () => {
  const frame = useCurrentFrame();
  const entrance = interpolate(frame, [0, 25], [0, 1], {extrapolateLeft: 'clamp', extrapolateRight: 'clamp', easing: Easing.bezier(0.16, 1, 0.3, 1)});
  return <AbsoluteFill style={{background: COLORS.background, fontFamily: FONT, color: COLORS.ink}}>
    <div style={{position: 'absolute', left: 110, top: 170, zIndex: 3, opacity: entrance, translate: `${(1 - entrance) * -36}px 0px`}}>
      <div style={{fontSize: 18, letterSpacing: 5, fontWeight: 700, color: COLORS.accent}}>MR-VD / SHOWCASE V2</div>
      <div style={{fontSize: 94, fontWeight: 700, lineHeight: 1.04, marginTop: 26}}>空间、音频<br />与界面</div>
      <div style={{fontSize: 28, color: COLORS.muted, marginTop: 30}}>Mixed Reality Desktop · Unity · Meta XR</div>
    </div>
    <div style={{position: 'absolute', left: 690, top: 110, width: SCENE_WIDTH, height: SCENE_HEIGHT}}><SpatialScreenScene /></div>
  </AbsoluteFill>;
};

const Outro: React.FC = () => <AbsoluteFill style={{background: COLORS.background, fontFamily: FONT, color: COLORS.ink, justifyContent: 'center', paddingLeft: 110}}>
  <div style={{fontSize: 18, letterSpacing: 5, fontWeight: 700, color: COLORS.accent}}>MR-VD / SHOWCASE V2</div>
  <div style={{fontSize: 86, lineHeight: 1.08, fontWeight: 700, marginTop: 24}}>运行时的另一种<br />空间形态</div>
  <div style={{fontSize: 26, color: COLORS.muted, marginTop: 30}}>Audio · Visualization · Stage · Dashboard</div>
  <div style={{height: 3, width: 1600, background: COLORS.accent, marginTop: 58}} />
</AbsoluteFill>;

export const MRVDShowcaseV2: React.FC = () => <AbsoluteFill style={{background: COLORS.background}}>
  <Audio src={staticFile('media/mr-vd-system-audio.m4a')} volume={0.5} />
  <Sequence from={0} durationInFrames={180} layout="none"><Intro /></Sequence>
  <Sequence from={180} durationInFrames={480} layout="none"><PartLayout index={1} label="Spatial Screen" title="屏幕成为空间里的界面" description="把桌面、控制与状态组织成可被注视、靠近和操作的混合现实层。" duration={480}><SpatialScreenScene /></PartLayout></Sequence>
  <Sequence from={660} durationInFrames={540} layout="none"><PartLayout index={2} label="Audio Field" title="声音生成可见的场" description="实时频谱、节拍和调性分析在空间里形成对称、连续的视觉反馈。" duration={540}><AudioFieldScene /></PartLayout></Sequence>
  <Sequence from={1200} durationInFrames={540} layout="none"><PartLayout index={3} label="Stage Director" title="灯光围绕屏幕运动" description="18 个灯光单元围绕屏幕组织成环，节拍改变亮度、色彩与空间方向。" duration={540}><StageRigScene /></PartLayout></Sequence>
  <Sequence from={1740} durationInFrames={540} layout="none"><PartLayout index={4} label="Runtime Dashboard" title="状态回到用户视线" description="音频、设备、天气和系统信息组成可读的运行面板，层级清楚，互不遮挡。" duration={540}><DashboardScene /></PartLayout></Sequence>
  <Sequence from={2280} durationInFrames={300} layout="none"><PartLayout index={5} label="System Constellation" title="多个系统汇合成一个运行时" description="屏幕、音频、灯光和面板通过同一套状态流相互联动。" duration={300}><NetworkScene /></PartLayout></Sequence>
  <Sequence from={2580} durationInFrames={120} layout="none"><Outro /></Sequence>
</AbsoluteFill>;
