using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which upcoming notes' action slots are open and available. Necrodancer-
/// style: no accuracy grading, no knowledge of input devices or action types -
/// consumers poll their own input and call TryConsumeBeat() when they see a press.
///
/// NORMAL notes sit on every Nth beat (noteInterval), counting from the song's first
/// beat. On top of those, two optional random EXTRA notes can follow each normal note:
/// one in the middle of the interval, one half a beat later. The judge is the single
/// authority on which beats carry a note: BeatTrackUI asks it, so what is drawn always
/// matches what can actually be hit.
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

    // Notes can sit on half beats, so internally the judge counts in half-beat
    // "slots": slot 2 is beat 1, slot 3 is beat 1.5, slot 4 is beat 2, and so on.
    // Everything public (events, queries) still speaks in beats.
    private const int SlotsPerBeat = 2;
    private const int FirstNoteSlot = SlotsPerBeat; // beat 1 - there is no beat 0 to hit

    [Header("Notes")]
    [Tooltip("A normal note exists on every Nth beat, counting from the song's first beat " +
             "(beats 1, 1+N, 1+2N...). 1 = every beat, 2 = every other beat. BeatTrackUI " +
             "asks the judge which beats are notes, so what's drawn always matches what can be hit. " +
             "Set it before pressing Play - changing it mid-song leaves notes already in flight inconsistent.")]
    [SerializeField, Min(1)] private int noteInterval = 1;

    [Header("Extra notes (random)")]
    [Tooltip("Chance, 0 to 1, that a normal note is followed by an extra note in the middle of the " +
             "interval - the whole beat halfway to the next normal note (beat 2 when notes are on 1 and 3; " +
             "two beats after a note when the interval is 4). Rolled once per normal note. " +
             "Needs an interval of 2 or more. Safe to change while playing: it only affects notes not yet rolled.")]
    [SerializeField, Range(0f, 1f)] private float midBeatNoteChance = 0.0937f;

    [Tooltip("Chance, 0 to 1, that a normal note is followed by an extra note half a beat later. " +
             "Rolled once per normal note. Safe to change while playing: it only affects notes not yet rolled.")]
    [SerializeField, Range(0f, 1f)] private float halfBeatNoteChance = 0.0468f;

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

    /// <summary>Fired when a note's action slot is successfully claimed. Carries the note's beat (e.g. 3 or 3.5).</summary>
    public event Action<float> OnBeatConsumed;

    /// <summary>
    /// Fired when a note is destroyed without being consumed - either its window
    /// closed on its own (Expired), or it was sacrificed to a whiff penalty
    /// (Penalized). Carries the note's beat (e.g. 3 or 3.5) and which happened.
    /// </summary>
    public event Action<float, DestroyReason> OnNoteDestroyed;

    /// <summary>How far before the beat the window opens, in beats. Read by views that draw the window.</summary>
    public float EarlyTolerance => earlyTolerance;

    /// <summary>How far after the beat a note survives, in beats. Read by views that draw the window.</summary>
    public float LateTolerance => lateTolerance;

    /// <summary>Normal notes appear on every Nth beat. Read by views that draw the track.</summary>
    public int NoteInterval => noteInterval;

    private struct TrackedNote {
        public int slot;
        public bool resolved; // consumed or destroyed - or never a note to begin with
    }

    private struct ExtraRoll {
        public bool mid;
        public bool half;
    }

    // Contiguous, ascending run of slots from the oldest one not yet pruned up to
    // (nextSlotToTrack - 1). Can legitimately be empty between notes, which is why
    // the next slot to add is its own counter rather than read off the end.
    private readonly List<TrackedNote> tracked = new List<TrackedNote>();
    private int nextSlotToTrack;
    private bool initialized;

    // The dice are rolled once per normal note, the first time anyone asks about
    // its extras, and remembered - so the judge and the view always agree, and
    // changing a chance mid-song only affects notes not yet rolled.
    private readonly Dictionary<int, ExtraRoll> extraRolls = new Dictionary<int, ExtraRoll>();
    private System.Random rng;

    /// <summary>Overrides how far before the beat the window opens. See earlyTolerance.</summary>
    public void SetEarlyTolerance(float value) {
        earlyTolerance = Mathf.Max(0f, value);
    }

    /// <summary>Overrides how far after the beat a note survives. See lateTolerance.</summary>
    public void SetLateTolerance(float value) {
        lateTolerance = Mathf.Max(0f, value);
    }

    /// <summary>
    /// True if a note sits on this beat - a normal one, or an extra that came up.
    /// Notes only ever sit on whole or half beats, and never before the first beat.
    /// </summary>
    public bool IsNoteBeat(float beat) {
        float scaled = beat * SlotsPerBeat;
        int slot = Mathf.RoundToInt(scaled);

        if (Mathf.Abs(scaled - slot) > 0.001f) return false;

        return IsNoteSlot(slot);
    }

    /// <summary>
    /// True if the note on this beat has already been consumed or destroyed. Views use
    /// this to avoid spawning a note that's already been decided - e.g. by a penalty
    /// that landed before the note would otherwise have appeared.
    /// </summary>
    public bool IsBeatResolved(float beat) {
        int index;
        return TryGetIndex(Mathf.RoundToInt(beat * SlotsPerBeat), out index) && tracked[index].resolved;
    }

    private static float BeatOfSlot(int slot) {
        return slot / (float)SlotsPerBeat;
    }

    private bool TryGetIndex(int slot, out int index) {
        index = -1;
        if (tracked.Count == 0) return false;

        index = slot - tracked[0].slot;
        return index >= 0 && index < tracked.Count;
    }

    private static bool IsNormalSlot(int slot, int interval) {
        if (slot < FirstNoteSlot || slot % SlotsPerBeat != 0) return false;

        return (slot / SlotsPerBeat - 1) % interval == 0;
    }

    private bool IsNoteSlot(int slot) {
        int interval = Mathf.Max(1, noteInterval);

        if (IsNormalSlot(slot, interval)) return true;

        // An extra half a beat after a normal note.
        if (IsNormalSlot(slot - 1, interval)) return RollFor(slot - 1).half;

        // An extra in the middle of the interval. An interval of 1 has no gap for one.
        int midOffsetSlots = interval >= 2 ? Mathf.Max(1, interval / 2) * SlotsPerBeat : 0;
        if (midOffsetSlots > 0 && IsNormalSlot(slot - midOffsetSlots, interval)) {
            return RollFor(slot - midOffsetSlots).mid;
        }

        return false;
    }

    private ExtraRoll RollFor(int normalSlot) {
        ExtraRoll roll;

        if (!extraRolls.TryGetValue(normalSlot, out roll)) {
            if (rng == null) rng = new System.Random();

            roll.mid = midBeatNoteChance > 0f && rng.NextDouble() < midBeatNoteChance;
            roll.half = halfBeatNoteChance > 0f && rng.NextDouble() < halfBeatNoteChance;

            extraRolls[normalSlot] = roll;
        }

        return roll;
    }

    private void Update() {
        if (BeatManager.Instance == null || !BeatManager.Instance.IsPlaying) return;

        double beatPosition = BeatManager.Instance.CurrentSongTime / BeatManager.Instance.SecPerBeat;

        GrowTrackedRange(beatPosition);
        ExpireResolvedRange(beatPosition);
    }

    /// <summary>
    /// Extends the tracked range up to whatever slot is now close enough that its
    /// window could plausibly be open (or, for a penalty, be a target) - everything
    /// up to earlyTolerance beats ahead of now.
    /// </summary>
    private void GrowTrackedRange(double beatPosition) {
        if (!initialized) {
            // Start at the oldest slot whose window could still be open. Normally that
            // is the first beat; it only differs if the judge wakes up partway through a song.
            nextSlotToTrack = Mathf.Max(FirstNoteSlot,
                Mathf.CeilToInt((float)((beatPosition - lateTolerance) * SlotsPerBeat)));
            initialized = true;
        }

        TrackUpTo(Mathf.FloorToInt((float)((beatPosition + earlyTolerance) * SlotsPerBeat)));
    }

    /// <summary>Adds every slot up to and including this one that isn't tracked yet.</summary>
    private void TrackUpTo(int slot) {
        while (nextSlotToTrack <= slot) {
            tracked.Add(new TrackedNote {
                slot = nextSlotToTrack,
                // A slot that isn't a note starts out "resolved", so every scan below
                // (consume, expire, penalty) skips it without any special cases.
                resolved = !IsNoteSlot(nextSlotToTrack)
            });

            nextSlotToTrack++;
        }
    }

    /// <summary>
    /// Removes slots from the front of the range once their window has fully
    /// closed, firing OnNoteDestroyed(Expired) for any note that was never claimed.
    /// </summary>
    private void ExpireResolvedRange(double beatPosition) {
        while (tracked.Count > 0) {
            TrackedNote oldest = tracked[0];

            if (beatPosition <= BeatOfSlot(oldest.slot) + lateTolerance) break; // not expired yet

            if (!oldest.resolved) {
                if (logActions) Debug.Log($"Beat {BeatOfSlot(oldest.slot)}: destroyed (expired)");
                OnNoteDestroyed?.Invoke(BeatOfSlot(oldest.slot), DestroyReason.Expired);
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
            TrackedNote tn = tracked[i];
            if (tn.resolved) continue;

            double offset = beatPosition - BeatOfSlot(tn.slot);
            if (offset < -earlyTolerance || offset > lateTolerance) continue;

            tn.resolved = true;
            tracked[i] = tn;

            if (logActions) Debug.Log($"Beat {BeatOfSlot(tn.slot)}: consumed");
            OnBeatConsumed?.Invoke(BeatOfSlot(tn.slot));
            return true;
        }

        ApplyPenalty(beatPosition);
        return false;
    }

    /// <summary>
    /// Destroys the next penaltyNoteCount notes that haven't opened yet, oldest
    /// first. Slots that aren't notes, and notes an earlier penalty already took,
    /// are skipped rather than counted.
    /// </summary>
    private void ApplyPenalty(double beatPosition) {
        if (penaltyNoteCount <= 0) return;

        int candidate = Mathf.FloorToInt((float)((beatPosition + earlyTolerance) * SlotsPerBeat)) + 1;
        int destroyed = 0;

        while (destroyed < penaltyNoteCount) {
            TrackUpTo(candidate);

            int index;
            if (TryGetIndex(candidate, out index) && !tracked[index].resolved) {
                TrackedNote tn = tracked[index];
                tn.resolved = true;
                tracked[index] = tn;

                if (logActions) Debug.Log($"Beat {BeatOfSlot(candidate)}: destroyed (penalty)");
                OnNoteDestroyed?.Invoke(BeatOfSlot(candidate), DestroyReason.Penalized);
                destroyed++;
            }

            candidate++;
        }
    }
}