/// <summary>
/// Why a note was destroyed. Shared between BeatActionJudge (which decides) and
/// any view or scoring system that wants to react differently depending on
/// which happened.
/// </summary>
public enum DestroyReason {
    /// <summary>The note's window closed without ever being consumed - nobody tried.</summary>
    Expired,

    /// <summary>
    /// No longer fired: penalties now cost time instead of notes (see
    /// BeatActionJudge.OnPenaltyApplied). Kept so other branches that still
    /// compare against it keep compiling.
    /// </summary>
    Penalized
}
