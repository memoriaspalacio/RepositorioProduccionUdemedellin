using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// The Conductor - Single source of truth for all beat-synced timing in the game.
/// Everything listens to this instead of tracking time independently.
/// </summary>
public class BeatManager : MonoBehaviour {
    #region Singleton
    public static BeatManager Instance { get; private set; }

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    [Header("Music Settings")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private float bpm = 280f; // Your song's BPM - adjust after confirming!
    [SerializeField] private int beatsPerMeasure = 4; // Most songs are 4/4 time

    [Header("Speed Control (Accessibility)")]
    [SerializeField, Range(0.5f, 1f)] private float speedMultiplier = 1f; // 0.5x to 1x speed

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool visualMetronome = false; // Shows a flashing indicator on beats

    // Events - Other systems subscribe to these
    public UnityEvent OnBeat;           // Fires every beat
    public UnityEvent OnHalfBeat;       // Fires twice per beat (for half-time events)
    public UnityEvent OnMeasure;        // Fires every measure (4 beats usually)
    public UnityEvent<int> OnVerse;     // Fires every X measures with verse number

    // Public properties for other systems to read
    public int CurrentBeat { get; private set; }
    public int CurrentMeasure { get; private set; }
    public float SecPerBeat => 60f / (bpm * speedMultiplier);
    public bool IsPlaying => musicSource != null && musicSource.isPlaying;

    /// <summary>How many beats make up one measure. Exposed so other systems
    /// (like the chart's Measure:Beat conversion) don't have to hardcode a
    /// number that could drift out of sync with this one.</summary>
    public int BeatsPerMeasure => beatsPerMeasure;

    /// <summary>
    /// Continuous time in seconds since the song started, driven off AudioSettings.dspTime
    /// so it won't drift the way Update()-accumulated time can. Everything that needs to
    /// judge "how close was this to the beat" should read from this instead of tracking
    /// its own clock.
    /// </summary>
    public double CurrentSongTime => IsPlaying ? AudioSettings.dspTime - songStartDspTime : 0.0;

    // Internal timing variables
    private double nextBeatTime;
    private double nextHalfBeatTime;
    private int lastBeatTriggered = -1;
    private int lastHalfBeatTriggered = -1;
    private int verseLengthInMeasures = 2; // How many measures before a "verse" event
    private double songStartDspTime; // dspTime when the song actually started, used for CurrentSongTime

    private IEnumerator Start() {
        if (musicSource == null) {
            Debug.LogError("BeatManager: No AudioSource assigned! Please assign your music source.");

        } else {
            // Initialize timing
            nextBeatTime = AudioSettings.dspTime;
            nextHalfBeatTime = AudioSettings.dspTime;

            // Apply speed multiplier to audio
            musicSource.pitch = speedMultiplier;

            if (showDebugLogs) {
                Debug.Log($"BeatManager initialized | BPM: {bpm} | Speed: {speedMultiplier}x | Sec/Beat: {SecPerBeat:F3}");
            }
            if (musicSource != null) musicSource.Play();
            yield return null;
            if (musicSource == null) musicSource.Pause();
        }
    }

    bool myFix = true;

    private void Update() {
        if (!IsPlaying) return;

        double currentTime = AudioSettings.dspTime;

        // Check for half-beats (fires twice per beat)
        if (currentTime >= nextHalfBeatTime) {
            TriggerHalfBeat();
            nextHalfBeatTime += SecPerBeat / 2.0;

            if (myFix) {
                TriggerBeat();
                myFix = false;
            } else myFix = true;
        }
    }

    private void TriggerBeat() {
        CurrentBeat++;

        if (showDebugLogs) {
            Debug.Log($"♪ BEAT {CurrentBeat} | Time: {AudioSettings.dspTime:F2}");
        }

        OnBeat?.Invoke();

        // Check for measure
        if (CurrentBeat % beatsPerMeasure == 1) {
            CurrentMeasure++;
            OnMeasure?.Invoke();

            if (showDebugLogs) {
                Debug.Log($"═══ MEASURE {CurrentMeasure} ═══");
            }

            // Check for verse
            if (CurrentMeasure % verseLengthInMeasures == 1) {
                int verseNumber = CurrentMeasure / verseLengthInMeasures;
                OnVerse?.Invoke(verseNumber);

                if (showDebugLogs) {
                    Debug.Log($"▓▓▓ VERSE {verseNumber + 1} ▓▓▓");
                }
            }
        }

        // Visual metronome for debugging
        if (visualMetronome) {
            FlashMetronome();
        }
    }

    private void TriggerHalfBeat() {
        OnHalfBeat?.Invoke();
    }

    /// <summary>
    /// Returns progress through the current beat (0.0 to 1.0).
    /// Useful for interpolating animations between beats.
    /// </summary>
    public float GetBeatProgress() {
        if (!IsPlaying) return 0f;

        double timeSinceLastBeat = AudioSettings.dspTime - (nextBeatTime - SecPerBeat);
        float progress = (float)(timeSinceLastBeat / SecPerBeat);
        return Mathf.Clamp01(progress);
    }

    /// <summary>
    /// Starts the music from the beginning.
    /// </summary>
    public void StartMusic() {
        if (musicSource == null) return;

        CurrentBeat = 0;
        CurrentMeasure = 0;
        songStartDspTime = AudioSettings.dspTime;
        nextBeatTime = AudioSettings.dspTime + SecPerBeat;
        nextHalfBeatTime = AudioSettings.dspTime + (SecPerBeat / 2.0);

        musicSource.Play();

        if (showDebugLogs) {
            Debug.Log("🎵 Music Started!");
        }
    }

    /// <summary>
    /// Stops the music.
    /// </summary>
    public void StopMusic() {
        if (musicSource != null) {
            musicSource.Stop();
        }
    }

    /// <summary>
    /// Changes the speed multiplier (for accessibility slider).
    /// Affects both music pitch and all beat timing.
    /// </summary>
    public void SetSpeed(float newSpeed) {
        speedMultiplier = Mathf.Clamp(newSpeed, 0.5f, 1f);

        if (musicSource != null) {
            musicSource.pitch = speedMultiplier;
        }

        if (showDebugLogs) {
            Debug.Log($"Speed changed to {speedMultiplier:F2}x");
        }
    }

    /// <summary>
    /// Visual feedback for debugging - flashes the screen slightly on beat.
    /// Attach a UI Image to see this work.
    /// </summary>
    private void FlashMetronome() {
        // EVERYTHING: Flash a UI element or screen overlay
        // For now, just changes background color briefly
        Camera.main.backgroundColor = CurrentBeat % beatsPerMeasure == 0
            ? Color.red   // Downbeat (first beat of measure)
            : Color.white; // Regular beat

        Invoke(nameof(ResetMetronome), 0.05f);
    }

    private void ResetMetronome() {
        Camera.main.backgroundColor = Color.black;
    }

    #region Public Utility Methods

    /// <summary>
    /// Returns the beat number at a specific time offset from now.
    /// Useful for scheduling events X beats in the future.
    /// </summary>
    public int GetBeatAtTime(float secondsFromNow) {
        return CurrentBeat + Mathf.FloorToInt(secondsFromNow / SecPerBeat);
    }

    /// <summary>
    /// Converts a number of beats into seconds.
    /// </summary>
    public float BeatsToSeconds(float beats) {
        return beats * SecPerBeat;
    }

    /// <summary>
    /// Converts seconds into beats.
    /// </summary>
    public float SecondsToBeats(float seconds) {
        return seconds / SecPerBeat;
    }

    #endregion

    // Editor helper - allows changing speed in play mode
    private void OnValidate() {
        if (Application.isPlaying && musicSource != null) {
            musicSource.pitch = speedMultiplier;
        }
    }
}