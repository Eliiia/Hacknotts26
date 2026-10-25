using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(GuitarInput))]
public class GuitarCalibration : MonoBehaviour
{
    public float captureSeconds = 6f;
    private static readonly string[] Notes = { "G0", "B0", "e0", "E1", "E2", "E3", "E4", "E5", "E6", "E7", "E8", "E9", "E10", "E11", "E12" };
    private GuitarInput input;
    private GuitarCalibrationProfile profile;
    private readonly List<Vector2> samples = new List<Vector2>();
    private readonly List<float> noise = new List<float>();
    private int index;
    private float until, nextSample;
    private bool measuringNoise, recording, noiseReady, reviewing;
    private bool editingReview;
    private string message = "";
    private Vector2 scroll;

    private void Awake()
    {
        input = GetComponent<GuitarInput>();
        input.Calibrating = true;
        profile = GuitarCalibrationProfile.Load();
        if (profile != null)
        {
            noiseReady = true;
            reviewing = true;
            message = "Loaded saved calibration. Choose any input to redo; other inputs will be kept.";
        }
        else profile = new GuitarCalibrationProfile { outputSampleRate = AudioSettings.outputSampleRate };
    }

    private void SelectNote(int noteIndex)
    {
        index = noteIndex;
        reviewing = false;
        editingReview = true;
        input.Calibrating = true;
        message = "Re-record " + NoteLabel(Notes[index]);
        scroll = Vector2.zero;
    }

    private void Update()
    {
        if (!recording && !measuringNoise) return;
        if (!input.CaptureReady)
        {
            recording = measuringNoise = false;
            message = "Microphone stopped. Check the input device and restart Play mode.";
            return;
        }
        if (Time.unscaledTime >= nextSample)
        {
            nextSample = Time.unscaledTime + 0.05f;
            if (measuringNoise) noise.Add(input.LatestAmplitude);
            else if (input.LatestAmplitude >= profile.minimumAmplitude && input.LatestFrequency > 0f)
                samples.Add(new Vector2(input.LatestFrequency, input.LatestAmplitude));
        }
        if (Time.unscaledTime < until) return;
        if (measuringNoise)
        {
            noise.Sort();
            float background = noise.Count == 0 ? 0f : noise[Mathf.Min(noise.Count - 1, Mathf.FloorToInt(noise.Count * .95f))];
            profile.minimumAmplitude = Mathf.Max(0.001f, background * 3f);
            noiseReady = true; measuringNoise = false;
            message = "Ready. Record each input with 3–5 separate plucks, muting between plucks.";
        }
        else
        {
            recording = false;
            var ranges = BuildRanges(Notes[index], samples.Select(x => x.x).ToList(), input.FrequencyBinWidth);
            if (samples.Count < 20 || ranges.Count == 0)
            {
                message = "Not enough stable signal. Try again with clearer/louder plucks.";
                return;
            }
            profile.ranges.RemoveAll(r => r.note == Notes[index]);
            profile.ranges.AddRange(ranges);
            message = "Captured " + Notes[index] + ": " + Describe(Notes[index]);
            if (editingReview) { reviewing = true; editingReview = false; }
            else { index++; reviewing = index >= Notes.Length; }
        }
    }

    // Keep frequency groups seen repeatedly; discard short, isolated readings.
    public static List<GuitarCalibrationProfile.Range> BuildRanges(string note, List<float> readings, float binWidth)
    {
        var clean = readings.Where(x => x > 0f && !float.IsNaN(x) && !float.IsInfinity(x)).OrderBy(x => x).ToList();
        var result = new List<GuitarCalibrationProfile.Range>();
        int minimumCount = Mathf.Max(3, Mathf.CeilToInt(clean.Count * .05f));
        int start = 0;
        for (int end = 1; end <= clean.Count; end++)
        {
            if (end < clean.Count && clean[end] - clean[end - 1] <= binWidth * 1.1f) continue;
            if (end - start >= minimumCount)
            {
                var cluster = clean.GetRange(start, end - start);
                float low = cluster[Mathf.FloorToInt((cluster.Count - 1) * .1f)];
                float high = cluster[Mathf.CeilToInt((cluster.Count - 1) * .9f)];
                float margin = Mathf.Max(1f, binWidth * .45f);
                result.Add(new GuitarCalibrationProfile.Range { note = note, minHz = Mathf.Max(.01f, low - margin), maxHz = high + margin });
            }
            start = end;
        }
        return result;
    }

