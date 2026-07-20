using System.Collections.Generic;
using CSAccess.Modality;
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
                // Session-5 tighten: an early tick can see the station before its
                // location canvases enable (logged a 0-location baseline once) — a
                // real station always has locations, so wait for them.
                if (locations == 0) return;
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
            // BL-16 / fresh-run F3: the enabled-Selectable set is zone/camera-
            // local, so this diff conflates story changes with camera position.
            // MUTED until the census redesign; log-only as redesign evidence.
            Plugin.Log.LogInfo("[NodeCensus] (muted, BL-16) " + sb.ToString().Trim());
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
                // Identity = the location name parsed from the nearest "* Canvas"
                // ancestor at ANY depth (the same parse Describe uses), normalized so
                // story-state VARIANTS of one location ("Canvas 2", "(END)") count as
                // one node. BL-7: the old walk required the canvas to be a DIRECT child
                // of Locations/Characters; variant canvases that nest differently fell
                // back to the full object path, which changes on a swap — that fallback
                // was the false "1 location added. 1 node removed." (fired 3x, all
                // false, real-save session). Never key on the object or its path.
                var t = s.transform;
                string canvas = null;
                for (var cur = t.parent; cur != null; cur = cur.parent)
                {
                    int idx = cur.name.IndexOf(" Canvas");
                    if (idx > 0) { canvas = Normalize(cur.name.Substring(0, idx)); break; }
                }
                string key = (isLoc ? "L:" : "C:") + (canvas ?? Normalize(s.transform.parent != null
                    ? s.transform.parent.name : s.gameObject.name));
                if (set.Add(key))
                {
                    if (isLoc) locations++; else characters++;
                }
            }
            return set;
        }
    }
}
