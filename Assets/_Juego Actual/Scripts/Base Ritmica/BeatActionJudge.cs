using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which upcoming beats' action slots are open and available. Necrodancer-
/// style: no accuracy grading, no knowledge of input devices or action types -
/// consumers poll their own input and call TryConsumeBeat() when they see a press.
///
/// Early and late tolerance are set independently by two separate zones (typically
/// BeatWindowZone and BeatDestroyZone) rather than being tied to a single box, so
/// "how late can you still succeed" and "how far a note travels before it's removed"
/// can be tuned without dragging each other along.
///
/// Because tolerances are independent and can add up to more than a full beat, more
/// than one beat's window can be open at the same time. A successful press always
/// consumes the OLDEST open beat - the one closest to being destroyed - never a
/// newer one. A press with nothing open is a whiff: it destroys the next
/// penaltyNoteCount upcoming beats that haven't opened yet, oldest first.
/// </summary>
public class BeatActionJudge : MonoBehaviour {

    [Header("Timing")]
    [Tooltip("How far BEFORE the beat, in fractions of a beat, the window opens. " +
             "Normally driven by BeatWindowZone; this slider is the fallback default.")]
    [SerializeField, Range(0.05f, 2f)] private float earlyTolerance = 1f / 3f;

    [Tooltip("How far AFTER the beat a note survives before it's destroyed. " +
             "Normally driven by BeatDestroyZone; this slider is the fallback default.")]
    [SerializeField, Range(0.05f, 2f)] private float lateTolerance = 0.25f;

    [Header("Penalty")]
    [Tooltip("How many upcoming notes are destroyed when the player whiffs (presses with nothing open).")]
    [SerializeField, Min(0)] private int penaltyNoteCount = 2;

    [Header("Debug")]
    [SerializeField] private bool logActions = true;

    /// <summary>Fired when a beat's action slot is successfully claimed. Carries the beat number.</summary>
    public event Action<int> OnBeatConsumed;

    /// <summary>
    /// Fired when a note is destroyed without being consumed - either its window
    /// closed on its own (Expired), or it was sacrificed to a whiff penalty
    /// (Penalized). Carries the beat number and which happened.
    /// </summary>
    public event Action<int, DestroyReason> OnNoteDestroyed;

    /// <summary>How far before the beat the window opens, in beats. Read by views that draw the window.</summary>
    public float EarlyTolerance => earlyTolerance;

    /// <summary>How far after the beat a note survives, in beats. Read by views that draw the window.</summary>
    public float LateTolerance => lateTolerance;

    private struct TrackedBeat {
        public int beat;
        public bool resolved; // consumed or destroyed - either way, done
    }

    // Ascending, contiguous run of beat numbers from the oldest one still
    // unresolved (or not yet pruned) through the newest one currently relevant.
    private readonly List<TrackedBeat> tracked = new List<TrackedBeat>();
    private bool initialized;

    /// <summary>Overrides how far before the beat the window opens. See earlyTolerance.</summary>
    public void SetEarlyTolerance(float value) {
        earlyTolerance = Mathf.Max(0f, value);
    }

    /// <summary>Overrides how far after the beat a note survives. See lateTolerance.</summary>
    public void SetLateTolerance(float value) {
        lateTolerance = Mathf.Max(0f, value);
    }

    /// <summary>
    /// True if this beat has already been consumed or destroyed. Views use this to
    /// avoid spawning a note for a beat that's already been decided - e.g. by a
    /// penalty that landed before the note would otherwise have appeared.
    /// </summary>
    public bool IsBeatResolved(int beat) {
        if (tracked.Count == 0) return false;

        int index = beat - tracked[0].beat;
        if (index < 0 || index >= tracked.Count) return false;

        return tracked[index].resolved;
    }

    private void Update() {
        if (BeatManager.Instance == null || !BeatManager.Instance.IsPlaying) return;

        double beatPosition = BeatManager.Instance.CurrentSongTime / BeatManager.Instance.SecPerBeat;

        GrowTrackedRange(beatPosition);
        ExpireResolvedRange(beatPosition);
    }

