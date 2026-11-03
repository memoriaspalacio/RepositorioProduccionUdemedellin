using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A hand-made note chart: the exact moments a song's notes reach the heart, written as
/// Measure:Beat:Tick (all 1-based, tick = 16th note) by the web chart editor in
/// Herramientas/EditorCharts. BeatActionJudge plays it instead of its noteInterval
/// pattern whenever the scene has one.
///
/// Every time is when the note must be HIT - when it sounds in the song - never when it
/// spawns: BeatTrackUI already spawns each bar travelBeats ahead of its beat.
///
/// Charts live in Resources/Charts, named after the scene that plays them
/// (Charts/cementerio.json), so deploying a new version from the editor is just
/// overwriting that file - no scene has to be touched.
/// </summary>
public class NoteChart {

    /// <summary>Resources subfolder the judge looks in for a chart named after its scene.</summary>
    public const string ResourcesFolder = "Charts";

    // Mirrors the JSON the editor writes; field names must match it exactly.
    [Serializable]
    private class ChartFile {
        public string song;
        public string scene;
        public float bpm;
        public int beatsPerMeasure = 4;
        public int ticksPerBeat = 4;
        public float offsetMs;
        public Anchor[] anclas;
        public string[] notes;
    }

    // Where the beat really falls in one stretch of a song whose timing wanders: from this
    // beat on, beat N sits offsetMs into its own beat instead of the chart's offsetMs.
    [Serializable]
    private class Anchor {
        public float beat;
        public float offsetMs;
    }

    /// <summary>Asset the chart was read from, for logs.</summary>
    public string Name { get; private set; }

    /// <summary>Tempo the chart was written for. Should match the scene's BeatManager.</summary>
    public float Bpm { get; private set; }

    /// <summary>Where the chart's 1:1:1 sits inside the audio file, in seconds.</summary>
    public double OffsetSeconds { get; private set; }

    /// <summary>
    /// Optional ("anclas" in the JSON): for songs that weren't played to a click, beats where
    /// the music sits at a different offset than OffsetSeconds. BeatManager slides the beat
    /// grid between them so the notes follow the music. Null when the chart has none.
    /// Ascending, same length as AnchorOffsetSeconds.
    /// </summary>
    public double[] AnchorBeats { get; private set; }

    /// <summary>The offset at each of AnchorBeats, in seconds of audio (like OffsetSeconds).</summary>
    public double[] AnchorOffsetSeconds { get; private set; }

    public int NoteCount => slots.Count;

    /// <summary>Judge slot of the chart's first note.</summary>
    public int FirstSlot { get; private set; }

    private readonly HashSet<int> slots = new HashSet<int>();

    private NoteChart() { }

    /// <summary>True if a note lands on this judge slot (slot 0 is 1:1:1).</summary>
    public bool HasNoteAt(int slot) {
        return slots.Contains(slot);
    }

    /// <summary>Loads Resources/Charts/&lt;sceneName&gt;, or returns null if that scene has no chart.</summary>
    public static NoteChart LoadForScene(string sceneName, int slotsPerBeat) {
        TextAsset file = Resources.Load<TextAsset>(ResourcesFolder + "/" + sceneName);
        return file != null ? Parse(file, slotsPerBeat) : null;
    }

    /// <summary>
    /// Reads a chart file into judge slots (slotsPerBeat per beat). Returns null - so the
    /// caller falls back to its pattern - if the file can't be read or has no valid note.
    /// A malformed note is skipped with a warning instead of failing the whole chart.
    /// </summary>
    public static NoteChart Parse(TextAsset file, int slotsPerBeat) {
        ChartFile data;

        try {
            data = JsonUtility.FromJson<ChartFile>(file.text);
        } catch (Exception e) {
            Debug.LogError($"NoteChart: {file.name} is not valid JSON ({e.Message}).", file);
            return null;
        }

        if (data == null || data.notes == null || data.beatsPerMeasure < 1 || data.ticksPerBeat < 1) {
            Debug.LogError($"NoteChart: {file.name} is missing its notes or its beatsPerMeasure / ticksPerBeat.", file);
            return null;
        }

        if (slotsPerBeat % data.ticksPerBeat != 0) {
            Debug.LogError($"NoteChart: {file.name} uses {data.ticksPerBeat} ticks per beat, which doesn't fit " +
                           $"the judge's {slotsPerBeat} slots per beat.", file);
            return null;
        }

        NoteChart chart = new NoteChart {
            Name = file.name,
            Bpm = data.bpm,
            OffsetSeconds = Math.Max(0f, data.offsetMs) / 1000.0
        };

        int slotsPerTick = slotsPerBeat / data.ticksPerBeat;
        int skipped = 0;
        chart.FirstSlot = int.MaxValue;

        foreach (string entry in data.notes) {
            int tick;
            if (TryParseTick(entry, data.beatsPerMeasure, data.ticksPerBeat, out tick)) {
                chart.slots.Add(tick * slotsPerTick);
                chart.FirstSlot = Math.Min(chart.FirstSlot, tick * slotsPerTick);
            } else {
                skipped++;
            }
        }

        if (skipped > 0) {
            Debug.LogWarning($"NoteChart: skipped {skipped} note(s) in {file.name} that aren't Measure:Beat:Tick " +
                             $"inside a bar of {data.beatsPerMeasure} beats and {data.ticksPerBeat} ticks.", file);
        }

        if (chart.slots.Count == 0) {
            Debug.LogWarning($"NoteChart: {file.name} has no valid notes.", file);
            return null;
        }

        ReadAnchors(chart, data, file);
        return chart;
    }

    private static void ReadAnchors(NoteChart chart, ChartFile data, TextAsset file) {
        if (data.anclas == null || data.anclas.Length == 0) return;

        List<Anchor> anchors = new List<Anchor>(data.anclas);
        anchors.RemoveAll(a => a == null || a.beat < 0f);
        anchors.Sort((a, b) => a.beat.CompareTo(b.beat));

        if (anchors.Count == 0) {
            Debug.LogWarning($"NoteChart: {file.name} has an empty \"anclas\" list.", file);
            return;
        }

        chart.AnchorBeats = new double[anchors.Count];
        chart.AnchorOffsetSeconds = new double[anchors.Count];

        for (int i = 0; i < anchors.Count; i++) {
            chart.AnchorBeats[i] = anchors[i].beat;
            chart.AnchorOffsetSeconds[i] = Math.Max(0f, anchors[i].offsetMs) / 1000.0;
        }
    }

    /// <summary>"12:3:2" -> ticks since 1:1:1.</summary>
    private static bool TryParseTick(string entry, int beatsPerMeasure, int ticksPerBeat, out int tick) {
        tick = 0;
        if (string.IsNullOrEmpty(entry)) return false;

        string[] parts = entry.Split(':');
        if (parts.Length != 3) return false;

        int measure, beat, t;
        if (!int.TryParse(parts[0].Trim(), out measure) || measure < 1) return false;
        if (!int.TryParse(parts[1].Trim(), out beat) || beat < 1 || beat > beatsPerMeasure) return false;
        if (!int.TryParse(parts[2].Trim(), out t) || t < 1 || t > ticksPerBeat) return false;

        tick = ((measure - 1) * beatsPerMeasure + (beat - 1)) * ticksPerBeat + (t - 1);
        return true;
    }
}
