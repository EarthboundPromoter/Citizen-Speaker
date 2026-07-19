using System.Collections.Generic;
using UnityEngine;

namespace CSAccessBridge
{
    /// <summary>Ring buffer of game events captured by the Harmony hooks.
    /// Add() is only called from the main thread (patches); Query() from the bridge thread.</summary>
    internal static class WatchLog
    {
        private const int Capacity = 8000;

        private static readonly object Lock = new object();
        private static readonly List<Dictionary<string, object>> Entries = new List<Dictionary<string, object>>();
        private static long _seq;

        public static void Add(string kind, string text, string path = null)
        {
            var entry = new Dictionary<string, object>
            {
                ["kind"] = kind,
                ["text"] = text,
                ["path"] = path,
                ["t"] = Time.realtimeSinceStartup,
                // Shared clock with the mod's stamped log lines ([mode fN tN]):
                // frame number is the join key across both event records.
                ["frame"] = Time.frameCount,
            };
            lock (Lock)
            {
                entry["seq"] = ++_seq;
                Entries.Add(entry);
                if (Entries.Count > Capacity)
                    Entries.RemoveRange(0, Entries.Count - Capacity);
            }
        }

        public static object Query(long since, int max)
        {
            lock (Lock)
            {
                var result = new List<object>();
                foreach (var e in Entries)
                {
                    if ((long)e["seq"] > since)
                    {
                        result.Add(e);
                        if (result.Count >= max) break;
                    }
                }
                return new Dictionary<string, object>
                {
                    ["latest"] = _seq,
                    ["entries"] = result,
                };
            }
        }
    }
}
