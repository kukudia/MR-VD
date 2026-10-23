import React from 'react';
import {
  AbsoluteFill,
  Easing,
  Img,
  Sequence,
  Audio,
  interpolate,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from 'remotion';
import { Video } from '@remotion/media';

const C = {
  bg: '#071017',
  panel: '#0d1b25',
  panel2: '#102633',
  ink: '#eff8f7',
  muted: '#92aaa9',
  cyan: '#56e7dc',
  blue: '#74a7ff',
  violet: '#bf8dff',
  orange: '#ffb56b',
};

const font = 'Arial, Microsoft YaHei, sans-serif';
const fps = 30;

const Fade: React.FC<{ children: React.ReactNode; start?: number; end?: number }> = ({ children, start = 0, end = 15 }) => {
  const frame = useCurrentFrame();
  const opacity = interpolate(frame, [start, end], [0, 1], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp', easing: Easing.bezier(0.16, 1, 0.3, 1) });
  return <div style={{ opacity }}>{children}</div>;
};

const Kicker: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <div style={{ color: C.cyan, fontSize: 22, letterSpacing: 4, fontWeight: 700, textTransform: 'uppercase' }}>{children}</div>
);

const Title: React.FC<{ children: React.ReactNode; size?: number }> = ({ children, size = 76 }) => (
  <div style={{ color: C.ink, fontSize: size, fontWeight: 700, lineHeight: 1.05 }}>{children}</div>
);

const Pill: React.FC<{ children: React.ReactNode; color?: string }> = ({ children, color = C.blue }) => (
  <div style={{ border: `1px solid ${color}66`, background: `${color}18`, color, padding: '10px 16px', borderRadius: 4, fontSize: 22, fontWeight: 600 }}>{children}</div>
);

const Grid: React.FC = () => (
  <AbsoluteFill style={{ backgroundImage: 'linear-gradient(#17313b 1px, transparent 1px), linear-gradient(90deg, #17313b 1px, transparent 1px)', backgroundSize: '64px 64px', opacity: 0.23 }} />
);

const Section: React.FC<{ kicker: string; title: string; body: string; children?: React.ReactNode }> = ({ kicker, title, body, children }) => (
  <AbsoluteFill style={{ padding: '112px 120px', background: 'rgba(7, 16, 23, 0.78)', color: C.ink, fontFamily: font }}>
    <Grid />
    <div style={{ position: 'relative', zIndex: 1, width: 1080 }}>
      <Kicker>{kicker}</Kicker>
      <div style={{ marginTop: 22 }}><Title size={64}>{title}</Title></div>
      <div style={{ marginTop: 28, color: C.muted, fontSize: 28, lineHeight: 1.45, maxWidth: 940 }}>{body}</div>
      {children && <div style={{ marginTop: 58 }}>{children}</div>}
    </div>
  </AbsoluteFill>
);

const CodeCard: React.FC<{ lines: string[]; accent?: string }> = ({ lines, accent = C.cyan }) => (
  <div style={{ width: 1050, background: '#08151d', border: '1px solid #23414d', borderRadius: 8, padding: '24px 30px', boxShadow: '0 16px 50px #0008', fontFamily: 'Consolas, monospace', fontSize: 24, lineHeight: 1.65 }}>
    {lines.map((line, i) => <div key={i} style={{ color: i === 1 || i === lines.length - 1 ? accent : '#b7c8c9' }}><span style={{ color: '#47636b', display: 'inline-block', width: 48 }}>{String(i + 1).padStart(2, '0')}</span>{line}</div>)}
  </div>
);

const Bars: React.FC<{ count?: number; color?: string }> = ({ count = 38, color = C.cyan }) => {
  const frame = useCurrentFrame();
  return <div style={{ display: 'flex', alignItems: 'center', gap: 6, height: 180 }}>{Array.from({ length: count }).map((_, i) => {
    const wave = (Math.sin(frame * 0.19 + i * 0.62) + Math.sin(frame * 0.07 + i * 0.17)) * 0.25 + 0.5;
    const h = 22 + wave * 138;
    return <div key={i} style={{ width: 10, height: h, borderRadius: 5, background: i % 3 === 0 ? C.violet : color, boxShadow: `0 0 18px ${color}99` }} />;
  })}</div>;
};

const Flow: React.FC = () => {
  const frame = useCurrentFrame();
  const progress = interpolate(frame, [0, 70], [0, 1], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });
  const nodes = [
    ['CSCore', C.orange], ['FFT4096', C.cyan], ['BPM / Key', C.violet], ['Bars + Lights', C.blue],
  ];
  return <div style={{ display: 'flex', alignItems: 'center', gap: 24 }}>{nodes.map(([label, color], i) => <React.Fragment key={label}>
    <div style={{ width: 210, height: 130, border: `1px solid ${color}88`, background: `${color}18`, borderRadius: 8, display: 'flex', alignItems: 'center', justifyContent: 'center', color, fontSize: 25, fontWeight: 700, textAlign: 'center', boxShadow: `0 0 26px ${color}22` }}>{label}</div>
    {i < nodes.length - 1 && <div style={{ width: 80, height: 3, background: `linear-gradient(90deg, ${C.cyan} ${Math.min(progress * 100, 100)}%, #24424d ${Math.min(progress * 100, 100)}%)` }} />}
  </React.Fragment>)}</div>;
};

