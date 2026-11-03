using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws the metronome track: a bar per beat travelling from the spawn point to
/// the heart, arriving exactly on its beat.
///
/// Every position here is derived from BeatManager's clock, never from physics or
/// spatial checks - a bar's distance from the heart is just how many beats away its
/// beat is, so the visual can never drift out of sync with BeatActionJudge. Travel
/// time is measured in beats rather than seconds so the accessibility speed slider
/// stays consistent.
///
/// This is a pure view. It reacts to the judge's events and never decides whether an
/// action was on time, or when a note is destroyed - BeatActionJudge owns both.
/// </summary>
public class BeatTrackUI : MonoBehaviour {

    [Header("References")]
    [SerializeField] private BeatActionJudge beatJudge;

    [Tooltip("Where bars appear. Must share a parent with heartPoint and barParent.")]
    [SerializeField] private RectTransform spawnPoint;

    [Tooltip("Where bars arrive exactly on the beat.")]
    [SerializeField] private RectTransform heartPoint;

    [Tooltip("Prefab holding an Image. A BeatBar component is added if missing.")]
    [SerializeField] private GameObject barPrefab;

    [Tooltip("Container the bars are parented to.")]
    [SerializeField] private RectTransform barParent;

    /// <summary>Read by BeatWindowZone/BeatDestroyZone to convert their box edges into beats.</summary>
    public RectTransform SpawnPoint => spawnPoint;

    /// <summary>Read by BeatWindowZone/BeatDestroyZone to convert their box edges into beats.</summary>
    public RectTransform HeartPoint => heartPoint;

    [Header("Timing")]
    [Tooltip("How many beats a bar takes to travel from the spawn point to the heart.")]
    [SerializeField, Min(1f)] private float travelBeats = 4f;

    /// <summary>Read by BeatWindowZone/BeatDestroyZone to convert their box edges into beats.</summary>
    public float TravelBeats => travelBeats;

    [Header("Mirror")]
    [Tooltip("Every note travels in from both sides at once and the two copies meet at the heart. " +
             "The second side is the spawn point mirrored across the heart, so it needs no setup of its own. " +
             "BeatWindowZone / BeatDestroyZone are still measured on the original side.")]
    [SerializeField] private bool mirrorTrack = true;

    /// <summary>Read by BeatTrackLane to draw the second lane.</summary>
    public bool MirrorTrack => mirrorTrack;

    [Header("Colors")]
    [Tooltip("Before the beat's window opens. Inside the window a note takes the color of the grade a press " +
             "would get right then (ColoresCalificacion): BAD, then GOOD, then PERFECT on the beat.")]
    [SerializeField] private Color idleColor = new Color(0.42f, 0.17f, 0.17f, 1f);

    [Tooltip("How many beats a note takes to blend from idleColor into the BAD color as the window opens.")]
    [SerializeField, Min(0f)] private float colorBlendBeats = 0.15f;

    [Tooltip("A beat the player successfully hit.")]
    [SerializeField] private Color successColor = new Color(0.95f, 0.3f, 0.25f, 1f);

    private readonly List<BeatBar> activeBars = new List<BeatBar>();
    private readonly Queue<BeatBar> pool = new Queue<BeatBar>();

    // Notes can sit on any 16th, so this steps in the judge's slots (quarter beats)
    // rather than whole beats.
    private float nextBeatToSpawn;
    private bool trackStarted;

    private void OnEnable() {
        if (beatJudge == null)
            beatJudge = FindFirstObjectByType<BeatActionJudge>();

        if (beatJudge == null) {
            Debug.LogError("BeatTrackUI: no BeatActionJudge found in the scene.", this);

            enabled = false;
            return;
        }

        beatJudge.OnBeatConsumed += HandleBeatConsumed;
        beatJudge.OnNoteDestroyed += HandleNoteDestroyed;
    }

    private void OnDisable() {
        if (beatJudge == null) return;

        beatJudge.OnBeatConsumed -= HandleBeatConsumed;
        beatJudge.OnNoteDestroyed -= HandleNoteDestroyed;
    }

    private void Update() {
        if (BeatManager.Instance == null || !BeatManager.Instance.IsPlaying) return;
        if (barPrefab == null || spawnPoint == null || heartPoint == null) return;

        float nowBeat = (float)(BeatManager.Instance.CurrentSongTime / BeatManager.Instance.SecPerBeat);

        // On the first playing frame, fill the track so it is already populated
        // instead of ramping up over the first travelBeats beats.
        if (!trackStarted) {
            nextBeatToSpawn = Mathf.CeilToInt(nowBeat * BeatActionJudge.SlotsPerBeat) / (float)BeatActionJudge.SlotsPerBeat;
            trackStarted = true;
        }

        SpawnDueBars(nowBeat);
        UpdateBars(nowBeat);
    }

