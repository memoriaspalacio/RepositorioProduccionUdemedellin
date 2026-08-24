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
/// action was on time.
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

    [Header("Timing")]
    [Tooltip("How many beats a bar takes to travel from the spawn point to the heart.")]
    [SerializeField, Min(1f)] private float travelBeats = 4f;

    [Tooltip("How many beats a bar keeps travelling past the heart before it despawns.")]
    [SerializeField, Min(0.1f)] private float despawnBeats = 1f;

    [Header("Colors")]
    [Tooltip("Before the beat's window opens.")]
    [SerializeField] private Color idleColor = new Color(0.42f, 0.17f, 0.17f, 1f);

    [Tooltip("The moment the window opens.")]
    [SerializeField] private Color windowOpenColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Tooltip("The moment the window closes - the bar degrades to this across the window.")]
    [SerializeField] private Color windowClosingColor = new Color(0.6f, 0.2f, 0.15f, 1f);

    [Tooltip("A beat the player successfully hit.")]
    [SerializeField] private Color successColor = new Color(0.95f, 0.3f, 0.25f, 1f);

    private readonly List<BeatBar> activeBars = new List<BeatBar>();
    private readonly Queue<BeatBar> pool = new Queue<BeatBar>();

    private int nextBeatToSpawn;
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
        beatJudge.OnBeatsPenalized += HandleBeatsPenalized;
    }

    private void OnDisable() {
        if (beatJudge == null) return;

        beatJudge.OnBeatConsumed -= HandleBeatConsumed;
        beatJudge.OnBeatsPenalized -= HandleBeatsPenalized;
    }

    private void Update() {
        if (BeatManager.Instance == null || !BeatManager.Instance.IsPlaying) return;
        if (barPrefab == null || spawnPoint == null || heartPoint == null) return;

        float nowBeat = (float)(BeatManager.Instance.CurrentSongTime / BeatManager.Instance.SecPerBeat);

        // On the first playing frame, fill the track so it is already populated
        // instead of ramping up over the first travelBeats beats.
        if (!trackStarted) {
            nextBeatToSpawn = Mathf.CeilToInt(nowBeat);
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
            nextBeatToSpawn++;
        }
    }

    private void SpawnBar(int targetBeat) {
        // A penalized beat has no opportunity to show, so it gets no bar at all.
        if (beatJudge.IsBeatLocked(targetBeat)) return;

        BeatBar bar;

        if (pool.Count > 0) {
            bar = pool.Dequeue();
            bar.gameObject.SetActive(true);

        } else {
            GameObject instance = Instantiate(barPrefab, barParent != null ? barParent : transform);

            bar = instance.GetComponent<BeatBar>();
            if (bar == null) bar = instance.AddComponent<BeatBar>();
        }

        bar.Initialize(targetBeat);
        activeBars.Add(bar);
    }

    private void UpdateBars(float nowBeat) {
        Vector2 spawnPosition = spawnPoint.anchoredPosition;
        Vector2 heartPosition = heartPoint.anchoredPosition;

        float earlyTolerance = beatJudge.EarlyTolerance;
        float lateTolerance = beatJudge.LateTolerance;

        for (int i = activeBars.Count - 1; i >= 0; i--) {
            BeatBar bar = activeBars[i];

            // How far past its own beat this bar is: negative while approaching,
            // zero exactly on the beat, positive once past the heart.
            float beatsPastTarget = nowBeat - bar.TargetBeat;

            if (beatsPastTarget > despawnBeats) {
                Recycle(bar, i);
                continue;
            }

            // Travel progress runs 0 at spawn to 1 at the heart, and keeps going
            // past 1 so a bar carries on through instead of stopping on the heart.
            float progress = 1f + (beatsPastTarget / travelBeats);

            bar.SetAnchoredPosition(
                Vector2.LerpUnclamped(spawnPosition, heartPosition, progress)
            );

            bar.SetColor(ResolveColor(bar, beatsPastTarget, earlyTolerance, lateTolerance));
        }
    }

    /// <summary>
    /// Picks a bar's color from timing alone: neutral until its window opens, then
    /// degrading from open to closing color across the window, or the success color
    /// once the player has claimed it. The window is asymmetric - it opens
    /// earlyTolerance before the beat and closes lateTolerance after it - so the
    /// highlight has to be read from the judge rather than assumed symmetric.
    /// </summary>
    private Color ResolveColor(BeatBar bar, float beatsPastTarget, float earlyTolerance, float lateTolerance) {
        if (bar.Consumed) return successColor;

        if (beatsPastTarget < -earlyTolerance || beatsPastTarget > lateTolerance) return idleColor;

        // 0 as the window opens, 1 as it closes.
        float windowProgress = (beatsPastTarget + earlyTolerance) / (earlyTolerance + lateTolerance);

        return Color.Lerp(windowOpenColor, windowClosingColor, windowProgress);
    }

    private void HandleBeatConsumed(int beat) {
        BeatBar bar = FindActiveBar(beat);

        if (bar != null) bar.MarkConsumed();
    }

    /// <summary>
    /// Removes the bars for beats the judge just stole, so the player sees the
    /// opportunities they lost disappear from the track.
    /// </summary>
    private void HandleBeatsPenalized(int firstBeat, int count) {
        for (int i = activeBars.Count - 1; i >= 0; i--) {
            int targetBeat = activeBars[i].TargetBeat;

            if (targetBeat >= firstBeat && targetBeat < firstBeat + count)
                Recycle(activeBars[i], i);
        }
    }

    private BeatBar FindActiveBar(int beat) {
        for (int i = 0; i < activeBars.Count; i++) {
            if (activeBars[i].TargetBeat == beat) return activeBars[i];
        }

        return null;
    }

    private void Recycle(BeatBar bar, int index) {
        activeBars.RemoveAt(index);

        bar.gameObject.SetActive(false);
        pool.Enqueue(bar);
    }
}
