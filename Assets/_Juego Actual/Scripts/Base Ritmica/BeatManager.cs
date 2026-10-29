using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// The Conductor - Single source of truth for all beat-synced timing in the game.
/// Everything listens to this instead of tracking time independently.
///
/// The clock only means anything if it starts when the song really does. The songs are
/// imported without "Preload Audio Data", so their audio isn't in memory when the scene
/// starts and decoding one takes a second or two: a plain Play() would start that late
/// while the clock was already counting. So the song is loaded first and then started
/// with PlayScheduled at a known dspTime, and the clock is measured from that moment.
/// The clock also keeps an eye on where the audio really is and re-anchors itself if the
/// two ever drift apart (an editor pause, for instance).
///
/// A looping song is repeated here rather than by AudioSource.loop: each repeat starts on
/// a whole number of beats (LoopLengthBeats), so the clock never jumps back and a note
/// chart simply plays again from the top.
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
        //DontDestroyOnLoad(gameObject);

        // Black from the very first frame, so the level never pops in (and the hitch of
        // loading the song happens out of sight).
        if (fadeFromBlack) startFade = FundidoInicio.Crear();
    }
    #endregion

    [Header("Music Settings")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private float bpm = 280f; // Your song's BPM - adjust after confirming!
    [SerializeField] private int beatsPerMeasure = 4; // Most songs are 4/4 time

    [Header("Speed Control (Accessibility)")]
    [SerializeField, Range(0.5f, 1f)] private float speedMultiplier = 1f; // 0.5x to 1x speed

    [Header("Sync")]
    [Tooltip("How long, in ms, between the audio being mixed and it coming out of the speakers. The clock is " +
             "pulled back by this much so notes reach the heart when the beat is HEARD. -1 = estimate it from " +
             "the DSP buffer size. If presses that sound on time keep counting as late, raise it; if notes look " +
             "late against the music, lower it.")]
    [SerializeField, Min(-1f)] private float outputLatencyMs = -1f;

    [Tooltip("Silence, in ms, that Unity's MP3 decoder leaves at the start of the song and browsers strip " +
             "(the encoder delay). Charts and their offsetMs are measured in the browser chart editor, so the " +
             "clock is pushed back by this much to match what Unity actually plays. Measured at 47 ms on the four " +
             "level songs; set it to 0 for a song that isn't an MP3.")]
    [SerializeField, Min(0f)] private float decoderPaddingMs = 47f;

    [Header("Level Start")]
    [Tooltip("Start the level on a black screen that fades in before the song begins. During the black screen " +
             "and the fade nothing can be played and the countdown (ControladorContrarreloj) is paused.")]
    [SerializeField] private bool fadeFromBlack = true;

    [Tooltip("Seconds the screen stays black once the song has loaded, before the fade starts.")]
    [SerializeField, Min(0f)] private float blackHoldSeconds = 0.4f;

    [Tooltip("Seconds the fade from black takes. The song starts when it ends.")]
    [SerializeField, Min(0f)] private float fadeSeconds = 1.2f;

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

    /// <summary>True from the moment the song is scheduled - including the pre-roll before
    /// it sounds and the short gap between loops - until it's stopped or ends.</summary>
    public bool IsPlaying => clockRunning;

    /// <summary>
    /// Beats from the start of one play of a looping song to the next, or 0 if the song
    /// doesn't loop (or hasn't started yet). A chart repeats every this many beats.
    /// </summary>
    public int LoopLengthBeats => loopBeats;

    /// <summary>How many beats make up one measure. Exposed so other systems
    /// (like the chart's Measure:Beat conversion) don't have to hardcode a
    /// number that could drift out of sync with this one.</summary>
    public int BeatsPerMeasure => beatsPerMeasure;

    /// <summary>The song's tempo as set here, before the speed multiplier.</summary>
    public float Bpm => bpm;

    /// <summary>
    /// Continuous time in seconds since the song's first beat as it's heard, driven off
    /// AudioSettings.dspTime so it won't drift the way Update()-accumulated time can.
    /// Everything that needs to judge "how close was this to the beat" should read from
    /// this instead of tracking its own clock. Negative during the pre-roll (RequestPreRoll)
    /// and the song's own lead-in before its first beat (SetSongOffset).
    /// </summary>
    public double CurrentSongTime => IsPlaying
        ? AudioSettings.dspTime - songStartDspTime - LeadInSeconds - outputLatencySeconds
        : 0.0;

    /// <summary>
    /// Asks for the clock to start at least this many beats before beat 0, so notes due at
    /// the very start of a chart travel the whole track instead of popping in near the
    /// heart. Only takes effect if called before the song starts (any Start() will do).
    /// </summary>
    public void RequestPreRoll(float beats) {
        preRollBeats = Mathf.Max(preRollBeats, beats);
    }

    /// <summary>
    /// Where beat 0 sits inside the audio file, in seconds of audio. A note chart sets this
    /// from its offsetMs (measured in the chart editor) so the beat grid lines up with a song
    /// that has some lead-in before its first beat. 0 - the default - is the old behaviour.
    /// </summary>
    public void SetSongOffset(double seconds) {
        seconds = System.Math.Max(0.0, seconds);

        // Beat events run on their own schedule; move it along with the clock. If Start
        // hasn't run yet it sets the schedule itself, offset included.
        nextHalfBeatTime += (seconds - songOffsetSeconds) / speedMultiplier;
        songOffsetSeconds = seconds;
    }

    // Internal timing variables
    private double nextHalfBeatTime;
    private int lastBeatTriggered = -1;
    private int lastHalfBeatTriggered = -1;
    private int verseLengthInMeasures = 2; // How many measures before a "verse" event
    private double songStartDspTime; // dspTime the song's first sample plays at, used for CurrentSongTime
    private double songOffsetSeconds; // where beat 0 sits inside the audio file, see SetSongOffset

    // Real seconds from the song's first sample to beat 0: the chart's offset plus the
    // decoder's leading silence.
    private double LeadInSeconds => (songOffsetSeconds + decoderPaddingMs / 1000.0) / speedMultiplier;
    private double outputLatencySeconds; // see outputLatencyMs

    private const double MinScheduleAhead = 0.1; // the audio thread needs the start time a little in advance
    private const double RepeatScheduleAhead = 1.0; // how early the next loop is handed to the audio thread
    private const double ResyncThreshold = 0.06; // drift between clock and audio that triggers a re-anchor

    private bool clockRunning;
    private float preRollBeats;
    private bool loopSong; // the music source's own Loop flag, read once: looping is done here
    private int loopBeats;
    private double clipSeconds; // length of the song, in seconds of audio
    private int repeatsScheduled; // plays of the song handed to the audio thread so far

    // A looping song alternates between two sources so the next play can be scheduled
    // while the current one is still ringing out.
    private AudioSource[] voices;

    private double LoopSeconds => loopBeats * (double)SecPerBeat;

    private FundidoInicio startFade;

    private IEnumerator Start() {
        if (musicSource == null) {
            Debug.LogError("BeatManager: No AudioSource assigned! Please assign your music source.");
            if (startFade != null) Destroy(startFade.gameObject);
            yield break;
        }

        if (startFade != null) PauseCountdown();

        // Apply speed multiplier to audio
        musicSource.pitch = speedMultiplier;

        loopSong = musicSource.loop;
        musicSource.loop = false;

        AudioClip clip = musicSource.clip;

        if (clip != null && clip.loadState != AudioDataLoadState.Loaded) {
            clip.LoadAudioData();

            float waited = 0f;
            while (clip.loadState != AudioDataLoadState.Loaded && clip.loadState != AudioDataLoadState.Failed && waited < 15f) {
                waited += Time.unscaledDeltaTime;
                if (startFade != null) PauseCountdown();
                yield return null;
            }

            if (clip.loadState != AudioDataLoadState.Loaded)
                Debug.LogWarning($"BeatManager: {clip.name} didn't finish loading ({clip.loadState}); starting anyway.", this);
        }

        // One more frame: every other Start() (the judge's offset and pre-roll requests) has run,
        // and the heavy first frame of the scene is behind us.
        yield return null;

        // The margin before the level: hold the black screen, then fade it in. Every frame,
        // since the countdown's own Start() may run after ours and switch it back on.
        if (startFade != null) {
            for (float t = 0f; t < blackHoldSeconds; t += Time.unscaledDeltaTime) {
                PauseCountdown();
                yield return null;
            }

            IEnumerator fade = startFade.Desvanecer(fadeSeconds);
            while (fade.MoveNext()) {
                PauseCountdown();
                yield return fade.Current;
            }
        }

        if (showDebugLogs) {
            Debug.Log($"BeatManager initialized | BPM: {bpm} | Speed: {speedMultiplier}x | Sec/Beat: {SecPerBeat:F3}");
        }

        StartMusic();

        if (startFade != null && ControladorContrarreloj.Instancia != null)
            ControladorContrarreloj.Instancia.IniciarContrarreloj();
    }

    private static void PauseCountdown() {
        if (ControladorContrarreloj.Instancia != null)
            ControladorContrarreloj.Instancia.PausarContrarreloj();
    }

    bool myFix = true;

    private void Update() {
        if (!IsPlaying) return;

        double currentTime = AudioSettings.dspTime;

        if (loopBeats > 0) {
            CopyLevels(voices[1]);
            ScheduleNextRepeat(currentTime);
        } else if (SongIsOver(currentTime)) {
            clockRunning = false; // as when its AudioSource used to stop
            return;
        }

        KeepClockOnAudio(currentTime);

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

        double beats = CurrentSongTime / SecPerBeat;
        return Mathf.Clamp01((float)(beats - System.Math.Floor(beats)));
    }

    /// <summary>
    /// Starts the music from the beginning: schedules its first sample a little ahead (more
    /// if a pre-roll was requested) and starts the clock against that exact moment.
    /// </summary>
    public void StartMusic() {
        if (musicSource == null) return;

        StopVoices();

        AudioClip clip = musicSource.clip;
        clipSeconds = clip != null ? (double)clip.samples / clip.frequency : 0.0;

        // Repeats start on whole beats so the beat grid - and the chart - carry straight on.
        // The small gap that leaves after the song's last sample is silence.
        loopBeats = loopSong && clip != null
            ? Mathf.Max(1, Mathf.CeilToInt((float)(clipSeconds * bpm / 60.0) - 0.001f))
            : 0;

        if (voices == null)
            voices = loopBeats > 0 ? new[] { musicSource, CreateSecondVoice() } : new[] { musicSource };

        outputLatencySeconds = EstimateOutputLatency();

        // Beat 0 lands preRollBeats after now at the earliest; the song's own lead-in before
        // its first beat (the offset) counts towards that wait.
        double offsetSeconds = LeadInSeconds;
        double wait = System.Math.Max(MinScheduleAhead, preRollBeats * SecPerBeat - offsetSeconds);

        songStartDspTime = AudioSettings.dspTime + wait;
        musicSource.PlayScheduled(songStartDspTime);
        repeatsScheduled = 1;

        CurrentBeat = 0;
        CurrentMeasure = 0;
        myFix = true;
        nextHalfBeatTime = songStartDspTime + offsetSeconds + outputLatencySeconds;
        clockRunning = true;

        if (showDebugLogs) {
            Debug.Log($"🎵 Music scheduled in {wait:F2}s | loop: {(loopBeats > 0 ? loopBeats + " beats" : "no")} | " +
                      $"output latency: {outputLatencySeconds * 1000:F0} ms");
        }
    }

    /// <summary>
    /// Stops the music.
    /// </summary>
    public void StopMusic() {
        clockRunning = false;
        StopVoices();
    }

    private bool SongIsOver(double dspNow) {
        // Without a clip (some other kind of audio resource) the length isn't known: wait for the source to stop.
        if (clipSeconds <= 0.0)
            return dspNow > songStartDspTime + 0.5 && !musicSource.isPlaying;

        return dspNow > songStartDspTime + clipSeconds / speedMultiplier + outputLatencySeconds;
    }

    private void StopVoices() {
        if (voices == null) {
            if (musicSource != null) musicSource.Stop();
            return;
        }

        foreach (AudioSource voice in voices)
            voice.Stop();
    }

    /// <summary>Hands the next play of a looping song to the audio thread ahead of time.</summary>
    private void ScheduleNextRepeat(double dspNow) {
        double nextStart = songStartDspTime + repeatsScheduled * LoopSeconds;
        if (dspNow < nextStart - RepeatScheduleAhead) return;

        AudioSource voice = voices[repeatsScheduled % voices.Length];
        CopyLevels(voice);
        voice.PlayScheduled(nextStart);
        repeatsScheduled++;
    }

    /// <summary>
    /// Compares the clock with where the playing source really is and re-anchors the
    /// clock if they've drifted apart - the audio can stall (an editor pause, a hiccup)
    /// while dspTime keeps going.
    /// </summary>
    private void KeepClockOnAudio(double dspNow) {
        if (clipSeconds <= 0.0 || dspNow < songStartDspTime) return;

        int play = loopBeats > 0 ? (int)((dspNow - songStartDspTime) / LoopSeconds) : 0;
        AudioSource voice = voices[play % voices.Length];
        if (!voice.isPlaying || voice.clip == null) return;

        double playStart = songStartDspTime + play * (loopBeats > 0 ? LoopSeconds : 0.0);
        double expected = (dspNow - playStart) * speedMultiplier;

        // Near the edges of a play the source's position is still settling or already done.
        if (expected < 0.25 || expected > clipSeconds - 0.25) return;

        double actual = (double)voice.timeSamples / voice.clip.frequency;
        double drift = expected - actual;
        if (System.Math.Abs(drift) < ResyncThreshold) return;

        double shift = drift / speedMultiplier;
        songStartDspTime += shift;
        nextHalfBeatTime += shift;

        // The next loop was scheduled against the old timeline
        if (loopBeats > 0 && repeatsScheduled > play + 1)
            voices[(play + 1) % voices.Length].SetScheduledStartTime(songStartDspTime + (play + 1) * LoopSeconds);

        if (showDebugLogs)
            Debug.Log($"BeatManager: clock was {drift * 1000:F0} ms off the music, re-anchored");
    }

    private AudioSource CreateSecondVoice() {
        AudioSource voice = musicSource.gameObject.AddComponent<AudioSource>();
        voice.clip = musicSource.clip;
        voice.outputAudioMixerGroup = musicSource.outputAudioMixerGroup;
        voice.playOnAwake = false;
        voice.loop = false;
        voice.priority = musicSource.priority;
        voice.spatialBlend = musicSource.spatialBlend;
        voice.panStereo = musicSource.panStereo;
        voice.bypassEffects = musicSource.bypassEffects;
        voice.bypassListenerEffects = musicSource.bypassListenerEffects;
        voice.bypassReverbZones = musicSource.bypassReverbZones;
        CopyLevels(voice);
        return voice;
    }

    // Anything that turns the music source up or down (a volume menu) reaches the other voice too.
    private void CopyLevels(AudioSource voice) {
        if (voice == musicSource) return;

        voice.volume = musicSource.volume;
        voice.mute = musicSource.mute;
        voice.pitch = musicSource.pitch;
    }

    private double EstimateOutputLatency() {
        if (outputLatencyMs >= 0f) return outputLatencyMs / 1000.0;

        int bufferLength, numBuffers;
        AudioSettings.GetDSPBufferSize(out bufferLength, out numBuffers);

        int sampleRate = AudioSettings.outputSampleRate;
        if (sampleRate <= 0) return 0.0;

        return System.Math.Min(0.15, (double)bufferLength * numBuffers / sampleRate);
    }

    private void SetPitch() {
        if (voices == null) {
            if (musicSource != null) musicSource.pitch = speedMultiplier;
            return;
        }

        foreach (AudioSource voice in voices)
            voice.pitch = speedMultiplier;
    }

    /// <summary>
    /// Changes the speed multiplier (for accessibility slider).
    /// Affects both music pitch and all beat timing.
    /// </summary>
    public void SetSpeed(float newSpeed) {
        speedMultiplier = Mathf.Clamp(newSpeed, 0.5f, 1f);

        SetPitch();

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
        if (Application.isPlaying) {
            SetPitch();
        }
    }
}