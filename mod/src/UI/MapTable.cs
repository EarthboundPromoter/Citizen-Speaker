using System.Collections.Generic;
using CSAccess.Game;
using CSAccess.Speech;
using UnityEngine;

namespace CSAccess.UI
{
    /// <summary>
    /// The station map table (docs/map-table-design.md, all rulings owner-made
    /// 2026-07-20). Zone tabs on slash (tab = native zone transit, visited zones
    /// only), rows = rendered locations/characters in corridor order, columns from a
    /// registry, stable geometry with terse empties, row report on vertical movement,
    /// header+cell on horizontal. Browsing drives the camera (Focus Z write — the
    /// game's own input accumulator; its FSM clamps and damps natively) and the native
    /// highlight lands via the game's own closest-claim; game-driven focus is muted
    /// while the camera settles (CloudFlight pattern). Enter anywhere in a row = one
    /// native click. N opens/closes; Station mode only.
    /// </summary>
    internal static class MapTable
    {
        // ---------- Wording block (ALL table phrases; owner calibration lands here) ----------
        private static class W
        {
            public const string Opened = "Station table.";
            public const string Closed = "Table closed.";
            public const string RowNew = "new";
            public const string RowDisabled = "disabled";
            public const string CharacterPrefix = "Character: ";
            public const string NoClock = "No clock.";
            public const string NoDrive = "No tracked drive.";
            public const string NoActions = "No actions.";
            public const string NoTagline = "No description.";
            public const string HeaderName = "Name";
            public const string HeaderClock = "Clock";
            public const string HeaderDrives = "Drives";
            public const string HeaderActions = "Actions";
            public const string HeaderTagline = "Description";
            public const string HeaderWhere = "Where";
            public const string TabCharacters = "Characters";
            public const string TabDrives = "Tracked drives";
            public const string NoRows = "Nothing here.";
            public const string CommitDisabled = "Not open yet.";
            public const string NoObjective = "No current objective.";
            public static string Zone(int z) => z == 0 ? "The Rim" : z == 1 ? "Greenway" : z == 2 ? "The Hub" : "Station";
        }

        // ---------- Column registry (ruling: build flexibly — one edit adds a column) ----------
        private sealed class Column
        {
            public string Header;
            public System.Func<StationAtlas.Row, string> Cell; // null result = empty cell
            public string EmptyForm;
        }

        private static readonly List<Column> Columns = new List<Column>
        {
            new Column { Header = W.HeaderName, EmptyForm = null, Cell = NameCell },
            new Column { Header = W.HeaderClock, EmptyForm = W.NoClock,
                Cell = r => StationAtlas.ClockCell(r) },
            new Column { Header = W.HeaderDrives, EmptyForm = W.NoDrive, Cell = r =>
                {
                    var d = StationAtlas.DriveCell(r);
                    return d.Count > 0 ? string.Join(", ", d) : null;
                } },
            new Column { Header = W.HeaderActions, EmptyForm = W.NoActions, Cell = r =>
                {
                    int n = StationAtlas.ActionCount(r);
                    return n < 0 ? null : n + (n == 1 ? " action" : " actions");
                } },
            new Column { Header = W.HeaderTagline, EmptyForm = W.NoTagline,
                Cell = r => r.Tagline },
        };

        // The characters tab appends the Where column (ruling 7).
        private static readonly Column WhereColumn = new Column
        {
            Header = W.HeaderWhere, EmptyForm = null, Cell = r =>
            {
                var near = StationAtlas.NearestLocation(r, _rows);
                return near != null ? "near " + near.Name + ", " + W.Zone(r.Zone) : W.Zone(r.Zone);
            }
        };

        // ---------- State ----------

        public static bool IsOpen { get; private set; }

        private static List<StationAtlas.Row> _rows = new List<StationAtlas.Row>();
        private static readonly List<int> Tabs = new List<int>(); // 0/1/2 zones, -2 chars, -3 drives
        private static int _tab, _row, _col;
        private static List<StationAtlas.Row> _view = new List<StationAtlas.Row>();
        private static List<string> _driveRows = new List<string>();
        private static bool _hubSeen;

        // Camera settle machinery (CloudFlight pattern: mute game focus, no announce —
        // the row report IS the announcement; the highlight lands silently).
        private static float _muteUntil = -1f;
        private static StationAtlas.Row _cameraTarget;
        private static int _pendingZone = -1;
        private static float _pendingZoneAt = -1f;

