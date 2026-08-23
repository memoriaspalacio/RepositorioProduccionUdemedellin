using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The input judgement system for the beat-synced rhythm mechanic. Reads its
/// notes from a SongChart, converts each note's authored Measure:Beat into an
/// absolute time via ChartTiming, and checks key presses against those times -
/// reporting Perfect / Hit / Miss via OnNoteJudged.
///
/// Notes don't need arming ahead of time: on every key press, this scans
/// forward from the earliest unresolved note for the closest match within the
/// hit window. That's what lets an early press register just as correctly as
/// a late one, with no beat-by-beat lookahead step required.
/// </summary>
public class InputJudge : MonoBehaviour {

    [Header("Lane Setup")]
    [Tooltip("One key per beat in the 4-beat loop, in order. Index 0-2 = dings, index 3 = clap. " +
             "Must match the order of the circles in NoteFeedbackUI.")]
    [SerializeField] private Key[] noteKeys = { Key.W, Key.A, Key.S, Key.D };

    [Header("Judgement Windows (seconds, each side of the expected beat)")]
    [SerializeField] private double perfectWindowSeconds = 0.05;
    [SerializeField] private double hitWindowSeconds = 0.12;

    [Header("Debug")]
    [SerializeField] private bool logJudgements = true;

    /// <summary>Fired whenever a note is judged: (note, result).</summary>
    public event Action<NoteData, JudgementResult> OnNoteJudged;

    private struct ScheduledNote {
        public NoteData data;
        public double expectedTime;
        public bool judged;
    }

    private ScheduledNote[] scheduledNotes;
    private HashSet<Key> usedKeys;
    private int expiryScanIndex;

    private void Start() {
        if (BeatManager.Instance == null) {
            Debug.LogError("InputJudge: No BeatManager found in the scene.");
            enabled = false;
            return;
        }

        if (songChart == null) {
            Debug.LogError("InputJudge: No SongChart assigned.");
            enabled = false;
            return;
        }

        BuildSchedule();
        BeatManager.Instance.StartMusic();
    }

    /// <summary>
    /// Converts every note's Measure:Beat into an absolute time once, up
    /// front, and sorts by that time. This is the only place tempo enters the
    /// picture - everything after this works purely in seconds.
    /// </summary>
    private void BuildSchedule() {
        IReadOnlyList<NoteData> notes = songChart.Notes;
        scheduledNotes = new ScheduledNote[notes.Count];
        usedKeys = new HashSet<Key>();

        for (int i = 0; i < notes.Count; i++) {
            NoteData note = notes[i];
            double time = ChartTiming.ToSeconds(
                note.Measure, note.Beat,
                BeatManager.Instance.SecPerBeat, BeatManager.Instance.BeatsPerMeasure);

            scheduledNotes[i] = new ScheduledNote { data = note, expectedTime = time, judged = false };
            usedKeys.Add(note.Key);
        }

        Array.Sort(scheduledNotes, (a, b) => a.expectedTime.CompareTo(b.expectedTime));
        expiryScanIndex = 0;
    }

    private void Update() {
        if (BeatManager.Instance == null || !BeatManager.Instance.IsPlaying) return;

        CheckForInput();
        CheckForExpiredNotes();
    }

    private void CheckForInput() {
        if (Keyboard.current == null) return;

        for (int i = 0; i < noteKeys.Length; i++) {
            if (Keyboard.current[noteKeys[i]].wasPressedThisFrame) {
                HandlePress();
                break;
            }
        }
    }
    private void HandlePress()
    {
        double currentTime = BeatManager.Instance.CurrentSongTime;

        for (int i = 0; i < pendingNotes.Length; i++)
        {
            ref PendingNote note = ref pendingNotes[i];

            if (!note.active || note.judged)
                continue;

            double delta = Math.Abs(currentTime - note.expectedTime);

            if (delta <= perfectWindowSeconds)
            {
                Judge(i, JudgementResult.Perfect);
                return;
            }

            if (delta <= hitWindowSeconds)
            {
                Judge(i, JudgementResult.Hit);
                return;
            }
        }
    }
    /*private void HandlePress(int laneIndex) {
        ref PendingNote note = ref pendingNotes[laneIndex];
        if (!note.active || note.judged) return; // nothing due on this key right now

        for (int i = expiryScanIndex; i < scheduledNotes.Length; i++) {
            ref ScheduledNote note = ref scheduledNotes[i];
            if (note.expectedTime - now > hitWindowSeconds) break; // sorted by time - nothing further is in range

            if (note.judged || note.data.Key != key) continue;

            double delta = Math.Abs(now - note.expectedTime);
            if (delta <= hitWindowSeconds && delta < bestDelta) {
                bestDelta = delta;
                bestIndex = i;
            }
        }
        // Outside the hit window entirely -> ignore. The note isn't judged yet,
        // so a too-early press just doesn't count; the player can still press
        // again once the window actually opens. Expiry below handles true misses.
    }*/

    private void CheckForExpiredNotes() {
        double now = BeatManager.Instance.CurrentSongTime;

        while (expiryScanIndex < scheduledNotes.Length) {
            ref ScheduledNote note = ref scheduledNotes[expiryScanIndex];

            if (!note.judged) {
                if (now - note.expectedTime > hitWindowSeconds) {
                    Judge(expiryScanIndex, JudgementResult.Miss);
                } else {
                    break; // earliest unresolved note isn't expired yet
                }
            }

            expiryScanIndex++;
        }
    }

    private void Judge(int index, JudgementResult result) {
        scheduledNotes[index].judged = true;
        NoteData data = scheduledNotes[index].data;

        if (logJudgements) {
            Debug.Log($"{data.Key} @ M{data.Measure}:B{data.Beat}: {result}");
        }

        OnNoteJudged?.Invoke(data, result);
    }
}