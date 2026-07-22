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
            public const string HeaderDemand = "Demand";
            public const string HeaderNarrative = "Narrative";
            public const string HeaderDrives = "Drives";
            public const string StatusOpen = "open";
            public const string StatusAvailable = "available";
            public const string NoDrives = "No marked nodes.";
            public const string Revealed = "Revealed: ";
            public const string Moved = " moved.";
            public const string HeaderTakes = "Takes";
            public const string NotActivatable = "Action card disabled.";
        }

        // Field columns trimmed to the relevant set (owner ruling, session 11):
        // Status cut — inclusion is gated on the available family, so every row was
        // "available" by construction; Narrative cut — un-hacked nodes haven't
        // populated it (it lives in the card table, where it does). Demand speaks
        // pre-entry from authored constants + the game's own INTERFACE-bucket count,
        // self-labeled ("Required die/dice: …" / "Takes <item>").
        private static readonly string[] Headers =
            { W.HeaderName, W.HeaderDemand, W.HeaderDrives };

        // The open node's interior is the same card the field marker owns (corpus:
        // node == action, one card per group), so it reads as a ONE-ROW table
        // (owner ruling 2026-07-20): columns over the card's facets, Enter = the
        // card's own live button (dice slot or collect), Backspace falls through
        // to the designed Leave.
        private static readonly string[] CardHeaders =
            { W.HeaderName, W.HeaderDemand, W.HeaderTakes, W.HeaderNarrative };

        private static int _row, _col;
        private static string _cardNode;
        private static int _cardCol;

        private struct Node
        {
            public string Name;
            public string Tagline;
            public float Z;
            public Transform Canvas;
            public GameObject Button;
            public Transform Card;
            public bool Open;
            public string Drives;
            public string Demand;
            public string Narrative;
        }

        // ---------- Permanent nav (D3, MapTable pattern) ----------

        /// <summary>The permanent-nav gate: table keys route whenever we are at the
        /// cloud surface and the Ctrl+X escape hatch is off.</summary>
        public static bool Active()
            => !Modality.NavIdiom.Native
               && Modality.ModeModel.Current() == Modality.Mode.Cloud;

        private static Modality.Mode _prevSurface = Modality.Mode.Title;
        private static float _surfaceAt;
        private static bool _entered;

        private static void EntryTick()
        {
            var surface = Modality.ModeModel.Surface();
            if (surface != _prevSurface)
            {
                _prevSurface = surface;
                _surfaceAt = Time.unscaledTime;
                _entered = false;
                return;
            }
            if (surface != Modality.Mode.Cloud || _entered || Modality.NavIdiom.Native)
                return;
            // 1.2s: land AFTER the entry census's own 0.8s Text Setup settle so
            // row names are populated and the silent baseline has run.
            if (Time.unscaledTime - _surfaceAt < 1.2f) return;
            // Camera flights inside the cloud keep their own mute; don't announce
            // mid-flight.
            if (Modality.CloudFlight.Suppressing()) return;
            _entered = true;
            EnterCloud();
        }

        private static void EnterCloud()
        {
            var rows = Rows();
            // Auto-select the open node's row (owner: entering with a node already
            // open lands on it).
            _row = 0; _col = 0;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Open) { _row = i; break; }
            SpeechService.Say(W.TableName + " "
                + (rows.Count > 0 ? RowRead(rows[_row]) : W.NoRows),
                Priority.Immediate, "table");
        }

        /// <summary>Ctrl+X return path: re-announce the field position.</summary>
        public static void AnnouncePosition()
        {
            _entered = true;
            EnterCloud();
        }

        /// <summary>Called by CloudFlight at a settled camera flight in place of its
        /// native focus announcement: a zoom-in speaks the open node's card row, a
        /// pull-back re-anchors on the field row. False = not ours (native mode or
        /// cloud already gone) — CloudFlight keeps its own announcement.</summary>
        public static bool AnnounceSettled()
        {
            if (!Active()) return false;
            var rows = Rows();
            if (rows.Count == 0) return false;
            int open = OpenIndex(rows);
            if (open >= 0)
            {
                _row = open;
                SpeechService.Say(CardRow(rows[open]), Priority.Immediate, "table");
                return true;
            }
            _row = Mathf.Clamp(_row, 0, rows.Count - 1);
            SpeechService.Say(RowRead(rows[_row]), Priority.Immediate, "table");
            return true;
        }

        public static bool HandleKeys()
        {
            var rows = Rows();
            int open = OpenIndex(rows);
            if (open >= 0) return HandleCardKeys(rows[open]);
            _cardNode = null;

            if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveRow(1); return true; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveRow(-1); return true; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { MoveCol(1); return true; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { MoveCol(-1); return true; }
            if (Input.GetKeyDown(KeyCode.Space)) { Detail(); return true; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            { Commit(); return true; }
            return false;
        }

        // ---------- The open-node card table (one row) ----------

        private static int OpenIndex(List<Node> rows)
        {
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Open) return i;
            return -1;
        }

        private static bool HandleCardKeys(Node node)
        {
            if (_cardNode != node.Name) { _cardNode = node.Name; _cardCol = 0; }

            // One row: vertical moves are a bare repeat (dead-end idiom).
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow)
                || Input.GetKeyDown(KeyCode.Space))
            { SpeechService.Say(CardRow(node), Priority.Immediate, "table"); return true; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { MoveCardCol(node, 1); return true; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { MoveCardCol(node, -1); return true; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            { CommitCard(node); return true; }
            return false;
        }

        private static void MoveCardCol(Node node, int delta)
        {
            _cardCol = Mathf.Clamp(_cardCol + delta, 0, CardHeaders.Length - 1);
            // C2: the demand cell self-labels ("Required dice: 2, 3") — the header
            // prefix stacked two labels; same suppression the field table has.
            SpeechService.Say(_cardCol == 1
                    ? (CardCell(node, _cardCol) ?? W.HeaderDemand + ": none")
                    : CardHeaders[_cardCol] + ": " + (CardCell(node, _cardCol) ?? "none"),
                Priority.Immediate, "table");
        }

        private static void CommitCard(Node node)
        {
            var button = CardButton(node);
            if (button != null) { Navigator.Click(button.gameObject); return; }
            SpeechService.Say(
                (node.Card != null ? Describe.DisabledReason(node.Card) : null)
                ?? W.NotActivatable, Priority.Immediate, "table");
        }

        private static Button CardButton(Node node)
        {
            if (node.Card == null) return null;
            var slot = Game.StationAtlas.FindDeep(node.Card, "Dice Slot Button");
            var b = slot != null ? slot.GetComponent<Button>() : null;
            if (b != null && b.gameObject.activeInHierarchy && b.IsInteractable()) return b;
            foreach (var candidate in node.Card.GetComponentsInChildren<Button>(false))
                if (candidate.IsInteractable()) return candidate;
            return null;
        }

        private static string CardCell(Node node, int col)
        {
            switch (col)
            {
                case 0: return node.Name;
                case 1: return node.Card != null ? DemandFor(node.Card) : null;
                // Verb-free under the header (owner dupe ruling, session-12 ride:
                // "Takes: Takes a die" heard in the card table too).
                case 2: return node.Card != null ? Describe.RequiresPhrase(node.Card) : null;
                default: return node.Card != null ? NarrativeFor(node.Card) : node.Narrative;
            }
        }

        /// <summary>Full card read: name, then populated facets in column order.
        /// C1: the generic "a die" Takes drops beside a die demand — the session-12
        /// cull covered the detail read only and the row composition still spoke
        /// "Required dice: 2, 3. a die." (column visits stay individually intact).</summary>
        private static string CardRow(Node node)
        {
            var sb = new System.Text.StringBuilder(node.Name);
            string demand = CardCell(node, 1);
            for (int c = 1; c < CardHeaders.Length; c++)
            {
                string cell = c == 1 ? demand : CardCell(node, c);
                if (cell == null) continue;
                if (c == 2 && demand != null
                    && ((cell == "a die" && demand.IndexOf("die",
                            System.StringComparison.OrdinalIgnoreCase) >= 0)
                        // C6: "Takes SOLHEIM CIPHER. an item." — the generic item
                        // phrase dangles beside a named item demand, same dupe.
                        || (cell.StartsWith("an item") && demand.StartsWith("Takes "))))
                    continue;
                sb.Append(". ").Append(cell);
            }
            return sb.ToString() + ".";
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
                : n.Name + ". " + (Cell(n, _col) ?? "none"),
                Priority.Immediate, "table");
        }

        private static void MoveCol(int delta)
        {
            var rows = Rows();
            if (rows.Count == 0) return;
            _row = Mathf.Clamp(_row, 0, rows.Count - 1);
            _col = Mathf.Clamp(_col + delta, 0, Headers.Length - 1);
            // The demand cell is self-labeled ("Required die: 2" / "Takes X") —
            // no header prefix (owner ruling, session 11).
            SpeechService.Say(_col == 1
                    ? (Cell(rows[_row], _col) ?? "none")
                    : Headers[_col] + ": " + (Cell(rows[_row], _col) ?? "none"),
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
            Navigator.Click(node.Button); // native camera flight; the settle speaks the card row
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
                    ? DropPlaceholder(SpeechService.Clean(descVar.Value.Trim()))
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

                var card = CardOf(fsm);
                list.Add(new Node
                {
                    Name = name,
                    Tagline = tagline,
                    Z = canvas.localPosition.z,
                    Canvas = canvas,
                    Button = buttonT != null ? buttonT.gameObject : null,
                    Card = card,
                    Open = open,
                    Drives = DrivesFor(canvas),
                    Demand = card != null ? DemandFor(card) : null,
                    Narrative = card != null ? NarrativeFor(card) : null,
                });
            }
            list.Sort((a, b) => b.Z.CompareTo(a.Z)); // static descending corridor sort
            return list;
        }

        /// <summary>The node's single action card (corpus: every one of the 40 groups
        /// holds exactly one action) via the canvas FSM's own Hacking Actions group
        /// reference — resolvable while the group is dormant.</summary>
        private static Transform CardOf(PlayMakerFSM canvasFsm)
        {
            var groupVar = canvasFsm.FsmVariables.GetFsmGameObject("Hacking Actions");
            var group = groupVar != null ? groupVar.Value : null;
            if (group == null) return null;
            foreach (Transform card in group.transform) return card;
            return null;
        }

        /// <summary>Demand cell (owner override: pre-entry demand is strictly better
        /// UX). Dice cards: authored Required Roll values + the INTERFACE-bucket count
        /// when the card is dormant. Gates/item cards (2026-07-22): the item name from
        /// the card's OWN Action Controller (Item Name / Cost Label / Item Required —
        /// authored from birth, 10/10 cloud gates live), which unifies the cloud read
        /// with the station Requires cell and retires the A5 class (the positional
        /// action-name derivation spoke a neighbor's cipher at field level). Name
        /// derivation kept only as fallback; TakesLine last.</summary>
        private static string DemandFor(Transform card)
        {
            string demand = Describe.HackingDemand(card, InterfaceBucketCount());
            if (demand != null) return demand;
            string item = Describe.ItemDemandName(card);
            if (item != null) return "Takes " + item;
            return Describe.CloudItemTake(card) ?? Describe.TakesLine(card);
        }

        private static int InterfaceBucketCount()
        {
            // The game's own bucket→glyph-count mapping (corpus: 0→1, +1→2, +2→3).
            switch (Substrate.LuaStore.SkillModifierForWord("INTERFACE"))
            {
                case "+1": return 2;
                case "+2": return 3;
                default: return 1;
            }
        }

        /// <summary>Narrative cell: the card controller's resolved Action Description
        /// (localizes at the group's first activation; empty before — the natural
        /// boundary). Falls back to the rendered Description text when active.</summary>
        private static string NarrativeFor(Transform card)
        {
            foreach (var fsm in card.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                var v = fsm.FsmVariables.GetFsmString("Action Description");
                if (v != null && !string.IsNullOrEmpty(v.Value))
                    return DropPlaceholder(SpeechService.Clean(v.Value.Trim()));
            }
            return DropPlaceholder(Describe.TextUnder(card, "Description"));
        }

        /// <summary>The authored localization default "Description in text table"
        /// leaks from unlocalized string variables (session-11 cloud live) — it is
        /// never rendered; speak nothing instead.</summary>
        private static string DropPlaceholder(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            string t = s.Trim().TrimEnd('.');
            return t.Equals("Description in text table",
                System.StringComparison.OrdinalIgnoreCase) ? null : s;
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
        /// populated facets. Narrative and status dropped (owner ruling, session 11
        /// — see the column registry note).</summary>
        private static string RowRead(Node n)
        {
            var sb = new System.Text.StringBuilder(n.Name);
            if (n.Tagline != null) sb.Append(". ").Append(n.Tagline);
            if (n.Open) sb.Append(". ").Append(W.StatusOpen);
            if (n.Demand != null) sb.Append(". ").Append(n.Demand);
            if (n.Drives != null) sb.Append(". ").Append(W.HeaderDrives).Append(' ').Append(n.Drives);
            return sb.ToString() + ".";
        }

        private static string Cell(Node n, int col)
        {
            switch (col)
            {
                case 0: return RowRead(n);
                case 1: return n.Demand;
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
            EntryTick();
            // Surface-based (D3): the die picker and other overlays are excursions,
            // not exits — the census runs only on a genuine cloud entry.
            bool cloud = Modality.ModeModel.Surface() == Modality.Mode.Cloud;
            if (cloud && !_wasCloud)
                _entryCensusAt = Time.unscaledTime + 0.8f; // let Text Setup settle
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