        public static bool SuppressingFocus()
            => IsOpen || Time.unscaledTime < _muteUntil;

        // ---------- Open / close ----------

        public static void Open()
        {
            _rows = StationAtlas.Build();
            if (_rows.Count == 0)
            {
                SpeechService.Say(W.NoRows, Priority.Immediate, "table");
                return;
            }
            BuildTabs();
            IsOpen = true;
            int zone = StationAtlas.CurrentZone();
            _tab = Mathf.Max(0, Tabs.IndexOf(zone));
            BuildView();
            _row = NearestRowToCamera();
            _col = 0;
            SpeechService.Say(W.Opened + " " + TabName(Tabs[_tab]) + ". " + RowReport(),
                Priority.Immediate, "table");
        }

        public static void Close(bool announce = true)
        {
            IsOpen = false;
            _muteUntil = -1f;
            _pendingZone = -1;
            if (announce) SpeechService.Say(W.Closed, Priority.Immediate, "table");
        }

        // ---------- Input (routed from InputManager while open) ----------

        public static bool HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Backspace))
            { Close(); return true; }

            if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveRow(1); return true; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveRow(-1); return true; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { MoveCol(1); return true; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { MoveCol(-1); return true; }
            if (Input.GetKeyDown(KeyCode.Slash)) { NextTab(); return true; }
            if (Input.GetKeyDown(KeyCode.Space)) { SpeakFullRow(); return true; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            { Commit(); return true; }
            return false;
        }

        public static void Tick()
        {
            // Session zone observation (Hub tab gate — the Hub has no visited flag).
            if (!_hubSeen && Time.frameCount % 120 == 0 && StationAtlas.CurrentZone() == 2)
                _hubSeen = true;

            // Two-step zone transit chain (Greenway<->Hub route via Rim).
            if (_pendingZone >= 0 && Time.unscaledTime >= _pendingZoneAt)
            {
                var controller = StationAtlas.LocationController();
                if (controller != null && IsZoneRest(controller.ActiveStateName))
                {
                    int target = _pendingZone;
                    _pendingZone = -1;
                    FireTransit(target);
                }
                else if (Time.unscaledTime > _pendingZoneAt + 6f)
                    _pendingZone = -1; // transit never settled; give up silently, log
            }
        }

        // ---------- Movement ----------

        private static void MoveRow(int delta)
        {
            if (_view.Count == 0 && Tabs[_tab] != -3)
            { SpeechService.Say(W.NoRows, Priority.Immediate, "table"); return; }

            if (Tabs[_tab] == -3) // drives tab: simple list rows
            {
                if (_driveRows.Count == 0)
                { SpeechService.Say(W.NoRows, Priority.Immediate, "table"); return; }
                _row = Mathf.Clamp(_row + delta, 0, _driveRows.Count - 1);
                SpeechService.Say(DriveRowReport(_driveRows[_row]), Priority.Immediate, "table");
                return;
            }

            _row = Mathf.Clamp(_row + delta, 0, _view.Count - 1);
            SpeechService.Say(_col == 0 ? RowReport() : Current().Name + ". " + CellText(_col),
                Priority.Immediate, "table");
            CameraTo(Current());
        }

        private static void MoveCol(int delta)
        {
            if (Tabs[_tab] == -3 || _view.Count == 0) return;
            var cols = ActiveColumns();
            _col = Mathf.Clamp(_col + delta, 0, cols.Count - 1);
            SpeechService.Say(cols[_col].Header + ": " + CellText(_col), Priority.Immediate, "table");
        }

        private static void NextTab()
        {
            if (Tabs.Count == 0) return;
            _tab = (_tab + 1) % Tabs.Count;
            _row = 0; _col = 0;
            int id = Tabs[_tab];
            if (id >= 0)
            {
                BuildView();
                if (id != StationAtlas.CurrentZone()) RequestZone(id);
                _row = _view.Count > 0 ? 0 : 0;
                SpeechService.Say(TabName(id) + ". "
                    + (_view.Count > 0 ? RowReport() : W.NoRows), Priority.Immediate, "table");
            }
            else if (id == -2)
            {
                BuildView();
                SpeechService.Say(W.TabCharacters + ". "
                    + (_view.Count > 0 ? RowReport() : W.NoRows), Priority.Immediate, "table");
            }
            else
            {
                BuildDriveRows();
                SpeechService.Say(W.TabDrives + ". "
                    + (_driveRows.Count > 0 ? DriveRowReport(_driveRows[0]) : W.NoRows),
                    Priority.Immediate, "table");
            }
        }

        private static void SpeakFullRow()
        {
            if (Tabs[_tab] == -3 || _view.Count == 0) return;
            var cols = ActiveColumns();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cols.Count; i++)
            {
                string cell = CellRaw(cols[i]);
                sb.Append(cols[i].Header).Append(": ")
                  .Append(cell ?? cols[i].EmptyForm ?? "none");
                if (!sb.ToString().EndsWith(".")) sb.Append('.');
                sb.Append(' ');
            }
            SpeechService.Say(sb.ToString().TrimEnd(), Priority.Immediate, "table");
        }

        private static void Commit()
        {
            if (Tabs[_tab] == -3 || _view.Count == 0) return;
            var row = Current();
            if (row.Button != null && row.Button.activeInHierarchy && row.Interactable)
            {
                Close(announce: false);
                Navigator.Click(row.Button);
            }
            else
                SpeechService.Say(row.Name + ": " + W.CommitDisabled, Priority.Immediate, "table");
        }

        // ---------- Camera ----------

        private static void CameraTo(StationAtlas.Row row)
        {
            if (!Plugin.MapTableCamera.Value || row == null) return;
            _cameraTarget = row;
            int zone = StationAtlas.CurrentZone();
            if (row.Zone != zone && row.Zone >= 0 && row.Zone <= 2)
            {
                RequestZone(row.Zone);
                return; // angle write follows on a later row move once zoned; v1 keeps it simple
            }
            WriteAngle(row.Angle);
        }

        /// <summary>The Focus Z write: the FSM's own scroll accumulator — the game's
        /// jump states do the same discrete write (+/-20) to the same domain, and its
        /// per-zone FloatClamp + SmoothDamp own everything after (map-table-design.md,
        /// camera decode).</summary>
        private static void WriteAngle(float angle)
        {
            var fsm = StationAtlas.FocusFsm();
            var focusZ = fsm != null ? fsm.FsmVariables.GetFsmFloat("Focus Z") : null;
            if (focusZ == null)
            {
                Plugin.Log.LogInfo("[Table] Focus Z not found — camera follow silent.");
                return;
            }
            focusZ.Value = angle;
            _muteUntil = Time.unscaledTime + 1.5f;
            if (Plugin.TraceInput.Value)
                Plugin.Log.LogInfo("[Table] camera -> " + angle.ToString("0.0"));
        }

        private static void RequestZone(int targetZone)
        {
            int current = StationAtlas.CurrentZone();
            if (current == targetZone) return;
            // Gate: only zones the player has reached natively (story safety, ruling 8).
            if (targetZone == 1 && !StationAtlas.GreenwayVisited()) return;
            if (targetZone == 2 && !_hubSeen) return;
            // Topology: Greenway<->Hub route via Rim (controller decode).
            if ((current == 1 && targetZone == 2) || (current == 2 && targetZone == 1))
            {
                FireTransit(0);
                _pendingZone = targetZone;
                _pendingZoneAt = Time.unscaledTime + 1.5f;
                return;
            }
            FireTransit(targetZone);
        }

        private static void FireTransit(int zone)
        {
            var controller = StationAtlas.LocationController();
            if (controller == null) return;
            string ev = zone == 0 ? "RimTransit" : zone == 1 ? "GreenwayTransit" : "HubTransit";
            _muteUntil = Time.unscaledTime + 3f;
            controller.SendEvent(ev);
            Plugin.Log.LogInfo("[Table] zone transit fired: " + ev);
        }

        private static bool IsZoneRest(string state)
            => state == "Rim" || state == "Greenway" || state == "Hub";

        // ---------- Rows / tabs / cells ----------

        private static void BuildTabs()
        {
            Tabs.Clear();
            int current = StationAtlas.CurrentZone();
            for (int z = 0; z <= 2; z++)
            {
                bool hasRows = _rows.Exists(r => !r.IsCharacter && r.Zone == z);
                if (!hasRows) continue;
                bool reachable = z == current
                    || (z == 0)                                  // Rim: the start zone
                    || (z == 1 && StationAtlas.GreenwayVisited())
                    || (z == 2 && _hubSeen);
                if (reachable) Tabs.Add(z);
            }
            if (_rows.Exists(r => r.IsCharacter)) Tabs.Add(-2);
            BuildDriveRows();
            if (_driveRows.Count > 0) Tabs.Add(-3);
            if (Tabs.Count == 0) Tabs.Add(current >= 0 ? current : 0);
        }

        private static void BuildView()
        {
            int id = Tabs[_tab];
            _view = id == -2
                ? _rows.FindAll(r => r.IsCharacter)
                : _rows.FindAll(r => r.Zone == id); // zone tabs interleave characters (ruling 7)
            _row = Mathf.Clamp(_row, 0, Mathf.Max(0, _view.Count - 1));
        }

        private static void BuildDriveRows()
        {
            _driveRows.Clear();
            try
            {
                foreach (var quest in PixelCrushers.DialogueSystem.QuestLog.GetAllQuests())
                    if (PixelCrushers.DialogueSystem.QuestLog.IsQuestTrackingEnabled(quest))
                        _driveRows.Add(quest);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("[Table] quest list: " + e.Message); }
        }

        private static List<Column> ActiveColumns()
        {
            if (Tabs[_tab] != -2) return Columns;
            var cols = new List<Column>(Columns) { WhereColumn };
            return cols;
        }

        private static StationAtlas.Row Current() => _view[_row];

        private static string NameCell(StationAtlas.Row r)
        {
            var sb = new System.Text.StringBuilder();
            if (r.IsCharacter) sb.Append(W.CharacterPrefix);
            sb.Append(r.Name);
            if (r.IsNew) sb.Append(", ").Append(W.RowNew);
            if (!r.Interactable) sb.Append(", ").Append(W.RowDisabled);
            return sb.ToString();
        }

        private static string CellRaw(Column c) => c.Cell(Current());

        private static string CellText(int colIndex)
        {
            var col = ActiveColumns()[colIndex];
            return CellRaw(col) ?? col.EmptyForm ?? "none";
        }

        /// <summary>Row report: name + flags, then POPULATED facets only (ruling 3 —
        /// the report carries the compression; empty cells stay visitable but silent
        /// here).</summary>
        private static string RowReport()
        {
            var cols = ActiveColumns();
            var sb = new System.Text.StringBuilder(CellRaw(cols[0]));
            sb.Append('.');
            for (int i = 1; i < cols.Count; i++)
            {
                string cell = CellRaw(cols[i]);
                if (cell == null) continue;
                sb.Append(' ').Append(cols[i].Header).Append(' ').Append(cell);
                if (!cell.EndsWith(".")) sb.Append('.');
            }
            return sb.ToString();
        }

        private static string DriveRowReport(string quest)
        {
            var sb = new System.Text.StringBuilder(quest).Append('.');
            try
            {
                int entries = PixelCrushers.DialogueSystem.QuestLog.GetQuestEntryCount(quest);
                for (int i = 1; i <= entries; i++)
                {
                    if (PixelCrushers.DialogueSystem.QuestLog.GetQuestEntryState(quest, i)
                        != PixelCrushers.DialogueSystem.QuestState.Active) continue;
                    string text = PixelCrushers.DialogueSystem.QuestLog.GetQuestEntry(quest, i);
                    if (!string.IsNullOrEmpty(text))
                        sb.Append(' ').Append(SpeechService.Clean(text)).Append('.');
                }
            }
            catch { }
            var places = new List<string>();
            foreach (var r in _rows)
                if (StationAtlas.DriveCell(r).Contains(quest))
                    places.Add(r.Name);
            sb.Append(places.Count > 0 ? " At: " + string.Join(", ", places) + "."
                                       : " " + W.NoObjective);
            return sb.ToString();
        }

        private static int NearestRowToCamera()
        {
            var fsm = StationAtlas.FocusFsm();
            var damped = fsm != null ? fsm.FsmVariables.GetFsmFloat("Damped Z") : null;
            if (damped == null || _view.Count == 0) return 0;
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < _view.Count; i++)
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(_view[i].Angle, damped.Value));
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private static string TabName(int id)
            => id == -2 ? W.TabCharacters : id == -3 ? W.TabDrives : W.Zone(id);
    }
}
