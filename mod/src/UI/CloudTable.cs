using System.Collections.Generic;
using CSAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace CSAccess.UI
{
    /// <summary>
    /// The cloud node field as a table (owner rulings 2026-07-20, geometry walk):
    /// the field is a ONE-AXIS corridor (all node canvases at x=0,y=0, only Z varies
    /// — level1 statics), so corridor order does the geometric work: rows sorted by
    /// Z descending (the station sort ruling), adjacency in the table = adjacency in
    /// space. No connected-nodes cell, no subtree filter.
    ///
    /// Connection intuition ships as two callout channels instead (owner-approved):
    ///  1. Post-hack reveal callout — after a cloud outcome, diff the rendered node
    ///     set and speak what appeared (all hack-driven reveal edges are LOCAL:
    ///     gates reveal their own corridor neighborhood — bridge sweep 2026-07-20).
    ///  2. Cloud-entry census — on entering scan mode, diff against the last visit:
    ///     new / gone / moved. Most reveals are station-story-driven, so entry is
    ///     when the game first renders them. Keyed on rendered names, never objects
    ///     (BL-7 lesson; agents are 3 position-variants sharing one name — a name at
    ///     a new position is "moved", the owner-approved agent line).
    ///
    /// Field markers render name, tagline, and tracking-gated drive pips only —
    /// Demand/Actions render inside an entered node and stay with the card flow —
    /// so columns are Name | Status | Drives. Inclusion = render: a node is a row
    /// while its canvas dial rests in the Variables Met family (camera-independent,
    /// the BL-16-proof instrument). Enter = one native marker click; the game's own
    /// camera flight follows (CloudFlight mutes the flurry). Browse is camera-silent
    /// in v1 — Focus-rig sync is a live-check follow-up.
    /// </summary>
    internal static class CloudTable
    {
        private static class W
        {
            public const string TableName = "Cloud table.";
            public const string NoRows = "No nodes rendered.";
            public const string HeaderName = "Name";
            public const string HeaderStatus = "Status";
            public const string HeaderDrives = "Drives";
            public const string StatusOpen = "open";
            public const string StatusAvailable = "available";
            public const string NoDrives = "No marked nodes.";
            public const string Revealed = "Revealed: ";
            public const string Moved = " moved.";
            public const string Closed = "Cloud table closed.";
        }

        private static readonly string[] Headers = { W.HeaderName, W.HeaderStatus, W.HeaderDrives };

        public static bool IsOpen { get; private set; }
        private static int _row, _col;

        private struct Node
        {
            public string Name;
            public string Tagline;
            public float Z;
            public Transform Canvas;
            public GameObject Button;
            public bool Open;
            public string Drives;
        }

        // ---------- Open / close / keys (MapTable pattern) ----------

        public static void Open()
        {
            var rows = Rows();
            IsOpen = true;
            _row = 0; _col = 0;
            SpeechService.Say(W.TableName + " "
                + (rows.Count > 0 ? RowRead(rows[0]) : W.NoRows),
                Priority.Immediate, "table");
        }

        public static void Close(bool announce)
        {
            IsOpen = false;
            if (announce) SpeechService.Say(W.Closed, Priority.Immediate, "table");
        }

        public static bool HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Backspace))
            { Close(announce: true); return true; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveRow(1); return true; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveRow(-1); return true; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { MoveCol(1); return true; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { MoveCol(-1); return true; }
            if (Input.GetKeyDown(KeyCode.Space)) { Detail(); return true; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            { Commit(); return true; }
            return false;
        }

        private static void MoveRow(int delta)
        {
            var rows = Rows();
            if (rows.Count == 0)
            { SpeechService.Say(W.NoRows, Priority.Immediate, "table"); return; }
            _row = Mathf.Clamp(_row + delta, 0, rows.Count - 1);
            var n = rows[_row];
            // Camera-synced browse (owner live-confirmed: cloud rides the same
            // one-axis Focus rig — W/S pans the corridor identically). Same config
            // gate and focus mute as the station table.
            MapTable.CameraToAngle(Game.StationAtlas.MarkerAngle(
                n.Button != null ? n.Button.transform : n.Canvas));
            SpeechService.Say(_col == 0 ? RowRead(n)
                : n.Name + ". " + Cell(n, _col),
                Priority.Immediate, "table");
        }

        private static void MoveCol(int delta)
        {
            var rows = Rows();
            if (rows.Count == 0) return;
            _row = Mathf.Clamp(_row, 0, rows.Count - 1);
            _col = Mathf.Clamp(_col + delta, 0, Headers.Length - 1);
            SpeechService.Say(Headers[_col] + ": " + Cell(rows[_row], _col),
                Priority.Immediate, "table");
        }

        private static void Detail()
        {
            var rows = Rows();
            if (rows.Count == 0) return;
            _row = Mathf.Clamp(_row, 0, rows.Count - 1);
            SpeechService.Say(RowRead(rows[_row]), Priority.Immediate, "table");
        }

        private static void Commit()
        {
            var rows = Rows();
            if (rows.Count == 0) return;
            _row = Mathf.Clamp(_row, 0, rows.Count - 1);
            var node = rows[_row];
            if (node.Button == null)
            {
                SpeechService.Say(node.Name + " is not clickable.", Priority.Immediate, "table");
                return;
            }
            Close(announce: false);
            Navigator.Click(node.Button); // native camera flight; CloudFlight announces the settle
        }

        // ---------- Rows / cells ----------

        private static Transform _hackingUi;

        private static Transform HackingUi()
        {
            if (_hackingUi == null)
            {
                var go = GameObject.Find("ERLIN MAIN/1_Station UI/Hacking UI");
                if (go != null) _hackingUi = go.transform;
            }
            return _hackingUi;
        }

        private static List<Node> Rows()
        {
            var list = new List<Node>();
            var hui = HackingUi();
            if (hui == null) return list;
            foreach (Transform canvas in hui)
            {
                if (!canvas.gameObject.activeInHierarchy) continue;
                var fsm = canvas.GetComponent<PlayMakerFSM>();
                if (fsm == null || fsm.ActiveStateName == null
                    || !fsm.ActiveStateName.StartsWith("Variables Met")) continue;

                var nameVar = fsm.FsmVariables.GetFsmString("Location Name");
                string name = nameVar != null && !string.IsNullOrEmpty(nameVar.Value)
                    ? SpeechService.Clean(nameVar.Value.Trim())
                    : canvas.name.TrimEnd();
                var descVar = fsm.FsmVariables.GetFsmString("Location Description");
                string tagline = descVar != null && !string.IsNullOrEmpty(descVar.Value)
                    ? SpeechService.Clean(descVar.Value.Trim())
                    : null;

                var buttonT = Game.StationAtlas.FindDeep(canvas, "Location Button");
                bool open = false;
                if (buttonT != null)
                {
                    var bfsm = buttonT.GetComponent<PlayMakerFSM>();
                    string bs = bfsm != null ? bfsm.ActiveStateName : null;
                    open = bs == "Clicked" || bs == "Active" || bs == "UI Camera On"
                        || (bs != null && bs.StartsWith("Camera Transition"));
                }

                list.Add(new Node
                {
                    Name = name,
                    Tagline = tagline,
                    Z = canvas.localPosition.z,
                    Canvas = canvas,
                    Button = buttonT != null ? buttonT.gameObject : null,
                    Open = open,
                    Drives = DrivesFor(canvas),
                });
            }
            list.Sort((a, b) => b.Z.CompareTo(a.Z)); // static descending corridor sort
            return list;
        }

        /// <summary>Tracking-gated drive pips on the billboard ("&lt;DRIVE&gt; pip &lt;n&gt;"
        /// objects, rendered only for tracked drives — the station Drives rule).</summary>
        private static string DrivesFor(Transform canvas)
        {
            HashSet<string> drives = null;
            foreach (var t in canvas.GetComponentsInChildren<Transform>(false))
            {
                int idx = t.name.IndexOf(" pip ");
                if (idx <= 0) continue;
                (drives = drives ?? new HashSet<string>()).Add(t.name.Substring(0, idx));
            }
            return drives != null ? string.Join(", ", drives) : null;
        }

        /// <summary>Full read on row switch: name (tagline riding, station ruling) +
        /// populated facets.</summary>
        private static string RowRead(Node n)
        {
            var sb = new System.Text.StringBuilder(n.Name);
            if (n.Tagline != null) sb.Append(". ").Append(n.Tagline);
            if (n.Open) sb.Append(". ").Append(W.StatusOpen);
            if (n.Drives != null) sb.Append(". ").Append(W.HeaderDrives).Append(' ').Append(n.Drives);
            return sb.ToString() + ".";
        }

        private static string Cell(Node n, int col)
        {
            switch (col)
            {
                case 0: return RowRead(n);
                case 1: return n.Open ? W.StatusOpen : W.StatusAvailable;
                default: return n.Drives ?? W.NoDrives;
            }
        }

        // ---------- Census + reveal callouts (owner-approved two-channel design) ----------

        private static readonly Dictionary<string, float> _known = new Dictionary<string, float>();
        private static bool _hasBaseline;
        private static bool _wasCloud;
        private static float _entryCensusAt = -1f;
        private static float _revealDiffAt = -1f;
        private const float MovedThreshold = 100f;

        public static void Tick()
        {
            bool cloud = Modality.ModeModel.Current() == Modality.Mode.Cloud;
            if (cloud && !_wasCloud)
                _entryCensusAt = Time.unscaledTime + 0.8f; // let Text Setup settle
            if (!cloud && IsOpen) Close(announce: false);
            _wasCloud = cloud;

            if (_entryCensusAt > 0 && Time.unscaledTime >= _entryCensusAt)
            {
                _entryCensusAt = -1f;
                EntryCensus();
            }
            if (_revealDiffAt > 0 && Time.unscaledTime >= _revealDiffAt)
            {
                _revealDiffAt = -1f;
                RevealDiff();
            }
        }

        /// <summary>Called by CloudOutcomes after an outcome read: schedule the reveal
        /// diff a beat later (target dials flip on everyFrame watches + Text Setup).</summary>
        public static void AfterOutcome()
        {
            _revealDiffAt = Time.unscaledTime + 0.6f;
        }

        private static Dictionary<string, float> Snapshot()
        {
            var snap = new Dictionary<string, float>();
            foreach (var n in Rows()) snap[n.Name] = n.Z;
            return snap;
        }

        /// <summary>Entry: new / gone / moved vs the last visit. First visit per game
        /// run is the silent baseline (station census pattern).</summary>
        private static void EntryCensus()
        {
            var now = Snapshot();
            if (_hasBaseline)
            {
                var added = new List<string>();
                var moved = new List<string>();
                foreach (var kv in now)
                {
                    if (!_known.TryGetValue(kv.Key, out float oldZ)) added.Add(kv.Key);
                    else if (Mathf.Abs(oldZ - kv.Value) > MovedThreshold) moved.Add(kv.Key);
                }
                var gone = new List<string>();
                foreach (var name in _known.Keys)
                    if (!now.ContainsKey(name)) gone.Add(name);

                var parts = new List<string>();
                if (added.Count > 0)
                    parts.Add(added.Count + (added.Count == 1 ? " new node: " : " new nodes: ")
                        + string.Join(", ", added));
                if (gone.Count > 0)
                    parts.Add(gone.Count + (gone.Count == 1 ? " node gone: " : " nodes gone: ")
                        + string.Join(", ", gone));
                foreach (var name in moved) parts.Add(name + W.Moved.TrimEnd('.'));
                if (parts.Count > 0)
                    SpeechService.Say(string.Join(". ", parts) + ".", Priority.Queued, "cloud");
            }
            _known.Clear();
            foreach (var kv in now) _known[kv.Key] = kv.Value;
            _hasBaseline = true;
        }

        /// <summary>Post-hack: speak appeared nodes only (the local reveal edges); the
        /// hacked node's own despawn updates silently — its outcome was just announced.</summary>
        private static void RevealDiff()
        {
            if (!_hasBaseline) return;
            var now = Snapshot();
            var added = new List<string>();
            foreach (var kv in now)
                if (!_known.ContainsKey(kv.Key)) added.Add(kv.Key);
            if (added.Count > 0)
                SpeechService.Say(W.Revealed + string.Join(", ", added) + ".",
                    Priority.Queued, "cloud");
            _known.Clear();
            foreach (var kv in now) _known[kv.Key] = kv.Value;
        }
    }
}
