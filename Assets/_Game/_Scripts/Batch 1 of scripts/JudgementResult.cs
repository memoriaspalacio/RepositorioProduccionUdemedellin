/// <summary>
/// Result of comparing a key press against a note's expected timing.
/// Shared between the input judgement system, scoring, UI feedback, and any
/// other system that cares how well a note was hit (e.g. the power chord /
/// cinematic-fight trigger later on).
/// </summary>
public enum JudgementResult {
    Miss,
    Hit,
    Perfect
}