    /// <summary>
    /// Spawns every bar whose beat is now close enough to be in flight. A bar for
    /// beat N must appear travelBeats before N to arrive on time.
    /// </summary>
    private void SpawnDueBars(float nowBeat) {
        int furthestVisibleBeat = Mathf.CeilToInt(nowBeat + travelBeats);

        while (nextBeatToSpawn <= furthestVisibleBeat) {
            SpawnBar(nextBeatToSpawn);
            nextBeatToSpawn += 1f / BeatActionJudge.SlotsPerBeat;
        }
    }

    private void SpawnBar(float targetBeat) {
        // Only beats the judge treats as notes get a bar, so what's drawn always
        // matches what can actually be hit.
        if (!beatJudge.IsNoteBeat(targetBeat)) return;

        // A note already resolved before its bar would appear has no opportunity
        // to show, so it gets no bar at all.
        if (beatJudge.IsBeatResolved(targetBeat)) return;

        CreateBar(targetBeat, false);
        if (mirrorTrack) CreateBar(targetBeat, true);
    }

    private void CreateBar(float targetBeat, bool mirrored) {
        BeatBar bar;

        if (pool.Count > 0) {
            bar = pool.Dequeue();
            bar.gameObject.SetActive(true);

        } else {
            GameObject instance = Instantiate(barPrefab, barParent != null ? barParent : transform);

            bar = instance.GetComponent<BeatBar>();
            if (bar == null) bar = instance.AddComponent<BeatBar>();
        }

        bar.Initialize(targetBeat, mirrored);
        activeBars.Add(bar);
    }

    private void UpdateBars(float nowBeat) {
        Vector2 spawnPosition = spawnPoint.anchoredPosition;
        Vector2 heartPosition = heartPoint.anchoredPosition;

        // The mirrored side starts just as far from the heart, on the other side of it.
        Vector2 mirroredSpawnPosition = new Vector2(2f * heartPosition.x - spawnPosition.x, spawnPosition.y);

        float earlyTolerance = beatJudge.EarlyTolerance;
        float lateTolerance = beatJudge.LateTolerance;

        for (int i = activeBars.Count - 1; i >= 0; i--) {
            BeatBar bar = activeBars[i];

            // How far past its own beat this bar is: negative while approaching,
            // zero exactly on the beat, positive once past the heart.
            float beatsPastTarget = nowBeat - bar.TargetBeat;

            // Travel progress runs 0 at spawn to 1 at the heart, and keeps going
            // past 1 so a bar carries on through instead of stopping on the heart.
            // Destruction is entirely event-driven now (HandleNoteDestroyed) - this
            // method only ever moves and colors bars that are still alive.
            float progress = 1f + (beatsPastTarget / travelBeats);

            bar.SetAnchoredPosition(
                Vector2.LerpUnclamped(bar.Mirrored ? mirroredSpawnPosition : spawnPosition, heartPosition, progress)
            );

            bar.SetColor(ResolveColor(bar, beatsPastTarget, earlyTolerance, lateTolerance));
        }
    }

    /// <summary>
    /// Picks a bar's color from timing alone: neutral until its window opens, then the
    /// color of the grade a press would get at this moment (BAD far from the beat, GOOD
    /// closer, PERFECT on it), or the success color once the player has claimed it. The
    /// window is asymmetric - it opens earlyTolerance before the beat and closes
    /// lateTolerance after it - so it has to be read from the judge rather than assumed.
    /// </summary>
    private Color ResolveColor(BeatBar bar, float beatsPastTarget, float earlyTolerance, float lateTolerance) {
        if (bar.Consumed) return successColor;

        if (beatsPastTarget < -earlyTolerance || beatsPastTarget > lateTolerance) return idleColor;

        Color grade = ColoresCalificacion.De(beatJudge.Grade(beatsPastTarget));

        // Blend in as the window opens instead of popping
        float blend = colorBlendBeats > 0f ? Mathf.Clamp01((beatsPastTarget + earlyTolerance) / colorBlendBeats) : 1f;
        return Color.Lerp(idleColor, grade, blend);
    }

    private void HandleBeatConsumed(float beat) {
        RecycleBarsOf(beat);
    }

    /// <summary>
    /// Removes the bars for a beat the judge just destroyed because it expired. If no
    /// bar exists yet for this beat, SpawnBar's IsBeatResolved check will simply skip
    /// spawning it later.
    /// </summary>
    private void HandleNoteDestroyed(float beat, DestroyReason reason) {
        RecycleBarsOf(beat);
    }

    // A mirrored track has two bars per beat, so every match goes, not just the first.
    private void RecycleBarsOf(float beat) {
        for (int i = activeBars.Count - 1; i >= 0; i--) {
            if (Mathf.Approximately(activeBars[i].TargetBeat, beat)) {
                Recycle(activeBars[i], i);
            }
        }
    }

    private void Recycle(BeatBar bar, int index) {
        activeBars.RemoveAt(index);

        bar.gameObject.SetActive(false);
        pool.Enqueue(bar);
    }
}