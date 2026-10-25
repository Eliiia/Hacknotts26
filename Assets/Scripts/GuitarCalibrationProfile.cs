using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class GuitarCalibrationProfile
{
    [Serializable]
    public class Range
    {
        public string note;
        public float minHz, maxHz;
    }

    public int outputSampleRate;
    public float minimumAmplitude = 0.001f;
    public List<Range> ranges = new List<Range>();
    public static string SavePath => Path.Combine(Application.persistentDataPath, "guitar-calibration.json");
    public static bool IsEnabled(string note) => note != "E0" && note != "A0" && note != "D0" &&
        note != "e5" && note != "e7" && note != "e9";

    public string Identify(float frequency)
    {
        string result = "??";
        foreach (var range in ranges)
        {
            if (!IsEnabled(range.note)) continue;
            if (frequency < range.minHz || frequency > range.maxHz) continue;
            if (result != "??" && result != range.note) return "??";
            result = range.note;
        }
        return result;
    }

    public List<string> Conflicts()
    {
        var conflicts = new List<string>();
        for (int i = 0; i < ranges.Count; i++)
            for (int j = i + 1; j < ranges.Count; j++)
            {
                var a = ranges[i]; var b = ranges[j];
                if (!IsEnabled(a.note) || !IsEnabled(b.note)) continue;
                if (a.note == b.note || a.maxHz < b.minHz || b.maxHz < a.minHz) continue;
                string pair = a.note + " / " + b.note;
                if (!conflicts.Contains(pair)) conflicts.Add(pair);
            }
        return conflicts;
    }

    public static GuitarCalibrationProfile Load()
    {
        if (!File.Exists(SavePath)) return null;
        try
        {
            var profile = JsonUtility.FromJson<GuitarCalibrationProfile>(File.ReadAllText(SavePath));
            if (profile == null || profile.ranges == null || profile.ranges.Count == 0 ||
                profile.outputSampleRate != AudioSettings.outputSampleRate ||
                float.IsNaN(profile.minimumAmplitude) || float.IsInfinity(profile.minimumAmplitude) ||
                profile.minimumAmplitude <= 0f) return null;
            foreach (var range in profile.ranges)
                if (range == null || string.IsNullOrEmpty(range.note) ||
                    float.IsNaN(range.minHz) || float.IsNaN(range.maxHz) ||
                    float.IsInfinity(range.minHz) || float.IsInfinity(range.maxHz) ||
                    range.minHz <= 0f || range.maxHz < range.minHz) return null;
            profile.ranges.RemoveAll(range => !IsEnabled(range.note));
            return profile.ranges.Count > 0 ? profile : null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not load guitar calibration: " + exception.Message);
            return null;
        }
    }
}
