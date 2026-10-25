
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class GuitarInput : MonoBehaviour
{
    private AudioSource source;
    private AudioClip mic;

    private const int SampleRate = 44100;
    private const int BufferLength = 1;
    private const int FftSize = 8192;
    private const float volumeMult = 4f;

    private float[] spectrum = new float[FftSize];

    private string currentNote = "??";
    public float LatestFrequency { get; private set; }
    public float LatestAmplitude { get; private set; }
    public bool CaptureReady => source != null && source.isPlaying;
    public float FrequencyBinWidth => AudioSettings.outputSampleRate / (2f * FftSize);
    public bool Calibrating { get; set; }
    public bool logReadings;
    private float nextReadingLogTime;
    private GuitarCalibrationProfile calibration;

    public void ApplyCalibration(GuitarCalibrationProfile profile) { calibration = profile; }

    IEnumerator Start()
    {
        calibration = GuitarCalibrationProfile.Load();
        source = GetComponent<AudioSource>();

        // Start recording from the default microphone.
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone found for guitar input.", this);
            yield break;
        }
        mic = Microphone.Start(null, true, BufferLength, SampleRate);
        if (mic == null) yield break;

        // Assign the recording clip to the AudioSource.
        source.clip = mic;

        // Allow playback to repeat continuously.
        source.loop = true;

        // Make sure the microphone audio is audible.
        source.mute = false;
        source.volume = volumeMult;

        // Wait until Unity reports that recording has started.
        float deadline = Time.realtimeSinceStartup + 8f;
        while (Microphone.GetPosition(null) <= 0)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Debug.LogError("Microphone did not start within 8 seconds.", this);
                StopCapture();
                yield break;
            }
            yield return null;
        }

        // Start playing the recorded microphone audio.
        source.Play();
    }

    void Update()
    {
        // Don't analyse audio until playback has started.
        if (source == null || !source.isPlaying)
            return;

        // Get the frequency spectrum of the audio being played.
        source.GetSpectrumData(
            spectrum,
            0,
            FFTWindow.BlackmanHarris
        );

        // Track the strongest frequency bin.
        int peakIndex = 0;
        float peak = 0f;

        // First FFT bin is at a predetermined hz
        float firstHz = 80f;
        int minBin = Mathf.CeilToInt(firstHz / FrequencyBinWidth);

        // Find the bin with the greatest magnitude.
        for (int i = minBin; i < spectrum.Length; i++)
        {
            if (spectrum[i] > peak)
            {
                peak = spectrum[i];
                peakIndex = i;
            }
        }

        // Convert the bin index into an approximate frequency in Hz.
        float frequency = peakIndex * FrequencyBinWidth;
        LatestFrequency = frequency;
        LatestAmplitude = peak;

        // Only report a frequency when there is a detectable signal.
        if (peak > (calibration != null && !Calibrating ? calibration.minimumAmplitude : 0.05f))
        {
            var note = calibration != null && !Calibrating ? calibration.Identify(frequency) : IdentifyNote(frequency);

            currentNote = note;

            if (logReadings && Time.unscaledTime >= nextReadingLogTime)
            {
                Debug.Log($"Likely note: {note} / Dominant frequency: {frequency:F1} Hz");
                nextReadingLogTime = Time.unscaledTime + 0.25f;
            }
        }
        else currentNote = "??";
    }

    void OnDestroy()
    {
        StopCapture();
    }

    public void StopCapture()
    {
        LatestFrequency = LatestAmplitude = 0f;
        currentNote = "??";
        StopAllCoroutines();
        if (source != null)
        {
            source.Stop();
            source.clip = null;
        }

        // Only release a recording this component started, and only once.
        if (mic != null)
        {
            mic = null;
            Microphone.End(null);
        }
    }

    string IdentifyNote(float frequency) {
        // New measurements only; each reading accepts a +/- 1 Hz range.
        // Shared/overlapping ranges resolve to the first matching case below.
        // The two E6 measurement lists are combined.
        switch (frequency) {
            case float n when (n >= 176.6f && n <= 178.6f):
                return "G0";
            case float n when (n >= 225.1f && n <= 227.1f):
                return "B0";
            case float n when (n >= 300.5f && n <= 302.5f):
                return "e0";
            case float n when (n >= 249.3f && n <= 251.3f) ||
                (n >= 82.4f && n <= 84.4f) ||
                (n >= 79.7f && n <= 81.7f):
                return "E1";
            case float n when (n >= 174.0f && n <= 176.0f) ||
                (n >= 260.1f && n <= 262.1f):
                return "E2";
            case float n when (n >= 93.2f && n <= 95.2f) ||
                (n >= 90.5f && n <= 92.5f) ||
                (n >= 273.5f && n <= 275.5f):
                return "E3";
            case float n when (n >= 95.9f && n <= 97.9f) ||
                (n >= 392.0f && n <= 394.0f) ||
                (n >= 98.6f && n <= 100.6f) ||
                (n >= 85.9f && n <= 87.9f) ||
                (n >= 93.2f && n <= 95.2f):
                return "E4";
            case float n when (n >= 206.3f && n <= 208.3f) ||
                (n >= 203.6f && n <= 205.6f) ||
                (n >= 101.3f && n <= 103.3f):
                return "E5";
            case float n when (n >= 219.7f && n <= 221.7f) ||
                (n >= 109.4f && n <= 111.4f) ||
                (n >= 106.7f && n <= 108.7f) ||
                (n >= 136.0f && n <= 138.0f) ||
                (n >= 112.0f && n <= 114.0f) ||
                (n >= 327.4f && n <= 329.4f):
                return "E6";
            case float n when (n >= 114.7f && n <= 116.7f) ||
                (n >= 233.2f && n <= 235.2f) ||
                (n >= 117.4f && n <= 119.4f) ||
                (n >= 136.3f && n <= 138.3f):
                return "E7";
            case float n when (n >= 243.2f && n <= 245.2f) ||
                (n >= 367.8f && n <= 369.8f) ||
                (n >= 365.1f && n <= 367.1f) ||
                (n >= 370.4f && n <= 372.4f) ||
                (n >= 125.5f && n <= 127.5f) ||
                (n >= 117.4f && n <= 119.4f):
                return "E8";
            case float n when (n >= 125.5f && n <= 127.5f) ||
                (n >= 130.9f && n <= 132.9f) ||
                (n >= 128.2f && n <= 130.2f):
                return "E9";
            case float n when (n >= 136.3f && n <= 138.3f) ||
                (n >= 133.6f && n <= 135.6f):
                return "E10";
            case float n when (n >= 144.3f && n <= 146.3f) ||
                (n >= 284.3f && n <= 286.3f) ||
                (n >= 141.7f && n <= 143.7f):
                return "E11";
            case float n when (n >= 149.7f && n <= 151.7f) ||
                (n >= 303.2f && n <= 305.2f) ||
                (n >= 305.8f && n <= 307.8f):
                return "E12";
            default: return "??";
        }
    }

    public string GetCurrentNote() {
        return currentNote;
    }
}
