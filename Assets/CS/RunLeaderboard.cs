using System;
using System.Collections.Generic;
using UnityEngine;

public static class RunLeaderboard
{
    private const string Key = "NeonRunner.LocalRecords.v1";
    [Serializable] public class Entry
    {
        public float seconds;
        public int misses; // Legacy long-note count, retained for existing saved records.
        public int collisionMisses;
        public long TotalMisses => (long)misses + collisionMisses;
        public string date;
    }
    [Serializable] private class SaveData { public List<Entry> entries = new List<Entry>(); }

    public static List<Entry> Load()
    {
        try
        {
            var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key, "{}"));
            var entries = data?.entries ?? new List<Entry>();
            entries.RemoveAll(e => e == null || e.seconds <= 0f || float.IsNaN(e.seconds) ||
                float.IsInfinity(e.seconds) || e.misses < 0 || e.collisionMisses < 0);
            entries.Sort(Compare);
            if (entries.Count > 10) entries.RemoveRange(10, entries.Count - 10);
            return entries;
        }
        catch (ArgumentException) { return new List<Entry>(); }
    }

    public static void Add(float seconds, int misses) => Add(seconds, 0, misses);

    public static void Add(float seconds, int collisionMisses, int misses)
    {
        if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        var entries = Load();
        entries.Add(new Entry { seconds = seconds, collisionMisses = Mathf.Max(0, collisionMisses), misses = Mathf.Max(0, misses), date = DateTime.Now.ToString("yyyy.MM.dd") });
        entries.Sort(Compare);
        if (entries.Count > 10) entries.RemoveRange(10, entries.Count - 10);
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(new SaveData { entries = entries }));
        PlayerPrefs.Save();
    }

    private static int Compare(Entry a, Entry b)
    {
        int misses = a.TotalMisses.CompareTo(b.TotalMisses);
        return misses != 0 ? misses : a.seconds.CompareTo(b.seconds);
    }
}