    private string Describe(string note) => string.Join(", ", profile.ranges.Where(r => r.note == note).Select(r => $"{r.minHz:F1}–{r.maxHz:F1} Hz"));
    private static string NoteLabel(string note) => note.StartsWith("e") ? "High " + note : note;

    private void OnGUI()
    {
        GUI.skin.label.fontSize = 18;
        GUI.skin.button.fontSize = 18;
        float width = Mathf.Min(Screen.width - 24f, 820f);
        GUILayout.BeginArea(new Rect(12, 12, width, Screen.height - 24f), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("Guitar calibration");
        GUILayout.Label("Use the same guitar tuning, microphone position and input volume as gameplay.");
        GUILayout.Label("High e0 = open high E string. E1–E12 = low E frets. Open low E, A and D are excluded.");
        GUILayout.Label(input.CaptureReady ? $"Live: {input.LatestFrequency:F1} Hz   Signal: {input.LatestAmplitude:F4}" : "Waiting for microphone… Check Console if it does not start.");
        GUILayout.Label(message);
        GUI.enabled = input.CaptureReady && !recording && !measuringNoise;
        GUILayout.Label("Choose any input to record or redo (saved inputs marked *):");
        for (int row = 0; row < Notes.Length; row += 6)
        {
            GUILayout.BeginHorizontal();
            for (int i = row; i < Mathf.Min(row + 6, Notes.Length); i++)
            {
                string label = NoteLabel(Notes[i]);
                if (profile.ranges.Any(r => r.note == Notes[i])) label += " *";
                if (GUILayout.Button(label)) SelectNote(i);
            }
            GUILayout.EndHorizontal();
        }
        if (!noiseReady)
        {
            GUILayout.Label("First, mute your strings and stay quiet for two seconds.");
            if (GUILayout.Button("Measure background noise"))
            { noise.Clear(); measuringNoise = true; until = Time.unscaledTime + 2f; nextSample = 0f; }
        }
        else if (!reviewing)
        {
            string display = NoteLabel(Notes[index]);
            GUILayout.Label($"Input {index + 1}/{Notes.Length}: {display}");
            if (GUILayout.Button($"Record {display} — {captureSeconds:F0} seconds"))
            { samples.Clear(); recording = true; until = Time.unscaledTime + Mathf.Max(2f, captureSeconds); nextSample = 0f; message = "Play 3–5 separate plucks now."; }
            if (index > 0 && GUILayout.Button("Redo previous input")) { index--; message = "Record the previous input again."; }
            if (profile.ranges.Count > 0 && GUILayout.Button("Back to review")) reviewing = true;
        }
        else
        {
            GUILayout.Label("Review proposed ranges");
            foreach (string note in Notes) GUILayout.Label(note + ": " + Describe(note));
            var conflicts = profile.Conflicts();
            GUILayout.Label(conflicts.Count == 0 ? "No overlapping input ranges." : "Overlaps: " + string.Join(", ", conflicts) + ". Shared readings will return ?? rather than select the wrong input.");
            bool complete = Notes.All(note => profile.ranges.Any(r => r.note == note));
            if (!complete) GUILayout.Label("Record the missing inputs before saving. Select them above.");
            GUI.enabled = GUI.enabled && complete;
            if (GUILayout.Button(conflicts.Count == 0 ? "Save and apply calibration" : "Save and apply — reject ambiguous readings"))
            {
                try
                {
                    File.WriteAllText(GuitarCalibrationProfile.SavePath, JsonUtility.ToJson(profile, true));
                    input.ApplyCalibration(profile); input.Calibrating = false;
                    message = "Saved to " + GuitarCalibrationProfile.SavePath;
                }
                catch (Exception exception) { message = "Could not save: " + exception.Message; }
            }
        }
        GUI.enabled = true;
        if (recording || measuringNoise) GUILayout.Label($"{Mathf.Max(0, until - Time.unscaledTime):F1} seconds remaining");
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
