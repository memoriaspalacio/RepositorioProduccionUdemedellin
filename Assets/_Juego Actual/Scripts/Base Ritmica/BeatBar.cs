using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One bar travelling toward the heart, representing a single beat's action slot.
/// Purely a view: it holds which beat it stands for and lets BeatTrackUI drive its
/// position and color. It never reads input or decides anything about timing.
/// </summary>
public class BeatBar : MonoBehaviour {

    /// <summary>The beat number this bar arrives at the heart on.</summary>
    public int TargetBeat { get; private set; }

    /// <summary>True once the player successfully claimed this bar's beat.</summary>
    public bool Consumed { get; private set; }

    private RectTransform rect;
    private Image image;

    private void Awake() {
        CacheComponents();
    }

    private void CacheComponents() {
        if (rect == null) rect = GetComponent<RectTransform>();
        if (image == null) image = GetComponent<Image>();
    }

    /// <summary>Prepares this bar (fresh or reused from the pool) for a new beat.</summary>
    public void Initialize(int targetBeat) {
        CacheComponents();

        TargetBeat = targetBeat;
        Consumed = false;
    }

    /// <summary>Marks this bar as successfully hit, so it renders in the success color.</summary>
    public void MarkConsumed() {
        Consumed = true;
    }

    public void SetAnchoredPosition(Vector2 position) {
        CacheComponents();

        if (rect != null) rect.anchoredPosition = position;
    }

    public void SetColor(Color color) {
        CacheComponents();

        if (image != null) image.color = color;
    }
}
