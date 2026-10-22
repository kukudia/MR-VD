using System;
using UnityEngine;

/// <summary>Authored visual playlist; display-beat durations use the supplied BPM, not song-section recognition.</summary>
[CreateAssetMenu(menuName = "MR-VD/Audio Visual Sequence", fileName = "AudioVisualSequence")]
public sealed class AudioVisualSequence : ScriptableObject
{
    public enum Clock { Seconds, DisplayBeats }

    [Serializable]
    public sealed class Cue
    {
        public AudioVisualLook look;
        [Range(1f, 256f)] public float duration = 32f;
    }

    [SerializeField] private Clock clock = Clock.DisplayBeats;
    [SerializeField] private bool loop = true;
    [SerializeField] private Cue[] cues = Array.Empty<Cue>();
    public Clock TimeBase => clock;
    public bool Loop => loop;
    public int Count => cues != null ? cues.Length : 0;
    public Cue GetCue(int index) => cues[index];

    public bool IsValid()
    {
        if (Count == 0 || Count > 128)
        {
            return false;
        }
        foreach (Cue cue in cues)
        {
            if (cue == null || cue.look == null || float.IsNaN(cue.duration)
                || float.IsInfinity(cue.duration) || cue.duration < 1f)
            {
                return false;
            }
        }
        return true;
    }
}
