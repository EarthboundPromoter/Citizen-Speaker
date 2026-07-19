using System.Collections.Generic;
using CSAccess.Modality;
using CSAccess.Speech;
using UnityEngine.UI;

namespace CSAccess.Game
{
    /// <summary>
    /// New-node affordance (focus-model row 3, owner design): the game reveals
    /// station nodes progressively and plays a "new location" pulse sighted players
    /// get for free. We announce a composed count diff — "2 locations added." —
    /// at the FIRST full-control station moment per cycle (after any leading
    /// dialogue), never inside the cycle-end vitals string.
    ///
    /// Instrument: the game's own enabled-Selectable enumeration filtered to
    /// Location/Character Buttons (activeInHierarchy + interactable) — the same
    /// read the future N tree uses, and the instrument that caught Dock C-4 where
    /// a mod hierarchy walk missed it (session-5 lesson: read the game's truth,
    /// don't rebuild it).
    /// </summary>
    internal static class NodeCensus
    {
        private static readonly HashSet<string> Known = new HashSet<string>();
        private static bool _baselined;
        private static bool _pendingAnnounce;

        /// <summary>Cycle boundary: the next settled station moment announces the diff.</summary>
        public static void MarkCycleBoundary() => _pendingAnnounce = true;

        /// <summary>Scene load: re-baseline silently (cross-load diffs are unknowable).</summary>
        public static void OnSceneChanged()
        {
            Known.Clear();
            _baselined = false;
            _pendingAnnounce = false;
        }

        /// <summary>Called with the already-derived mode (no extra derivation cost).</summary>
        public static void Tick(Mode mode)
        {
            if (mode != Mode.Station) return;
            if (_baselined && !_pendingAnnounce) return;

            var current = Snapshot(out int locations, out int characters);
            if (current == null) return; // scene not ready

            if (!_baselined)
            {
                _baselined = true;
                Known.Clear();
                Known.UnionWith(current);
                Plugin.Log.LogInfo("[NodeCensus] baseline: " + locations + " location(s), "
                                   + characters + " character(s).");
                return;
            }

            _pendingAnnounce = false;
            int addedLoc = 0, addedChar = 0, removed = 0;
            foreach (var key in current)
                if (!Known.Contains(key))
                {
                    if (key.StartsWith("L:")) addedLoc++; else addedChar++;
                }
            foreach (var key in Known)
                if (!current.Contains(key)) removed++;

            Known.Clear();
            Known.UnionWith(current);

            if (addedLoc == 0 && addedChar == 0 && removed == 0) return;

            var sb = new System.Text.StringBuilder();
            if (addedLoc > 0)
                sb.Append(addedLoc).Append(addedLoc == 1 ? " location added. " : " locations added. ");
            if (addedChar > 0)
                sb.Append(addedChar).Append(addedChar == 1 ? " character added. " : " characters added. ");
            if (removed > 0)
                sb.Append(removed).Append(removed == 1 ? " node removed." : " nodes removed.");
            SpeechService.Say(sb.ToString().Trim(), Priority.Queued, "nav");
            Plugin.Log.LogInfo("[NodeCensus] " + sb.ToString().Trim());
        }

        /// <summary>Strip variant decoration: trailing digits, "Canvas", parentheticals —
        /// "Dragos's Yard Canvas 2" and "Riko Canvas (END)" reduce to the location core.</summary>
        private static string Normalize(string name)
        {
            int paren = name.IndexOf('(');
            if (paren > 0) name = name.Substring(0, paren);
            name = name.Trim();
            while (name.Length > 0 && char.IsDigit(name[name.Length - 1]))
                name = name.Substring(0, name.Length - 1).TrimEnd();
            if (name.EndsWith("Canvas"))
                name = name.Substring(0, name.Length - "Canvas".Length).TrimEnd();
            return name;
        }

        private static HashSet<string> Snapshot(out int locations, out int characters)
        {
            locations = 0; characters = 0;
            var set = new HashSet<string>();
            var all = Selectable.allSelectablesArray;
            if (all == null) return set;
            foreach (var s in all)
            {
                if (s == null || !s.interactable || !s.gameObject.activeInHierarchy) continue;
                string name = s.gameObject.name;
                bool isLoc = name == "Location Button";
                bool isChar = name == "Character Button";
                if (!isLoc && !isChar) continue;
                // Identity = the canvas-level ancestor (direct child of the Locations/
                // Characters root), normalized so story-state VARIANTS of one location
                // ("Empty Container Canvas" / "Canvas 2") count as one node
                // (focus-model row 3: tree dedups variants, live variant wins).
                var t = s.transform;
                string canvas = null;
                for (var cur = t.parent; cur != null && cur.parent != null; cur = cur.parent)
                {
                    string parentName = cur.parent.name;
                    if (parentName == "Locations" || parentName == "Characters")
                    { canvas = Normalize(cur.name); break; }
                }
                string key = (isLoc ? "L:" : "C:") + (canvas ?? GameQueries.PathOf(s.gameObject));
                if (set.Add(key))
                {
                    if (isLoc) locations++; else characters++;
                }
            }
            return set;
        }
    }
}
