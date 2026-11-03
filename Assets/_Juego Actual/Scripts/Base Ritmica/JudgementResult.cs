using UnityEngine;

/// <summary>
/// How well a press landed on its note. BeatActionJudge grades every hit by how far it
/// was from the exact beat (see perfectWindowSeconds / goodWindowSeconds) and fires
/// OnBeatGraded; the UI shows the grade (EfectosCalificacion) and the heart reacts to
/// it (HeartBeatFeedback). Miss is a note that slipped past or a press with nothing to hit.
/// </summary>
public enum JudgementResult {
    Miss,
    Bad,
    Good,
    Perfect
}

/// <summary>
/// One color per grade, shared by everything that shows a grade - the zones on the track,
/// the notes, the heart's glow and the grade text - so a color always means the same thing.
/// </summary>
public static class ColoresCalificacion {
    public static readonly Color Perfect = new Color(1f, 0.82f, 0.28f, 1f);
    public static readonly Color Good = new Color(0.42f, 0.95f, 0.68f, 1f);
    public static readonly Color Bad = new Color(0.66f, 0.6f, 0.95f, 1f);
    public static readonly Color Miss = new Color(0.95f, 0.25f, 0.3f, 1f);

    public static Color De(JudgementResult resultado) {
        switch (resultado) {
            case JudgementResult.Perfect: return Perfect;
            case JudgementResult.Good: return Good;
            case JudgementResult.Bad: return Bad;
            default: return Miss;
        }
    }
}
