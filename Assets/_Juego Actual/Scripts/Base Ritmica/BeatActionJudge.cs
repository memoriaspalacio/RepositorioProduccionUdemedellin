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
    [Tooltip("How far BEFORE the beat, in fractions of a beat, the window opens. " +
             "Larger values forgive pressing ahead of the beat.")]
    [SerializeField, Range(0.05f, 0.5f)] private float earlyTolerance = 1f / 3f;

    [Tooltip("How far AFTER the beat, in fractions of a beat, the window stays open.")]
    [SerializeField, Range(0.05f, 0.5f)] private float lateTolerance = 0.25f;

    [Header("Penalty")]
    [Tooltip("How many upcoming beats are locked out when the player presses off-beat.")]
    [SerializeField, Min(0)] private int penaltyBeats = 2;

    [Header("Debug")]
    [SerializeField] private bool logActions = true;

    /// <summary>Fired when a beat's window closes with no action claimed at all.</summary>
    public event Action OnBeatMissed;

    /// <summary>Fired when a beat's action slot is successfully claimed. Carries the beat number.</summary>
    public event Action<int> OnBeatConsumed;

    /// <summary>
    /// Fired when an off-beat press locks out upcoming beats. Carries the first
    /// locked beat number and how many beats are locked, so a view can mark
    /// exactly those beats as lost.
    /// </summary>
    public event Action<int, int> OnBeatsPenalized;

    /// <summary>How far before the beat the window opens, in beats. Read by views that draw the window.</summary>
    public float EarlyTolerance => earlyTolerance;

    /// <summary>How far after the beat the window closes, in beats. Read by views that draw the window.</summary>
    public float LateTolerance => lateTolerance;

    private int currentOpenBeat;
    private bool currentBeatConsumed;
    private bool initialized;

    // Half-open range [lockedFromBeat, lockedUntilBeat) of beats stolen by a
    // penalty. Equal values mean "no penalty active".
    private int lockedFromBeat;
    private int lockedUntilBeat;

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
    /// True if the given beat was stolen by an off-beat press and can no longer
    /// be played. Views use this to know which beats are lost.
    /// </summary>
    public bool IsBeatLocked(int beat) {
        return beat >= lockedFromBeat && beat < lockedUntilBeat;
    }

    /// <summary>
    /// Called by a consumer when it sees its own input. Returns true and claims
    /// the current beat's action slot if the press landed within beatTolerance
    /// of the nearest beat and the slot wasn't already claimed. Returns false
    /// for presses that are too early/late, land on a penalized beat, or repeat
    /// an already-claimed beat. An off-beat press also penalizes upcoming beats.
    /// </summary>
    public bool TryConsumeBeat() {
        if (currentBeatConsumed) return false;
        if (BeatManager.Instance == null || !BeatManager.Instance.IsPlaying) return false;

        // Checked before timing so a penalty can't spiral: presses made during a
        // lockout are ignored outright rather than stacking more penalties.
        if (IsBeatLocked(currentOpenBeat)) {
            if (logActions) Debug.Log($"Beat {currentOpenBeat}: locked out by penalty");
            return false;
        }

        double beatPosition = BeatManager.Instance.CurrentSongTime / BeatManager.Instance.SecPerBeat;

        // Negative while the press is ahead of the beat, positive once behind it.
        double offsetFromBeat = beatPosition - currentOpenBeat;

        if (offsetFromBeat < -earlyTolerance || offsetFromBeat > lateTolerance) {
            if (logActions) {
                string side = offsetFromBeat < 0 ? "early" : "late";
                Debug.Log($"Beat {currentOpenBeat}: press rejected, {Math.Abs(offsetFromBeat):F2} beats {side} " +
                          $"(window -{earlyTolerance:F2} to +{lateTolerance:F2})");
            }

            ApplyPenalty(beatPosition);
            return false;
        }

        currentBeatConsumed = true;

        if (logActions) Debug.Log($"Beat {currentOpenBeat}: action consumed");

        OnBeatConsumed?.Invoke(currentOpenBeat);

        return true;
    }

    /// <summary>
    /// Steals the next penaltyBeats beats whose window has not opened yet. Starting
    /// at the next unopened window (rather than always at the current beat) keeps
    /// the punishment the same size whether the press was early or late - an early
    /// press loses the beat it jumped, a late one loses the beats still ahead.
    /// </summary>
    private void ApplyPenalty(double beatPosition) {
        if (penaltyBeats <= 0) return;

        // If the current beat's window has already opened, it is spent either way,
        // so the penalty starts from the following beat instead.
        int firstLocked = beatPosition > currentOpenBeat - earlyTolerance
            ? currentOpenBeat + 1
            : currentOpenBeat;

        lockedFromBeat = firstLocked;
        lockedUntilBeat = firstLocked + penaltyBeats;

        if (logActions) Debug.Log($"Penalty: beats {firstLocked} to {lockedUntilBeat - 1} stolen");

        OnBeatsPenalized?.Invoke(firstLocked, penaltyBeats);
    }

    /// <summary>
    /// Rounds a moment in time to its nearest beat number, so a press is always
    /// judged against the beat it is closest to. This is only attribution - which
    /// beat a press belongs to - not the accept window itself. The window is the
    /// narrower earlyTolerance/lateTolerance range around that beat, and the gap
    /// between the two is the dead zone where presses are rejected.
    /// </summary>
    private int NearestBeat(double songTime) {
        return (int)Math.Round(songTime / BeatManager.Instance.SecPerBeat, MidpointRounding.AwayFromZero);
    }
}
