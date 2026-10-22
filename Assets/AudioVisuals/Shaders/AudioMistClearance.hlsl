// Output-only VFX particle clearance. Applies to the rendered alpha, never cumulative simulation
// alpha. This conservative center/sphere proxy is a desktop research mask, not per-pixel stereo.
void AudioMistClearance(inout VFXAttributes attributes, in float3 ClearanceEye,
    in float4x4 ClearanceWorldToScreen, in float4 ClearanceRect,
    in float4 ClearanceSettings, in float ClearanceVisibility)
{
    float3 world = TransformPositionVFXToWorld(attributes.position);
    float distanceToEye = distance(world, ClearanceEye);
    float nearFade = smoothstep(ClearanceSettings.y, max(ClearanceSettings.y + 0.1, ClearanceSettings.z), distanceToEye);
    float mask = 1.0;
    if (ClearanceSettings.x > 0.5)
    {
        float3 eye = mul(ClearanceWorldToScreen, float4(ClearanceEye, 1.0)).xyz;
        float3 samplePosition = mul(ClearanceWorldToScreen, float4(world, 1.0)).xyz;
        float denominator = samplePosition.z - eye.z;
        if (abs(denominator) > 0.00001)
        {
            float t = -eye.z / denominator;
            if (t > 0.0)
            {
                float2 intersection = lerp(eye.xy, samplePosition.xy, t);
                // World-space sphere projected onto the screen plane. This bounds sprites for
                // the intended small side-mist graph, but is not an exact ray/sphere projection.
                float2 screenScale = float2(length(ClearanceWorldToScreen[0].xyz), length(ClearanceWorldToScreen[1].xyz));
                float2 guard = screenScale * max(0.0, ClearanceSettings.w) * t;
                float2 outside = max(ClearanceRect.xy - guard - intersection,
                    intersection - ClearanceRect.zw - guard);
                float feather = max(0.001, min(screenScale.x, screenScale.y) * 0.08);
                mask = smoothstep(0.0, feather, max(outside.x, outside.y));
            }
        }
    }
    attributes.alpha *= saturate(ClearanceVisibility) * nearFade * mask;
}
