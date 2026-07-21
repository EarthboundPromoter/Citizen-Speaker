using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CSAccess.Game
{
    /// <summary>
    /// Camera-independent station truth for the map table (docs/map-table-design.md).
    /// Reads the always-active canvas FSMs under 1_Station UI/Locations and /Characters:
    /// the canvas FSM's ActiveStateName is the availability dial (corpus decode + live
    /// proof 2026-07-20 — Off/Off 2 = story-locked, never listed; Off Camera = available
    /// off-frustum), and its Location/Character Name variable is the localized rendered
    /// name. Walks inactive children by construction (GetComponentsInChildren(true) from
    /// the active canvas roots) — the render-honesty boundary is the STATE dial, never
    /// object visibility.
    /// </summary>
    internal static class StationAtlas
    {
        internal sealed class Row
        {
            public bool IsCharacter;
            public string Name;          // localized, FSM-resolved (rendered name)
            public string Tagline;       // billboard description ("Old dock terminal")
            public string State;         // canvas FSM ActiveStateName (diagnostic)
            public Transform Canvas;     // live variant canvas root
            public GameObject Button;    // Location/Character Button (may be inactive)
            public bool Interactable;    // zoom-down affordance (rendered + interactable)
            public bool IsNew;           // New Shine rendered (the sighted "new" pulse)
            public float Angle;          // corridor angle (rotator Z domain)
            public int Zone;             // 0 Rim / 1 Greenway / 2 Hub (heuristic, see ZoneOf)
        }

        // Location-canvas states that mean "the game shows (or would show) this node".
        // Everything else — Off / Off 2 / Text Setup and the character template's
        // polling states — is unlisted (owner ruling 4: not shown = not known).
        private static readonly HashSet<string> LocationListed = new HashSet<string>
        {
            "Variables Met", "Off Camera", "Selected", "Cycle Check",
            "Clock Flasher", "Flashed Already?",
        };

        private static readonly HashSet<string> CharacterListed = new HashSet<string>
        {
            "Variables Met Pos", "Variables Met Neg 1", "Click to Play",
            "Autoplay Wait", "Autoplay", "Active",
        };

        /// <summary>Build the full row set fresh (table open / tab swap — no caching
        /// across opens; variants swap and anchors move).</summary>
        public static List<Row> Build()
        {
            var rows = new List<Row>();
            var byName = new Dictionary<string, Row>();
            CollectContainer("Locations", false, rows, byName);
            CollectContainer("Characters", true, rows, byName);
            // Descending: table-Down matches the camera's natural forward travel from
            // the default position (owner report 2026-07-20 — ascending read inverted;
            // static flip, never dynamic reordering).
            rows.Sort((a, b) => b.Angle.CompareTo(a.Angle));
            return rows;
        }

        private static void CollectContainer(string containerName, bool characters,
            List<Row> rows, Dictionary<string, Row> byName)
        {
            var container = FindStationChild(containerName);
            if (container == null)
            {
                Plugin.Log.LogInfo("[Atlas] container '" + containerName + "' not found — silent.");
                return;
            }
            foreach (Transform canvas in container)
                CollectCanvas(canvas, characters, rows, byName);
        }

        private static void CollectCanvas(Transform canvas, bool characters,
            List<Row> rows, Dictionary<string, Row> byName)
        {
            var fsm = CanvasFsm(canvas);
            if (fsm == null)
            {
                // FSM-less grouping transform: Locations/Post Rim Gate holds the
                // entire far station (Lowend onward, ~70 canvases) one level down
                // (live find 2026-07-21 — Lowend rendered by the game, invisible to
                // the whole mod). Canvases all carry the availability FSM, so
                // recursion stops at real canvases and never walks their interiors.
                foreach (Transform child in canvas)
                    CollectCanvas(child, characters, rows, byName);
                return;
            }
            string state = fsm.ActiveStateName;
            bool listed = characters ? CharacterListed.Contains(state)
                                     : LocationListed.Contains(state);
            if (!listed) return;

            string varPrefix = characters ? "Character" : "Location";
            string name = StringVar(fsm, varPrefix + " Name");
            if (string.IsNullOrEmpty(name)) return; // graceful silence: unnamed template
            string tagline = StringVar(fsm, varPrefix + " Description");

            var button = FindDeep(canvas, characters ? "Character Button" : "Location Button");
            var selectable = button != null ? button.GetComponent<Selectable>() : null;
            var shine = FindDeep(canvas, "New Shine");

            var row = new Row
            {
                IsCharacter = characters,
                Name = Speech.SpeechService.Clean(name),
                Tagline = tagline != null ? Speech.SpeechService.Clean(tagline) : null,
                State = state,
                Canvas = canvas,
                Button = button != null ? button.gameObject : null,
                Interactable = selectable != null && selectable.gameObject.activeInHierarchy
                               && selectable.IsInteractable(),
                IsNew = shine != null && shine.gameObject.activeInHierarchy,
                Angle = MarkerAngle(button != null ? button.transform : canvas),
            };
            row.Zone = ZoneOf(button != null ? button.transform : canvas, row.Angle);

            // Variant dedup by rendered name: the live (non-Off) variant wins; if two
            // variants both read listed (transitional frame), keep the interactable one.
            if (byName.TryGetValue((characters ? "C:" : "L:") + row.Name, out var existing))
            {
                if (!existing.Interactable && row.Interactable)
                {
                    rows[rows.IndexOf(existing)] = row;
                    byName[(characters ? "C:" : "L:") + row.Name] = row;
                }
                return;
            }
            byName[(characters ? "C:" : "L:") + row.Name] = row;
            rows.Add(row);
        }

        // ---------- Facet reads (cells; each cites its source in map-table-design.md) ----------

        /// <summary>Clock cell: the billboard dial first, else the location's own clock
        /// groups (the K-index source — render-paired via the owner's reachability
        /// ruling: one Enter away). "Billboarded" and "started" are different axes
        /// (owner catch 2026-07-20: Shipyard's 0-of-8 didn't billboard and read as no
        /// clock; the club's 0-of-4 did) — null now means the location truly has NO
        /// clock. Values are as-rendered/last-maintained; staleness acceptance-flagged.</summary>
        public static string ClockCell(Row row)
        {
            // F1b (run-2 finding): the billboard must actually RENDER before its
            // dial is read — step-clock variants keep local activeSelf under an
            // off billboard root, which spoke a clock the game wasn't drawing
            // (Dragos's Yard "0 of 8, negative"). Billboard cycle-ness renders
            // via the separate Cycle Clock Visual child, not the FSM bool.
            var clockRoot = FindDeep(row.Canvas, "Location Clock");
            string fromBillboard =
                clockRoot != null && clockRoot.gameObject.activeInHierarchy
                    ? DialString(clockRoot, activeOnly: true) : null;
            if (fromBillboard != null)
            {
                var cycleVisual = FindDeep(clockRoot, "Cycle Clock Visual");
                if (cycleVisual != null && cycleVisual.gameObject.activeInHierarchy
                    && !fromBillboard.Contains("cycle clock") && fromBillboard.EndsWith(" clock"))
                    fromBillboard = fromBillboard.Substring(0, fromBillboard.Length - " clock".Length)
                        + " cycle clock";
                return fromBillboard;
            }

            // Interior clock groups live in the location's Actions group (corpus:
            // "<Location> Actions/<Name> Clock/<N> Step Clock" families).
            var fsm = row.Canvas != null ? row.Canvas.GetComponent<PlayMakerFSM>() : null;
            var group = fsm != null ? fsm.FsmVariables.GetFsmGameObject("Location Actions") : null;
            var groupGo = group != null ? group.Value : null;
            if (groupGo == null) return null;
            var parts = new List<string>();
            foreach (Transform child in groupGo.transform)
            {
                if (!child.name.TrimEnd().EndsWith(" Clock")) continue;
                // The clock group's own local activeSelf is the story gate's
                // output (Action Switch family writes it) and survives the
                // parent Actions group being off at map view. Locally-off
                // groups are story-gated ghosts — the game never rendered
                // them (fresh-run F1: Empty Container spoke three unstarted
                // clocks; the rendered set inside was empty).
                if (!child.gameObject.activeSelf) continue;
                string dial = DialString(child, activeOnly: false);
                if (dial != null) parts.Add(dial);
            }
            return parts.Count > 0 ? string.Join("; ", parts) : null;
        }

        /// <summary>The step-clock dial under root with a ClockValue: the authored size
        /// variant (activeSelf when rendered; first-with-value when reading an interior
        /// group whose objects may be inactive).</summary>
        private static string DialString(Transform root, bool activeOnly)
        {
            foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (!fsm.gameObject.name.Contains("Step Clock")) continue;
                if (activeOnly && !fsm.gameObject.activeSelf) continue;
                var value = fsm.FsmVariables.GetFsmFloat("ClockValue");
                if (value == null) continue;
                int steps = GameQueries.LeadingInt(fsm.gameObject.name);
                if (steps <= 0) continue;
                // Interior groups keep every size variant; the authored one is the
                // ACTIVE child when rendered — prefer it, else take the first valued.
                if (!activeOnly && !fsm.gameObject.activeSelf)
                {
                    var active = FindActiveDial(root);
                    if (active != null && active != fsm) continue;
                }
                var positive = fsm.FsmVariables.GetFsmBool("Positive?");
                var cycle = fsm.FsmVariables.GetFsmBool("Cycle Clock?");
                var sb = new System.Text.StringBuilder();
                int v = Mathf.RoundToInt(value.Value);
                // F11: overshoot past the authored size renders full — "complete".
                if (v >= steps) sb.Append("complete");
                else sb.Append(v).Append(" of ").Append(steps);
                if (positive != null) sb.Append(positive.Value ? ", positive" : ", negative");
                if (cycle != null && cycle.Value) sb.Append(" cycle");
                sb.Append(" clock");
                return sb.ToString();
            }
            return null;
        }

        private static PlayMakerFSM FindActiveDial(Transform root)
        {
            // Local activeSelf, not activeInHierarchy: the authored size variant
            // keeps its local flag when the parent Actions group is off at map
            // view (fresh-run F1 — the old (false) walk found nothing there and
            // the caller fell back to an arbitrary first-valued variant).
            foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                if (fsm.gameObject.activeSelf
                    && fsm.gameObject.name.Contains("Step Clock")
                    && fsm.FsmVariables.GetFsmFloat("ClockValue") != null)
                    return fsm;
            return null;
        }

        /// <summary>Tracked-drive relevance: pip children carry quest name + entry; the
        /// pip's own gate is IsQuestTrackingEnabled (corpus decode) — the mod asks the
        /// same API the pip does, so the answer is camera-independent and identical to
        /// what a rendered pip would show. Only tracked, non-complete entries listed.</summary>
        public static List<string> DriveCell(Row row)
        {
            var drives = new List<string>();
            var billboard = FindDeep(row.Canvas, "Billboard Elements")
                            ?? FindDeep(row.Canvas, "Billboarding Elements");
            if (billboard == null) return drives;
            foreach (Transform child in billboard)
            {
                // Case-insensitive: pips ship as both " pip" and " Pip"
                // ("SURVIVE Pip" at Bright Market — fresh-run F8).
                if (child.name.IndexOf(" pip", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var fsm = child.GetComponent<PlayMakerFSM>();
                if (fsm == null) continue;
                var quest = fsm.FsmVariables.GetFsmString("Quest Name");
                var entry = fsm.FsmVariables.GetFsmInt("Entry #");
                if (quest == null || string.IsNullOrEmpty(quest.Value)) continue;
                try
                {
                    if (!PixelCrushers.DialogueSystem.QuestLog.IsQuestTrackingEnabled(quest.Value))
                        continue;
                    if (entry != null)
                    {
                        var st = PixelCrushers.DialogueSystem.QuestLog.GetQuestEntryState(
                            quest.Value, entry.Value);
                        if (st == PixelCrushers.DialogueSystem.QuestState.Success
                            || st == PixelCrushers.DialogueSystem.QuestState.Failure)
                            continue; // pip Complete state = deactivated
                    }
                    if (!drives.Contains(quest.Value)) drives.Add(quest.Value);
                }
                catch (System.Exception e) { Plugin.Log.LogWarning("[Atlas] quest read: " + e.Message); }
            }
            return drives;
        }

        /// <summary>Actions count: activeSelf "* Action" roots under the canvas FSM's own
        /// Location Actions group — maintained by the game's Action Switch / controller
        /// FSMs whenever the location renders; may lag for unvisited locations
        /// (acceptance-flagged). -1 = no group (characters, some structures).</summary>
        public static int ActionCount(Row row)
        {
            var fsm = CanvasFsm(row.Canvas);
            var group = fsm != null ? fsm.FsmVariables.GetFsmGameObject("Location Actions") : null;
            var groupGo = group != null ? group.Value : null;
            if (groupGo == null) return -1;
            int n = 0;
            foreach (Transform child in groupGo.transform)
                if (child.gameObject.activeSelf && child.name.TrimEnd().EndsWith(" Action"))
                    n++;
            return n;
        }

        /// <summary>Nearest available non-character row by corridor angle (the character
        /// tab's "where are they" cell).</summary>
        public static Row NearestLocation(Row character, List<Row> rows)
        {
            Row best = null;
            float bestDist = float.MaxValue;
            foreach (var r in rows)
            {
                if (r.IsCharacter || r.Zone != character.Zone) continue;
                float d = Mathf.Abs(Mathf.DeltaAngle(r.Angle, character.Angle));
                if (d < bestDist) { bestDist = d; best = r; }
            }
            return best;
        }

        // ---------- Rig geometry ----------

        /// <summary>Corridor angle in the rig's Damped Z domain, self-calibrating: the
        /// Focus object rides the rotator AT the centered location (live /transforms
        /// proof 2026-07-20 — Focus world pos == the on-camera marker's pos), so
        /// offset = Damped Z − rawAngle(Focus) holds every frame, no guessed constants.
        /// rawAngle = signed angle around the rotator's forward axis (the ring is the
        /// plane perpendicular to it — vertical in world; never use world Y).
        /// Sign risk (acceptance-flagged): if row moves drive the camera the WRONG WAY,
        /// negate raw angles (one line, AngleSign).</summary>
        private const float AngleSign = 1f;

        public static float MarkerAngle(Transform marker)
        {
            var rotator = RotatorTransform();
            if (rotator == null || marker == null) return 0f;
            float raw = RawAngle(marker.position, rotator);
            var focus = rotator.Find("Focus");
            var fsm = FocusFsm();
            var damped = fsm != null ? fsm.FsmVariables.GetFsmFloat("Damped Z") : null;
            if (focus == null || damped == null) return (raw + 360f) % 360f;
            float offset = damped.Value - RawAngle(focus.position, rotator);
            return Norm(raw + offset);
        }

        private static float RawAngle(Vector3 p, Transform rotator)
        {
            Vector3 axis = rotator.forward;
            Vector3 v = Vector3.ProjectOnPlane(p - rotator.position, axis);
            Vector3 reference = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
            return AngleSign * Vector3.SignedAngle(reference, v, axis);
        }

        private static float Norm(float a) => (a % 360f + 360f) % 360f;

        /// <summary>Zone from rig geometry: the Hub is literally the wheel's hub —
        /// small radial distance from the rotator axis (ring markers sit at ~9300;
        /// threshold halves it). On the ring, the rig clamps Rim to 135–258 and the
        /// Greenway ferry tween rides 258–275 (Location Controller decode) — split by
        /// calibrated angle. Thresholds live here for one-place calibration.</summary>
        private const float HubRadius = 5000f;
        private const float GreenwayFrom = 258.5f;
        private const float GreenwayTo = 320f;

        public static int ZoneOf(Transform marker, float angle)
        {
            var rotator = RotatorTransform();
            if (marker != null && rotator != null)
            {
                Vector3 v = Vector3.ProjectOnPlane(
                    marker.position - rotator.position, rotator.forward);
                if (v.magnitude < HubRadius) return 2; // Hub
            }
            return angle > GreenwayFrom && angle < GreenwayTo ? 1 : 0; // Greenway : Rim
        }

        public static Transform RotatorTransform()
        {
            var go = GameObject.Find("Focus Rotator");
            return go != null ? go.transform : null;
        }

        public static PlayMakerFSM FocusFsm()
        {
            var rotator = RotatorTransform();
            var focus = rotator != null ? rotator.Find("Focus") : null;
            return focus != null ? focus.GetComponent<PlayMakerFSM>() : null;
        }

        public static PlayMakerFSM LocationController()
        {
            foreach (var fsm in PlayMakerFSM.FsmList)
                if (fsm != null && fsm.gameObject != null
                    && fsm.gameObject.name == "Location Controller")
                    return fsm;
            return null;
        }

        /// <summary>Current zone from Lua LOCATION (0 Rim / 1 Greenway / 2 Hub — the
        /// Location Controller's own filter source). -1 unknown.</summary>
        public static int CurrentZone()
        {
            try
            {
                var r = PixelCrushers.DialogueSystem.DialogueLua.GetVariable("LOCATION");
                return r.hasReturnValue && r.isNumber ? Mathf.RoundToInt(r.asFloat) : -1;
            }
            catch { return -1; }
        }

        /// <summary>Greenway visited flag (clock-tier gate for its zone tab; the Hub has
        /// no flag — its tab gates on availability + session observation).</summary>
        public static bool GreenwayVisited()
        {
            try
            {
                var r = PixelCrushers.DialogueSystem.DialogueLua.GetVariable("GREENWAYVISITED");
                return r.hasReturnValue && r.isNumber && r.asFloat >= 1f;
            }
            catch { return false; }
        }

        // ---------- Plumbing ----------

        private static Transform FindStationChild(string name)
        {
            // 1_Station UI lives under the ERLIN MAIN scene root; the container itself
            // is always active, so Find reaches it.
            var go = GameObject.Find("1_Station UI/" + name);
            if (go != null) return go.transform;
            go = GameObject.Find(name);
            return go != null && go.transform.parent != null
                   && go.transform.parent.name == "1_Station UI" ? go.transform : null;
        }

        private static PlayMakerFSM CanvasFsm(Transform canvas)
        {
            // The availability FSM sits on the canvas root itself.
            return canvas != null ? canvas.GetComponent<PlayMakerFSM>() : null;
        }

        private static string StringVar(PlayMakerFSM fsm, string name)
        {
            var v = fsm.FsmVariables.GetFsmString(name);
            return v != null ? v.Value : null;
        }

        /// <summary>Depth-first named lookup INCLUDING inactive children.</summary>
        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
