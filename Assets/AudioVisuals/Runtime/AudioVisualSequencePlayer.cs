using UnityEngine;

/// <summary>
/// Optional playlist controller. Runtime Update advances an explicit authored clock and applies
/// existing looks; Stop/disable restores the starting look without writing any shared asset.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(110)]
public sealed class AudioVisualSequencePlayer : MonoBehaviour
{
    [SerializeField] private AudioVisualSequence sequence;
    [SerializeField] private AudioVisualLookPlayer target;
    [SerializeField] private AudioVisualField clockSource;
    [SerializeField] private bool playOnEnable;
    [SerializeField] private bool pauseWhenQuiet = true;
    [SerializeField] private bool restoreOnStop = true;
    private AudioVisualLook startingLook;
    private float elapsed;
    private bool started;
    private bool completed;
    public bool IsRunning { get; private set; }
    public int CueIndex { get; private set; } = -1;
    public float Elapsed => elapsed;
    public AudioVisualSequence Sequence => sequence;

    private void OnEnable()
    {
        if (playOnEnable)
        {
            PlaySequence();
        }
    }

    private void Update()
    {
        if (!IsRunning)
        {
            return;
        }
        if (clockSource != null && (!clockSource.isActiveAndEnabled
            || pauseWhenQuiet && clockSource.CurrentFrame.energy < 0.005f))
        {
            return;
        }
        float bpm = clockSource != null ? clockSource.CurrentFrame.bpm : 120f;
        Advance(Time.deltaTime, bpm);
    }

    [ContextMenu("Play or Resume Sequence (Play Mode)")]
    public void PlaySequence()
    {
        if (!Application.isPlaying || target == null || sequence == null || !sequence.IsValid())
        {
            return;
        }
        if (completed)
        {
            StopSequence();
        }
        if (!started)
        {
            startingLook = target.Look;
            CueIndex = 0;
            elapsed = 0f;
            started = true;
            completed = false;
            target.SetLook(sequence.GetCue(CueIndex).look);
        }
        IsRunning = true;
    }

    [ContextMenu("Pause Sequence")]
    public void PauseSequence()
    {
        IsRunning = false;
    }

    [ContextMenu("Next Sequence Cue")]
    public void NextCue()
    {
        if (!started || sequence == null || !sequence.IsValid() || target == null)
        {
            return;
        }
        elapsed = 0f;
        if (CueIndex >= sequence.Count)
        {
            CueIndex = sequence.Count - 1;
        }
        MoveNext();
    }

    [ContextMenu("Stop Sequence and Restore Look")]
    public void StopSequence()
    {
        if (started && restoreOnStop && startingLook != null && target != null)
        {
            target.SetLook(startingLook);
        }
        IsRunning = false;
        started = false;
        completed = false;
        CueIndex = -1;
        elapsed = 0f;
        startingLook = null;
    }

    // Explicit input also allows deterministic Play-mode checks without waiting through each cue.
    public void Advance(float deltaSeconds, float bpm)
    {
        if (!IsRunning || !started)
        {
            return;
        }
        if (sequence == null || !sequence.IsValid() || target == null)
        {
            StopSequence();
            return;
        }
        if (CueIndex >= sequence.Count)
        {
            // Live Inspector edits can shorten the sequence while it is playing.
            CueIndex = 0;
            elapsed = 0f;
            target.SetLook(sequence.GetCue(0).look);
        }
        if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0f)
        {
            return;
        }
        float increment = Mathf.Min(deltaSeconds, 1f);
        if (sequence.TimeBase == AudioVisualSequence.Clock.DisplayBeats)
        {
            if (float.IsNaN(bpm) || float.IsInfinity(bpm) || bpm <= 0f)
            {
                return;
            }
            increment *= Mathf.Clamp(bpm, 20f, 300f) / 60f;
        }
        elapsed += increment;
        while (IsRunning && elapsed >= sequence.GetCue(CueIndex).duration)
        {
            elapsed -= sequence.GetCue(CueIndex).duration;
            MoveNext();
        }
    }

    private void MoveNext()
    {
        int next = CueIndex + 1;
        if (next >= sequence.Count)
        {
            if (!sequence.Loop)
            {
                // A completed performance holds its final look until the user stops/restores.
                IsRunning = false;
                completed = true;
                elapsed = sequence.GetCue(CueIndex).duration;
                return;
            }
            next = 0;
        }
        CueIndex = next;
        target.SetLook(sequence.GetCue(CueIndex).look);
    }

    private void OnDisable()
    {
        StopSequence();
    }
}
