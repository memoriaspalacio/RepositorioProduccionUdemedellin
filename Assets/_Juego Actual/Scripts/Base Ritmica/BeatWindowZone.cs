using UnityEngine;

/// <summary>
/// Lets a designer define when a note's window OPENS by dragging/resizing an
/// invisible RectTransform box. Reads the box's left edge every frame, converts
/// it into beats using the same spawn-to-heart travel speed BeatTrackUI's bars
/// move at, and pushes the result into BeatActionJudge as earlyTolerance. Only
/// the box's left edge matters - the right edge is unused, kept only so the box
/// is a visible, draggable rectangle in the Scene view.
///
/// Deliberately separate from BeatDestroyZone (which sets lateTolerance): this
/// decouples "how early can you still succeed" from "how far right a note
/// travels before it's destroyed" - widening one no longer drags the other along.
///
/// Runs before other scripts each frame (DefaultExecutionOrder) so nothing
/// reads a stale tolerance the same frame the box moves.
///
/// Assumes horizontal travel, left (spawn) to right (heart), matching the
/// current track layout. The box, the heart, and the spawn point must all
/// share the same parent as BeatTrackUI's bars for the edge positions to line
/// up correctly.
/// </summary>
[DefaultExecutionOrder(-100)]
public class BeatWindowZone : MonoBehaviour {

    [Header("References")]
    [SerializeField] private BeatActionJudge beatJudge;
    [SerializeField] private BeatTrackUI beatTrackUI;

    [Tooltip("The invisible box a designer drags/resizes to define when the window opens. " +
             "Only its left edge matters.")]
    [SerializeField] private RectTransform windowBox;

    private void OnEnable() {
        if (beatJudge == null)
            beatJudge = FindFirstObjectByType<BeatActionJudge>();

        if (beatTrackUI == null)
            beatTrackUI = FindFirstObjectByType<BeatTrackUI>();

        if (beatJudge == null || beatTrackUI == null || windowBox == null) {
            Debug.LogError("BeatWindowZone: missing a required reference.", this);
            enabled = false;
        }
    }

    private void Update() {
        ApplyBoxToTolerance();
    }

    private void ApplyBoxToTolerance() {
        RectTransform spawnPoint = beatTrackUI.SpawnPoint;
        RectTransform heartPoint = beatTrackUI.HeartPoint;

        if (spawnPoint == null || heartPoint == null) return;

        float travelDistanceX = heartPoint.anchoredPosition.x - spawnPoint.anchoredPosition.x;
        if (travelDistanceX <= 0f) return; // spawn must sit to the left of the heart

        float beatsPerUnit = beatTrackUI.TravelBeats / travelDistanceX;

        float heartX = heartPoint.anchoredPosition.x;
        float boxLeftX = windowBox.anchoredPosition.x + windowBox.rect.xMin;

        float earlyTolerance = (heartX - boxLeftX) * beatsPerUnit;

        beatJudge.SetEarlyTolerance(earlyTolerance);
    }
}