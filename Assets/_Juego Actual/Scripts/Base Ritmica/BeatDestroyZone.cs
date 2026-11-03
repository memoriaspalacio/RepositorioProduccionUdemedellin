using UnityEngine;

/// <summary>
/// Lets a designer define where a note gets DESTROYED by dragging/resizing an
/// invisible RectTransform box, positioned after the window zone. Reads the
/// box's left edge every frame, converts it into beats using the same
/// spawn-to-heart travel speed BeatTrackUI's bars move at, and pushes the
/// result into BeatActionJudge as lateTolerance. Only the box's left edge
/// matters - the right edge is unused, kept only so the box is a visible,
/// draggable rectangle in the Scene view.
///
/// A note that reaches this zone without being consumed is destroyed and its
/// window force-closed - see BeatActionJudge.OnNoteDestroyed. Purely
/// positional: no player input is involved in this half of the system.
///
/// Deliberately separate from BeatWindowZone (which sets earlyTolerance): this
/// decouples "how late can you still succeed" from "how far right a note
/// travels before it's removed" - widening one no longer drags the other along.
///
/// Runs before other scripts each frame (DefaultExecutionOrder) so nothing
/// reads a stale tolerance the same frame the box moves.
/// </summary>
[DefaultExecutionOrder(-100)]
public class BeatDestroyZone : MonoBehaviour {

    [Header("References")]
    [SerializeField] private BeatActionJudge beatJudge;
    [SerializeField] private BeatTrackUI beatTrackUI;

    [Tooltip("The invisible box marking where an unconsumed note is destroyed. " +
             "Only its left edge matters.")]
    [SerializeField] private RectTransform destroyBox;

    private void OnEnable() {
        if (beatJudge == null)
            beatJudge = FindFirstObjectByType<BeatActionJudge>();

        if (beatTrackUI == null)
            beatTrackUI = FindFirstObjectByType<BeatTrackUI>();

        if (beatJudge == null || beatTrackUI == null || destroyBox == null) {
            Debug.LogError("BeatDestroyZone: missing a required reference.", this);
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
        if (travelDistanceX <= 0f) return;

        float beatsPerUnit = beatTrackUI.TravelBeats / travelDistanceX;

        float heartX = heartPoint.anchoredPosition.x;
        float boxLeftX = destroyBox.anchoredPosition.x + destroyBox.rect.xMin;

        float lateTolerance = (boxLeftX - heartX) * beatsPerUnit;

        beatJudge.SetLateTolerance(lateTolerance);
    }
}
