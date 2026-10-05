using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which upcoming notes' action slots are open and available. Necrodancer-
/// style: no accuracy grading, no knowledge of input devices or action types -
/// consumers poll their own input and call TryConsumeBeat() when they see a press.
///
/// A note exists on every Nth beat (noteInterval), counting from the song's first
/// beat. The judge is the single authority on which beats are notes: BeatTrackUI
/// asks it, so what is drawn always matches what can actually be hit.
///
/// Early and late tolerance are set independently by two separate zones (typically
/// BeatWindowZone and BeatDestroyZone) rather than being tied to a single box, so
/// "how late can you still succeed" and "how far a note travels before it's removed"
/// can be tuned without dragging each other along.
///
/// Because tolerances are independent and can add up to more than a full beat, more
/// than one note's window can be open at the same time. A successful press always
/// consumes the OLDEST open note - the one closest to being destroyed - never a
/// newer one. A press with nothing open is a whiff: it destroys the next
/// penaltyNoteCount upcoming notes that haven't opened yet, oldest first.
///
/// Runs after the zones (which set tolerances) and before everything else, so
/// tolerances are fresh and the tracked range is current by the time a consumer
/// calls TryConsumeBeat().
/// </summary>
[DefaultExecutionOrder(-50)]
public class BeatActionJudge : MonoBehaviour {

    [Header("Notes")]
    [Tooltip("A note exists on every Nth beat, counting from the song's first beat " +
             "(beats 1, 1+N, 1+2N...). 1 = every beat, 2 = every other beat. BeatTrackUI " +
             "asks the judge which beats are notes, so what's drawn always matches what can be hit.")]
    [SerializeField, Min(1)] private int noteInterval = 2;

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

    /// <summary>Fired when a note's action slot is successfully claimed. Carries the beat number.</summary>
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

    /// <summary>Notes appear on every Nth beat. Read by views that draw the track.</summary>
    public int NoteInterval => noteInterval;

    private struct TrackedBeat {
        public int beat;
        public bool resolved; // consumed or destroyed - or never a note to begin with
    }

    // Contiguous, ascending run of beat numbers from the oldest one not yet pruned
    // up to (nextBeatToTrack - 1). Can legitimately be empty between notes, which
    // is why the next beat to add is its own counter rather than read off the end.
    private readonly List<TrackedBeat> tracked = new List<TrackedBeat>();
    private int nextBeatToTrack;
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
    /// True if this beat carries a note. Beats before the song's first beat never do,
    /// and with an interval above 1 only every Nth beat after it does.
    /// </summary>
    public bool IsNoteBeat(int beat) {
        int interval = Mathf.Max(1, noteInterval);
        return beat >= 1 && (beat - 1) % interval == 0;
    }

    /// <summary>
    /// True if this beat has already been consumed or destroyed. Views use this to
    /// avoid spawning a note for a beat that's already been decided - e.g. by a
    /// penalty that landed before the note would otherwise have appeared.
    /// </summary>
    public bool IsBeatResolved(int beat) {
        int index;
        return TryGetIndex(beat, out index) && tracked[index].resolved;
    }

    private bool TryGetIndex(int beat, out int index) {
        index = -1;
        if (tracked.Count == 0) return false;

        index = beat - tracked[0].beat;
        return index >= 0 && index < tracked.Count;
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
        if (!initialized) {
            // Start at the oldest beat whose window could still be open. Normally
            // that is beat 1 (there is no beat 0 to hit); it only differs if the
            // judge wakes up partway through a song.
            nextBeatToTrack = Mathf.Max(1, Mathf.CeilToInt((float)(beatPosition - lateTolerance)));
            initialized = true;
        }

        TrackUpTo(Mathf.FloorToInt((float)(beatPosition + earlyTolerance)));
    }

    /// <summary>Adds every beat up to and including this one that isn't tracked yet.</summary>
    private void TrackUpTo(int beat) {
        while (nextBeatToTrack <= beat) {
            tracked.Add(new TrackedBeat {
                beat = nextBeatToTrack,
                // A beat that isn't a note starts out "resolved", so every scan below
                // (consume, expire, penalty) skips it without any special cases.
                resolved = !IsNoteBeat(nextBeatToTrack)
            });

            nextBeatToTrack++;
        }
    }

    /// <summary>
    /// Removes beats from the front of the range once their window has fully
    /// closed, firing OnNoteDestroyed(Expired) for any note that was never claimed.
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
    /// open, unresolved note and returns true. If nothing is open, this is a whiff:
    /// it destroys the next penaltyNoteCount not-yet-open notes and returns false.
    /// </summary>
    public bool TryConsumeBeat() {
        if (!initialized) return false;
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
    /// Destroys the next penaltyNoteCount notes that haven't opened yet, oldest
    /// first. Beats that aren't notes, and notes an earlier penalty already took,
    /// are skipped rather than counted.
    /// </summary>
    private void ApplyPenalty(double beatPosition) {
        if (penaltyNoteCount <= 0) return;

        int candidate = Mathf.FloorToInt((float)(beatPosition + earlyTolerance)) + 1;
        int destroyed = 0;

        while (destroyed < penaltyNoteCount) {
            TrackUpTo(candidate);

            int index;
            if (TryGetIndex(candidate, out index) && !tracked[index].resolved) {
                TrackedBeat tb = tracked[index];
                tb.resolved = true;
                tracked[index] = tb;

                if (logActions) Debug.Log($"Beat {candidate}: destroyed (penalty)");
                OnNoteDestroyed?.Invoke(candidate, DestroyReason.Penalized);
                destroyed++;
            }

            candidate++;
        }
    }
}