const LightRig: React.FC = () => {
  const frame = useCurrentFrame();
  const lights = Array.from({ length: 18 });
  return <div style={{ position: 'relative', width: 1100, height: 410, border: '1px solid #28505b', background: '#091820', borderRadius: 8, overflow: 'hidden' }}>
    <div style={{ position: 'absolute', left: 360, top: 105, width: 380, height: 210, border: `2px solid ${C.cyan}88`, boxShadow: `0 0 70px ${C.cyan}44`, transform: `perspective(600px) rotateX(${Math.sin(frame * .03) * 2}deg)` }} />
    {lights.map((_, i) => { const angle = (i / lights.length) * Math.PI * 2; const x = 550 + Math.cos(angle) * 450; const y = 210 + Math.sin(angle) * 150; const col = i % 3 === 0 ? C.violet : i % 3 === 1 ? C.cyan : C.orange; return <div key={i} style={{ position: 'absolute', left: x - 8, top: y - 8, width: 16, height: 16, borderRadius: 16, background: col, boxShadow: `0 0 ${18 + Math.sin(frame * .1 + i) * 8}px ${col}` }} />; })}
    <div style={{ position: 'absolute', left: 28, bottom: 22, color: C.muted, fontSize: 22 }}>6 spot · 4 rim · 4 chase · 2 laser · 2 strobe</div>
  </div>;
};

const RuntimePanel: React.FC = () => <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, width: 1020 }}>
  {[
    ['AUDIO', 'BPM 128', C.cyan], ['KEY', 'A♭ minor', C.violet], ['WEATHER', '26°C · Clear', C.orange], ['DEVICE', 'Loopback / Ready', C.blue],
  ].map(([a, b, color]) => <div key={a} style={{ background: C.panel, border: `1px solid ${color}55`, padding: '22px 26px', borderRadius: 6 }}><div style={{ color, fontSize: 18, letterSpacing: 3 }}>{a}</div><div style={{ color: C.ink, fontSize: 32, fontWeight: 700, marginTop: 10 }}>{b}</div></div>)}
</div>;

const RecordedClip: React.FC<{ src: string; trimBefore: number; duration: number; from: number; opacity?: number; volume?: number }> = ({ src, trimBefore, duration, from, opacity = 1, volume = 0.18 }) => (
  <Sequence from={from} durationInFrames={duration} layout="none">
    <Video src={staticFile(src)} trimBefore={trimBefore * 30} volume={volume} style={{ width: '100%', height: '100%', objectFit: 'cover', opacity }} />
  </Sequence>
);

