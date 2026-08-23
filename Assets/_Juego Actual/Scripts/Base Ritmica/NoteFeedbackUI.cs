using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Flashes one of several UI circles based on judgement results coming from
/// InputJudge. Green = Hit, Gold = Perfect, Red = Miss. Purely cosmetic - all
/// the actual timing logic lives in InputJudge, this just reacts to it.
/// </summary>
public class NoteFeedbackUI : MonoBehaviour {

    [Header("Wiring")]
    [SerializeField] private InputJudge inputJudge;

    [Tooltip("Which key each circle represents, in the same order as Circles.")]
    [SerializeField] private Key[] noteKeys = { Key.D, Key.F, Key.J, Key.K };

    [Tooltip("One circle per key, in the same order as Note Keys.")]
    [SerializeField] private Image[] circles = new Image[4];

    [Header("Colors")]
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color hitColor = Color.green;
    [SerializeField] private Color perfectColor = new Color(1f, 0.84f, 0f); // gold
    [SerializeField] private Color missColor = Color.red;

    [Header("Timing")]
    [SerializeField] private float flashDuration = 0.15f;

    private Coroutine[] flashRoutines;

    private void Awake() {
        flashRoutines = new Coroutine[circles.Length];
    }

    private void OnEnable() {
        if (inputJudge != null) {
            inputJudge.OnNoteJudged += HandleJudged;
        }

        foreach (Image circle in circles) {
            if (circle != null) circle.color = idleColor;
        }
    }

    private void OnDisable() {
        if (inputJudge != null) {
            inputJudge.OnNoteJudged -= HandleJudged;
        }
    }

    private void HandleJudged(NoteData note, JudgementResult result) {
        int laneIndex = Array.IndexOf(noteKeys, note.Key);
        if (laneIndex < 0 || laneIndex >= circles.Length || circles[laneIndex] == null) return;

        if (flashRoutines[laneIndex] != null) {
            StopCoroutine(flashRoutines[laneIndex]);
        }

        flashRoutines[laneIndex] = StartCoroutine(Flash(laneIndex, ColorFor(result)));
    }

    private IEnumerator Flash(int laneIndex, Color color) {
        circles[laneIndex].color = color;
        yield return new WaitForSeconds(flashDuration);
        circles[laneIndex].color = idleColor;
        flashRoutines[laneIndex] = null;
    }

    private Color ColorFor(JudgementResult result) {
        switch (result) {
            case JudgementResult.Perfect: return perfectColor;
            case JudgementResult.Hit: return hitColor;
            default: return missColor;
        }
    }
}