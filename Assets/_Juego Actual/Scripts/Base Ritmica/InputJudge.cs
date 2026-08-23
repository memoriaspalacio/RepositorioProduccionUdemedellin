using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Prototype input judgement system for the beat-synced rhythm mechanic.
///
/// Listens to BeatManager for timing and checks key presses against a fixed
/// 4-beat test pattern (ding, ding, ding, clap - one required key per beat),
/// reporting Perfect / Hit / Miss via OnNoteJudged.
///
/// This is deliberately hardcoded to a 4-note loop so we can validate the core
/// timing math (windows, early/late detection, expiry) in isolation before
/// building the real chart-driven note spawner. The judging logic here
/// (PendingNote, Judge(), the window checks) is the part that carries forward
/// largely unchanged once notes come from a real chart instead of a fixed loop.
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

    /// <summary>Fired whenever a note is judged: (laneIndex, result).</summary>
    public event Action<int, JudgementResult> OnNoteJudged;

    private struct PendingNote {
        public bool active;
        public double expectedTime;
        public bool judged;
    }

    private PendingNote[] pendingNotes;

    private void Awake() {
        pendingNotes = new PendingNote[noteKeys.Length];
    }

    private void Start() {
        if (BeatManager.Instance == null) {
            Debug.LogError("InputJudge: No BeatManager found in the scene.");
            enabled = false;
            return;
        }

        BeatManager.Instance.OnBeat.AddListener(HandleBeat);
        BeatManager.Instance.StartMusic();

        // Arm the very first note by hand. Every note after this gets armed one
        // beat ahead of time by HandleBeat below (so its window opens before
        // it's due) - but there's no "beat before beat 1" to do that for us.
        pendingNotes[0] = new PendingNote {
            active = true,
            expectedTime = BeatManager.Instance.SecPerBeat,
            judged = false
        };
    }

    private void OnDisable() {
        if (BeatManager.Instance != null) {
            BeatManager.Instance.OnBeat.RemoveListener(HandleBeat);
        }
    }

    /// <summary>
    /// Fires every beat. Arms the *next* beat's note early, so its hit window
    /// is already open before it's actually due - this previews how the real
    /// chart spawner will look ahead and pre-arm upcoming notes.
    /// </summary>
    private void HandleBeat() {
        int nextIdx = BeatManager.Instance.CurrentBeat % noteKeys.Length;
        pendingNotes[nextIdx] = new PendingNote {
            active = true,
            expectedTime = BeatManager.Instance.CurrentSongTime + BeatManager.Instance.SecPerBeat,
            judged = false
        };
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

        double delta = Math.Abs(BeatManager.Instance.CurrentSongTime - note.expectedTime);

        if (delta <= perfectWindowSeconds) {
            Judge(laneIndex, JudgementResult.Perfect);
        } else if (delta <= hitWindowSeconds) {
            Judge(laneIndex, JudgementResult.Hit);
        }
        // Outside the hit window entirely -> ignore. The note isn't judged yet,
        // so a too-early press just doesn't count; the player can still press
        // again once the window actually opens. Expiry below handles true misses.
    }*/

    private void CheckForExpiredNotes() {
        for (int i = 0; i < pendingNotes.Length; i++) {
            ref PendingNote note = ref pendingNotes[i];
            if (!note.active || note.judged) continue;

            double delta = BeatManager.Instance.CurrentSongTime - note.expectedTime;
            if (delta > hitWindowSeconds) {
                Judge(i, JudgementResult.Miss);
            }
        }
    }

    private void Judge(int laneIndex, JudgementResult result) {
        pendingNotes[laneIndex].judged = true;

        if (logJudgements) {
            Debug.Log($"Lane {laneIndex} ({noteKeys[laneIndex]}): {result}");
        }

        OnNoteJudged?.Invoke(laneIndex, result);
    }
}