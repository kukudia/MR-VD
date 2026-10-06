import {interpolate, useCurrentFrame} from 'remotion';

// Retiming preserves the order of the explanatory actions while adding distinct
// reading pauses to each phase. Audio data always uses the real local frame.
const timings = {
  mr: {duration:600, input:[0,125,290,435,565,599], output:[0,100,230,330,425,449]},
  capture: {duration:660, input:[0,140,290,405,610,659], output:[0,100,195,270,415,449]},
  buffers: {duration:600, input:[0,125,300,450,555,599], output:[0,100,220,340,400,419]},
  audio: {duration:660, input:[0,140,290,430,625,659], output:[0,110,220,315,455,479]},
  beat: {duration:420, input:[0,95,190,305,390,419], output:[0,75,145,225,280,299]},
  information: {duration:420, input:[0,100,210,320,400,419], output:[0,80,175,285,344,359]},
};
export const useSceneTime = (name:keyof typeof timings) => {
  const frame=useCurrentFrame();
  const timing=timings[name];
  return {frame, duration:timing.duration, f:interpolate(frame,timing.input,timing.output,{extrapolateLeft:'clamp',extrapolateRight:'clamp'})};
};