    /// <summary>
    /// Extends the tracked range up to whatever beat is now close enough that its
    /// window could plausibly be open (or, for a penalty, be a target) - everything
    /// up to earlyTolerance beats ahead of now.
    /// </summary>
    private void GrowTrackedRange(double beatPosition) {
        int newestRelevant = Mathf.FloorToInt((float)(beatPosition + earlyTolerance));

        if (!initialized) {
            tracked.Add(new TrackedBeat { beat = newestRelevant, resolved = false });
            initialized = true;
            return;
        }

        int newestTracked = tracked[tracked.Count - 1].beat;
        for (int b = newestTracked + 1; b <= newestRelevant; b++) {
            tracked.Add(new TrackedBeat { beat = b, resolved = false });
        }
    }

    /// <summary>
    /// Removes beats from the front of the range once their window has fully
    /// closed, firing OnNoteDestroyed(Expired) for any that were never claimed.
    /// </summary>
    private void ExpireResolvedRange(double beatPosition) {
        while (tracked.Count > 0) {
            TrackedBeat oldest = tracked[0];

            if (beatPosition <= oldest.beat + lateTolerance) break; // not expired yet

            if (!oldest.resolved) {
                if (logActions) Debug.Log($"Beat {oldest.beat}: destroyed (expired)");
                OnNoteDestroyed?.Invoke(oldest.beat, DestroyReason.Expired);
            }

            tracked.RemoveAt(0);
        }
    }

    /// <summary>
    /// Called by a consumer when it sees its own input. Claims the OLDEST currently
    /// open, unresolved beat and returns true. If nothing is open, this is a whiff:
    /// it destroys the next penaltyNoteCount not-yet-open beats and returns false.
    /// </summary>
    public bool TryConsumeBeat() {
        if (BeatManager.Instance == null || !BeatManager.Instance.IsPlaying) return false;

        double beatPosition = BeatManager.Instance.CurrentSongTime / BeatManager.Instance.SecPerBeat;

        for (int i = 0; i < tracked.Count; i++) {
            TrackedBeat tb = tracked[i];
            if (tb.resolved) continue;

            double offset = beatPosition - tb.beat;
            if (offset < -earlyTolerance || offset > lateTolerance) continue;

            tb.resolved = true;
            tracked[i] = tb;

            if (logActions) Debug.Log($"Beat {tb.beat}: consumed");
            OnBeatConsumed?.Invoke(tb.beat);
            return true;
        }

        ApplyPenalty(beatPosition);
        return false;
    }

    /// <summary>
    /// Destroys the next penaltyNoteCount beats that haven't opened yet, oldest
    /// first, skipping any already resolved by an earlier penalty.
    /// </summary>
    private void ApplyPenalty(double beatPosition) {
        if (penaltyNoteCount <= 0) return;

        int candidate = Mathf.FloorToInt((float)(beatPosition + earlyTolerance)) + 1;
        int destroyed = 0;

        while (destroyed < penaltyNoteCount) {
            EnsureTracked(candidate);

            int index = candidate - tracked[0].beat;
            TrackedBeat tb = tracked[index];

            if (!tb.resolved) {
                tb.resolved = true;
                tracked[index] = tb;

                if (logActions) Debug.Log($"Beat {candidate}: destroyed (penalty)");
                OnNoteDestroyed?.Invoke(candidate, DestroyReason.Penalized);
                destroyed++;
            }

            candidate++;
        }
    }

    /// <summary>Grows the tracked range to include this specific beat, if it isn't already in it.</summary>
    private void EnsureTracked(int beat) {
        if (!initialized) {
            tracked.Add(new TrackedBeat { beat = beat, resolved = false });
            initialized = true;
            return;
        }

        int newestTracked = tracked[tracked.Count - 1].beat;
        for (int b = newestTracked + 1; b <= beat; b++) {
            tracked.Add(new TrackedBeat { beat = b, resolved = false });
        }
    }
}