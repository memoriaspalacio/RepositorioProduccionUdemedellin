/// <summary>
/// Why a note was destroyed. Shared between BeatActionJudge (which decides) and
/// any view or scoring system that wants to react differently depending on
/// which happened.
/// </summary>
public enum DestroyReason {
    /// <summary>The note's window closed without ever being consumed - nobody tried.</summary>
    Expired,

    /// <summary>The player whiffed on a different beat, and this upcoming note was destroyed as the penalty.</summary>
    Penalized
}
