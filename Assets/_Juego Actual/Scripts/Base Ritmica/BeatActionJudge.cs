using System;
using UnityEngine;

/// <summary>
/// Tracks whether the current beat's single action slot has been claimed.
/// Necrodancer-style: no accuracy grading, no knowledge of input devices or
/// action types - consumers poll their own input and call TryConsumeBeat()
/// when they see a press. One slot per beat, shared across all action types
/// (movement, attack, etc). A beat that closes with no action claimed at all
/// fires OnBeatMissed instead.
/// </summary>
public class BeatActionJudge : MonoBehaviour {

    [Header("Timing")]
    [Tooltip("How far from the nearest beat, in fractions of a beat, a press is still accepted.")]
    [SerializeField, Range(0.05f, 0.5f)] private float beatTolerance = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool logActions = true;

    /// <summary>Fired when a beat's window closes with no action claimed at all.</summary>
    public event Action OnBeatMissed;

    private int currentOpenBeat;
    private bool currentBeatConsumed;
    private bool initialized;

    private void Update() {
        if (BeatManager.Instance == null || !BeatManager.Instance.IsPlaying) return;

        int beatNow = NearestBeat(BeatManager.Instance.CurrentSongTime);

        if (!initialized) {
            currentOpenBeat = beatNow;
            currentBeatConsumed = false;
            initialized = true;
        }

        // Advance past any beat whose window has now closed, firing Missed for
        // any that never got an action. Handles more than one beat closing
        // between frames too (e.g. a brief frame hitch).
        while (currentOpenBeat < beatNow) {
            if (!currentBeatConsumed) {
                if (logActions) Debug.Log($"Beat {currentOpenBeat}: missed");
                OnBeatMissed?.Invoke();
            }
            currentOpenBeat++;
            currentBeatConsumed = false;
        }
    }

    /// <summary>
    /// Called by a consumer when it sees its own input. Returns true and claims
    /// the current beat's action slot if the press landed within beatTolerance
    /// of the nearest beat and the slot wasn't already claimed. Returns false
    /// for presses that are too early/late, or a repeat press this beat.
    /// </summary>
    public bool TryConsumeBeat() {
        if (currentBeatConsumed) return false;
        if (BeatManager.Instance == null || !BeatManager.Instance.IsPlaying) return false;

        double beatPosition = BeatManager.Instance.CurrentSongTime / BeatManager.Instance.SecPerBeat;
        double distance = Math.Abs(beatPosition - currentOpenBeat);

        if (distance > beatTolerance) {
            if (logActions) Debug.Log($"Beat {currentOpenBeat}: press rejected, {distance:F2} beats off (tolerance {beatTolerance:F2})");
            return false;
        }

        currentBeatConsumed = true;

        if (logActions) Debug.Log($"Beat {currentOpenBeat}: action consumed");

        return true;
    }

    /// <summary>
    /// Rounds a moment in time to its nearest beat number - the core of
    /// Necrodancer's "half a beat either side" window. Beat N's window spans
    /// from the midpoint before it to the midpoint after it.
    /// </summary>
    private int NearestBeat(double songTime) {
        return (int)Math.Round(songTime / BeatManager.Instance.SecPerBeat, MidpointRounding.AwayFromZero);
    }
}
