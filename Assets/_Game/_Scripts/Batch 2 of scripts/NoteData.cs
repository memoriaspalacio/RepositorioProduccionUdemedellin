using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A single note in a song's chart. Timing is authored as Measure:Beat to
/// match your DAW's timeline - nothing here thinks in seconds or tempo.
/// Converting a note's Measure:Beat into an absolute time in seconds happens
/// once, elsewhere, when the chart gets loaded into the game.
/// </summary>
[System.Serializable]
public class NoteData {

    [SerializeField, HideInInspector] private string id = Guid.NewGuid().ToString();
    /// <summary>Stable identifier for this note, independent of its position in the list.</summary>
    public string Id => id;

    /// <summary>
    /// Assigns a brand new Id. Used by SongChart to repair notes that lost Id
    /// uniqueness (e.g. via Unity's "Duplicate Array Element").
    /// </summary>
    public void RegenerateId() {
        id = Guid.NewGuid().ToString();
    }

    [Header("Input")]
    [SerializeField] private Key key = Key.D;
    public Key Key => key;

    [Header("Category")]
    [SerializeField] private NoteType noteType = NoteType.Normal;
    public NoteType NoteType => noteType;

    [Tooltip("Only used when Note Type is Power Chord. Identifies which specific " +
             "cinematic event this note triggers, so the cinematic script knows " +
             "which one just got hit (or missed).")]
    [SerializeField] private string powerChordId = "";
    public string PowerChordId => powerChordId;

    [Header("Timing (matches your DAW's Measure:Beat ruler)")]
    [SerializeField] private int measure = 1;
    public int Measure => measure;

    [Tooltip("Can be fractional, e.g. 2.5 = halfway between beat 2 and beat 3.")]
    [SerializeField] private float beat = 1f;
    public float Beat => beat;

    [Header("Hold Note")]
    [SerializeField] private bool isHeld = false;
    public bool IsHeld => isHeld;

    [Tooltip("Only used when Is Held is true - when the key should be released.")]
    [SerializeField] private int holdEndMeasure = 1;
    public int HoldEndMeasure => holdEndMeasure;

    [SerializeField] private float holdEndBeat = 1f;
    public float HoldEndBeat => holdEndBeat;

    [Header("Visual")]
    [SerializeField] private Vector2 position = Vector2.zero;
    public Vector2 Position => position;

    [Header("Notes for your team")]
    [TextArea(2, 4)]
    [SerializeField] private string comment = "";
    public string Comment => comment;
}
