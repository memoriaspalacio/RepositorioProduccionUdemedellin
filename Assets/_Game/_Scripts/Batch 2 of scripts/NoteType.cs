/// <summary>
/// The category of a note in the chart. Determines whether hitting it just
/// scores points (Normal) or also feeds into the cinematic fight system
/// (PowerChord) via NoteData.PowerChordId.
/// </summary>
public enum NoteType {
    Normal,
    PowerChord
}
