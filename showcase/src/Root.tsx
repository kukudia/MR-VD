import React from 'react';
import { Composition } from 'remotion';
import { MRVDShowcase } from './MRVDShowcase';
import { MRVDShowcaseV2 } from './MRVDShowcaseV2';
import { MRVDShowcaseV4 } from './v4/Reel';
import { MRVDShowcaseV5 } from './v5/Reel';
import { DemoTitleCard } from './v5/DemoTitleCard';
import { MRVDShowcaseV3 } from './v3/Reel';

export const Root: React.FC = () => (
  <>
    <Composition id="MRVDShowcaseV5" component={MRVDShowcaseV5} durationInFrames={3600} fps={30} width={1920} height={1080} />
    <Composition id="MRVDDemoTitleCard" component={DemoTitleCard} durationInFrames={120} fps={30} width={1920} height={1080} />
    <Composition id="MRVDShowcaseV4" component={MRVDShowcaseV4} durationInFrames={3600} fps={30} width={1920} height={1080} />
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
