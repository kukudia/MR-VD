import React from 'react';
import { Composition } from 'remotion';
import { MRVDShowcase } from './MRVDShowcase';
import { MRVDShowcaseV2 } from './MRVDShowcaseV2';
import { MRVDShowcaseV3 } from './v3/Reel';

export const Root: React.FC = () => (
  <>
    <Composition id="MRVDShowcaseV3" component={MRVDShowcaseV3} durationInFrames={2700} fps={30} width={1920} height={1080} />
    <Composition
    id="MRVDShowcase"
    component={MRVDShowcase}
    durationInFrames={90 * 30}
    fps={30}
    width={1920}
    height={1080}
    defaultProps={{}}
    />
    <Composition
    id="MRVDShowcaseV2"
    component={MRVDShowcaseV2}
    durationInFrames={90 * 30}
    fps={30}
    width={1920}
    height={1080}
    defaultProps={{}}
    />
  </>
);
