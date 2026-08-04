/// <summary>
/// Converts a chart's authored Measure:Beat position into an absolute time in
/// seconds. This is the one seam between how charts are authored (matching
/// your DAW's ruler) and how the game judges timing (real elapsed seconds).
/// If tempo changes ever need supporting, this is the only place that logic
/// would need to live - nothing in the chart or the judgement system itself
/// would need to change.
/// </summary>
public static class ChartTiming {

    /// <summary>
    /// Converts a Measure:Beat position (both 1-indexed, matching a typical
    /// DAW's display) into seconds since the song started, at a constant
    /// tempo. Measure 1, Beat 1 lines up with BeatManager's first triggered
    /// beat - not with time zero, since BeatManager itself doesn't fire its
    /// first beat until one full beat after the song starts.
    /// </summary>
    public static double ToSeconds(int measure, float beat, float secPerBeat, int beatsPerMeasure) {
        float totalBeats = (measure - 1) * beatsPerMeasure + beat;
        return totalBeats * secPerBeat;
    }
}