export const MRVDShowcase: React.FC = () => {
  const frame = useCurrentFrame();
  const { durationInFrames } = useVideoConfig();
  const vignette = { background: 'radial-gradient(circle at 50% 42%, transparent 0%, #02070bcc 100%)' };
  return <AbsoluteFill style={{ background: C.bg, fontFamily: font, overflow: 'hidden' }}>
    <Audio src={staticFile('media/mr-vd-system-audio.m4a')} volume={0.65} />
    <RecordedClip src="media/oculus-20260926-visualizer.mp4" trimBefore={68} duration={300} from={180} opacity={0.9} volume={0.08} />
    <RecordedClip src="media/oculus-20261001-visualizer.mp4" trimBefore={96} duration={540} from={480} opacity={0.9} volume={0.08} />
    <RecordedClip src="media/oculus-20261003-stage.mp4" trimBefore={228} duration={510} from={1020} opacity={0.88} volume={0.08} />
    <RecordedClip src="media/oculus-20261001-panel.mp4" trimBefore={286} duration={420} from={1530} opacity={0.88} volume={0.08} />
    <RecordedClip src="media/oculus-20260926-wheel.mp4" trimBefore={218} duration={240} from={2370} opacity={0.9} volume={0.08} />
    <div style={vignette} />
    <Sequence from={0} durationInFrames={180} layout="none"><Fade><AbsoluteFill style={{ padding: '118px 120px', justifyContent: 'flex-end' }}><Kicker>MR-VD / MIXED REALITY DESKTOP</Kicker><div style={{ marginTop: 22 }}><Title>把桌面变成<br />可交互舞台</Title></div><div style={{ marginTop: 30, color: C.muted, fontSize: 26 }}>Unity · Meta XR · Real-time Audio Visualizer</div></AbsoluteFill></Fade></Sequence>
    <Sequence from={180} durationInFrames={300} layout="none"><Section kicker="01 / Projection & Interaction" title="屏幕不是窗口，是空间里的界面" body="MR-VD 将桌面内容投影到混合现实空间，并用 Screen/Canvas 承载可交互的设备、音频和运行信息模块"><Flow /></Section></Sequence>
    <Sequence from={480} durationInFrames={540} layout="none"><Section kicker="02 / Audio Analysis" title="从系统声音到实时视觉反馈" body="CSCore 捕获系统音频，FFT4096 提取频谱；能量、节拍、BPM 和调性分析共同驱动可视化"><Bars /><div style={{ marginTop: 34 }}><Flow /></div></Section></Sequence>
    <Sequence from={1020} durationInFrames={510} layout="none"><Section kicker="03 / Stage Director" title="节拍驱动整套灯光系统" body="StageManager 根据音频帧、cue 和屏幕中心目标，协调舞台灯光的颜色、亮度与空间运动"><LightRig /></Section></Sequence>
    <Sequence from={1530} durationInFrames={420} layout="none"><Section kicker="04 / Runtime Dashboard" title="把状态放回用户视线" body="设备、音频、天气与系统信息在 Screen/Canvas 中汇合，形成可读、可操作的运行面板"><RuntimePanel /><div style={{ marginTop: 34 }}><CodeCard lines={['AudioCaptureCSCore.TryUpdateFftData()', 'AudioVisualizer.ProcessFftData()', 'StageManager.Presentation', 'Screen/Canvas → Runtime Dashboard']} accent={C.orange} /></div></Section></Sequence>
    <Sequence from={1950} durationInFrames={420} layout="none"><Section kicker="05 / Technical Core" title="可视化只是结果，系统才是作品" body="频段采样、节拍检测、调性估计与灯光 cue 都保留为可编辑的 Unity 组件参数"><CodeCard lines={['fftSize = 4096;', 'keyMinFrequencyHz = 55;', 'barColorMode = Rhythm;', 'lightingFocusTarget = Screen;']} /><div style={{ marginTop: 40, display: 'flex', gap: 16 }}><Pill color={C.cyan}>FFT</Pill><Pill color={C.violet}>BPM</Pill><Pill color={C.orange}>KEY</Pill><Pill color={C.blue}>LIGHTING</Pill></div></Section></Sequence>
    <Sequence from={2370} durationInFrames={240} layout="none"><AbsoluteFill style={{ padding: '118px 120px', justifyContent: 'center', color: C.ink }}><Grid /><div style={{ position: 'relative' }}><Kicker>06 / Live Composite</Kicker><div style={{ marginTop: 22 }}><Title>空间、音频与界面<br />在同一套运行时里汇合</Title></div><div style={{ marginTop: 34, color: C.muted, fontSize: 28 }}>Unity 6000.3 · Meta XR 203.0.0 · CSCore · FFT4096</div></div></AbsoluteFill></Sequence>
    <Sequence from={2610} durationInFrames={90} layout="none"><AbsoluteFill style={{ padding: '118px 120px', justifyContent: 'center', color: C.ink }}><Grid /><div style={{ position: 'relative' }}><Kicker>MR-VD / SHOWCASE V1</Kicker><div style={{ marginTop: 22 }}><Title size={68}>Mixed Reality Desktop</Title></div></div></AbsoluteFill></Sequence>
    <div style={{ position: 'absolute', left: 48, right: 48, bottom: 28, height: 3, background: '#29424a' }}><div style={{ height: 3, width: `${(frame / durationInFrames) * 100}%`, background: C.cyan }} /></div>
  </AbsoluteFill>;
};
