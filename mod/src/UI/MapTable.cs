using System.Collections.Generic;
using CSAccess.Game;
using CSAccess.Speech;
using UnityEngine;

namespace CSAccess.UI
{
    /// <summary>
    /// The ZONE table (owner redesign 2026-07-22, replacing the tabbed station map
    /// table): ONE table per zone, loaded when the player takes a zone transition
    /// themselves (ferry, ascender/descender, tolls). No tabs, no slash — the tab
    /// machinery died with the redesign; the character and tracked-drives tabs died
    /// with it (characters interleave in corridor order — the game converts them
    /// in place over locations; drives read from the Drives column and the journal).
    /// Titles are the bare region name. Rows = rendered locations/characters of the
    /// CURRENT zone in corridor order, columns from a registry, stable geometry with
    /// terse empties, row report on vertical movement, header+cell on horizontal.
    ///
    /// Camera contract (owner ruling 2026-07-22): browsing writes the camera ONCE
    /// per row change, to the row's calibrated angle — never on rebuilds, folds, or
    /// position restores (the fold-time re-assert fought every native camera move —
    /// the "jitter in place" ride finding). Zone membership comes from
    /// StationAtlas.ResolveZone: calibration-baked seed + live frustum stamping,
    /// geometry only as last resort (zone is NOT authored anywhere in the game —
    /// corpus Z1). The Hub is a fixed single view (live finding: no clamp, no
    /// scroll; every available marker in frustum at once) — no camera writes there.
    ///
    /// D3 (owner ruling 2026-07-20) still holds: the table IS station navigation,
    /// permanently — no open/close state. Overlays (windows, dialogue, dice)
    /// suspend it with position intact and no re-announcement (ModeModel.Surface);
    /// a genuine surface entry rebuilds and announces.
    /// </summary>
    internal static class MapTable
    {
        // ---------- Wording block (ALL table phrases; owner calibration lands here) ----------
        private static class W
        {
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
            public const string NoRows = "Nothing here.";
            public const string CommitDisabled = "Not open yet.";
            public static string Zone(int z) => z == 0 ? "The Rim" : z == 1 ? "Greenway" : z == 2 ? "The Hub" : "Station";
        }

        // ---------- Column registry (ruling: build flexibly — one edit adds a column) ----------
        private sealed class Column
        {
            public string Header;
            public System.Func<StationAtlas.Row, string> Cell; // null result = empty cell
            public string EmptyForm;
        }

        // Description column scrapped (owner, live calibration 2026-07-20): the
        // tagline rides the Name cell instead.
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
        };

        // ---------- State ----------

        /// <summary>The permanent-nav gate (D3): table keys route whenever we are at
        /// the station surface and the Ctrl+X escape hatch is off.</summary>
        public static bool Active()
            => !Modality.NavIdiom.Native
               && Modality.ModeModel.Current() == Modality.Mode.Station;

        private static List<StationAtlas.Row> _rows = new List<StationAtlas.Row>();
        private static int _row, _col;
        private static List<StationAtlas.Row> _view = new List<StationAtlas.Row>();
        /// <summary>The zone this table is built for — rebuilt + re-announced when the
        /// player's own transition settles (the ONLY way zones change now).</summary>
        private static int _zone = -1;

        // Camera settle machinery (CloudFlight pattern: mute game focus, no announce —
        // the row report IS the announcement; the highlight lands silently).
        private static float _muteUntil = -1f;
        private static StationAtlas.Row _cameraTarget;

        public static bool SuppressingFocus()
            => Active() || Time.unscaledTime < _muteUntil;

        // ---------- Surface entry (D3: rebuild + announce on a genuine arrival) ----------
        // Overlay excursions (windows, dialogue, dice picker) never pass through
        // here — ModeModel.Surface holds Station across them, so position and rows
        // survive and the return is silent. A real arrival (from a location, the
        // cloud, a cycle transition, or boot) rebuilds and announces. U/O/I/J and
        // query keys pass through regardless (HandleKeys never consumes them).

        private static Modality.Mode _prevSurface = Modality.Mode.Title;
        private static float _surfaceAt;
        private static bool _entered;
        private static bool _emptyAtlasLogged;

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
            if (surface != Modality.Mode.Station || _entered || Modality.NavIdiom.Native)
                return;
            // Settle debounce: transitional mode flickers (flights, teardown)
            // must not announce mid-flight.
            if (Time.unscaledTime - _surfaceAt < 0.6f) return;
            _entered = true;
            EnterStation();
        }

        private static void EnterStation()
        {
            try
            {
                _rows = StationAtlas.Build();
                // Boot finding (session 11 live): the mode authority reads Station
                // during the save-load window, before the scene's containers exist —
                // an empty atlas means "not arrived yet", never "empty station".
                // Un-claim the entry and re-debounce until rows exist.
                if (_rows.Count == 0)
                {
                    _entered = false;
                    _surfaceAt = Time.unscaledTime;
                    if (!_emptyAtlasLogged)
                    {
                        _emptyAtlasLogged = true;
                        Plugin.Log.LogInfo("[Table] station entry deferred: atlas empty (scene still loading).");
                    }
                    return;
                }
                _emptyAtlasLogged = false;
                _zone = StationAtlas.CurrentZone();
                BuildView();
                _row = NearestRowToCamera();
                _col = 0;
                // Title = the bare region name (owner ruling 2026-07-22).
                SpeechService.Say(W.Zone(_zone) + ". "
                    + (_view.Count > 0 ? RowReport() : W.NoRows),
                    Priority.Immediate, "table");
                // A genuine station arrival is a census beat: changes that landed
                // while away (cloud, cycle turnover, a location) speak here, queued
                // behind the entry announce — the return stays silent EXCEPT when
                // the world changed underneath.
                StationCensus.OnBeat();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("[Table] station entry failed: " + e);
                SpeechService.Say("Table unavailable.", Priority.Immediate, "table");
            }
        }

        /// <summary>Ctrl+X return path: re-anchor to the camera and re-announce —
        /// native browsing moved the world under us, so a fresh build is the honest
        /// position.</summary>
        public static void AnnouncePosition()
        {
            _entered = true;
            EnterStation();
        }

        // ---------- Periodic freshness (owner ruling, session 11): story spawns land
        // mid-visit (Character: SABINE appeared while browsing and stayed invisible
        // until the next surface change). A regular silent diff folds the world in;
        // the current row survives by identity. Spoken appearance callouts remain the
        // BL-16 census redesign's job.
        private const float RefreshInterval = 2.0f;
        private static float _nextRefresh;

        // D2 oscillation memory: a row that vanishes is held this long so a blink
        // back reclaims the cursor (spans a couple of refresh cycles).
        private const float OscillationWindow = 4.0f;
        private static string _departedName;
        private static bool _departedIsChar;
        private static float _departedAt;

        private static void RefreshTick()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            FoldFresh();
        }

        /// <summary>Rebuild the atlas and fold it in, preserving the current row by
        /// identity. Shared by the background tick and the per-keypress path; an
        /// empty scan never degrades the table.</summary>
        private static void FoldFresh()
        {
            if (!_entered || Modality.NavIdiom.Native) return;
            if (Modality.ModeModel.Surface() != Modality.Mode.Station) return;
            try
            {
                var fresh = StationAtlas.Build();
                if (fresh.Count == 0) return; // scene hiccup — never degrade to a husk
                if (!RowsDiffer(fresh)) return;

                string keepName = null;
                bool keepIsChar = false;
                if (_view.Count > 0 && _row < _view.Count)
                { keepName = _view[_row].Name; keepIsChar = _view[_row].IsCharacter; }

                int before = _rows.Count;
                _rows = fresh;
                BuildView();
                if (keepName != null)
                {
                    int found = _view.FindIndex(r => r.Name == keepName && r.IsCharacter == keepIsChar);
                    if (found >= 0) _row = found;
                    else
                    {
                        // D2: the row we were on vanished this fold. Oscillating
                        // canvases (a billboard walking scene states, 10->9->10)
                        // blink out and back within seconds — remember the departed
                        // identity and hold the index rather than ceding position
                        // permanently to a neighbor.
                        _departedName = keepName;
                        _departedIsChar = keepIsChar;
                        _departedAt = Time.unscaledTime;
                        _row = Mathf.Clamp(_row, 0, Mathf.Max(0, _view.Count - 1));
                    }
                }

                // D2: a recently-departed row that reappears reclaims the cursor.
                if (_departedName != null)
                {
                    if (Time.unscaledTime - _departedAt < OscillationWindow)
                    {
                        int back = _view.FindIndex(r =>
                            r.Name == _departedName && r.IsCharacter == _departedIsChar);
                        if (back >= 0) { _row = back; _departedName = null; }
                    }
                    else _departedName = null; // window expired: a genuine removal
                }
                Plugin.Log.LogInfo("[Table] refresh: rows " + before + " -> " + _rows.Count
                    + " (position " + (keepName ?? "start") + ")");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("[Table] refresh failed: " + e.Message);
            }
        }

        private static bool RowsDiffer(List<StationAtlas.Row> fresh)
        {
            if (fresh.Count != _rows.Count) return true;
            for (int i = 0; i < fresh.Count; i++)
                if (fresh[i].Name != _rows[i].Name || fresh[i].IsCharacter != _rows[i].IsCharacter
                    || fresh[i].Zone != _rows[i].Zone)
                    return true;
            return false;
        }

        // ---------- Input (routed from InputManager at the station surface) ----------

        public static bool HandleKeys()
        {
            if (_zone < 0) return false; // entry not settled yet

            // Per-keypress freshness (owner ruling, session 11): every table key
            // operates on a build made at this instant (cloud parity) — the fold
            // re-anchors the current row by identity first, so the delta below
            // applies to the live world. Gated on an actual keypress, never per
            // frame (Build allocates; per-frame would be pure GC churn).
            // Slash freed (owner redesign 2026-07-22: tabs are dead).
            bool tableKey = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow)
                || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.LeftArrow)
                || Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (!tableKey) return false;
            FoldFresh();

            if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveRow(1); return true; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveRow(-1); return true; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { MoveCol(1); return true; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { MoveCol(-1); return true; }
            if (Input.GetKeyDown(KeyCode.Space)) { SpeakFullRow(); return true; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            { Commit(); return true; }
            return false;
        }

        public static void Tick()
        {
            EntryTick();
            RefreshTick();
            TickPendingCommit();
            ZoneWatch();
        }

        /// <summary>Player-taken zone transitions load the new zone's table (owner
        /// redesign 2026-07-22): watch Lua LOCATION, and when it changes AND the
        /// Location Controller has settled at a zone rest (its own Transition-to-*
        /// states are the in-flight signal), rebuild for the new zone and announce
        /// the region name. The camera is NOT written here — the game's transit
        /// lands it at the authored point (ferry 275, Hub->Rim snap 240) and the
        /// table adopts position from it instead (NearestRowToCamera).</summary>
        private static void ZoneWatch()
        {
            if (!_entered || Modality.NavIdiom.Native) return;
            if (Modality.ModeModel.Surface() != Modality.Mode.Station) return;
            int now = StationAtlas.CurrentZone();
            if (now < 0 || now == _zone) return;
            var controller = StationAtlas.LocationController();
            if (controller == null || !IsZoneRest(controller.ActiveStateName)) return;
            _zone = now;
            _pendingCommit = null;
            _rows = StationAtlas.Build();
            BuildView();
            _row = NearestRowToCamera();
            _col = 0;
            SpeechService.Say(W.Zone(_zone) + ". "
                + (_view.Count > 0 ? RowReport() : W.NoRows),
                Priority.Queued, "table");
            // Arrival in a zone is a census beat (changes that landed while away
            // speak behind the region announce).
            StationCensus.OnBeat();
            Plugin.Log.LogInfo("[Table] zone table loaded: " + W.Zone(_zone)
                + " (" + _view.Count + " rows)");
        }

        // ---------- Movement ----------

        private static void MoveRow(int delta)
        {
            // Moving on withdraws a pending commit (session-11 live: the 2 s timeout
            // refusal spoke rows later, and a late-enabling target would have clicked
            // a row the player had left).
            _pendingCommit = null;
            if (_view.Count == 0)
            { SpeechService.Say(W.NoRows, Priority.Immediate, "table"); return; }

            int prev = _row;
            _row = Mathf.Clamp(_row + delta, 0, _view.Count - 1);
            SpeechService.Say(_col == 0 ? RowReport() : Current().Name + ". " + CellText(_col),
                Priority.Immediate, "table");
            // Camera contract: write ONLY when the row actually changed (edge
            // bare-repeats never re-write; nothing else in the table writes at all).
            if (_row != prev) CameraTo(Current());
        }

        private static void MoveCol(int delta)
        {
            if (_view.Count == 0) return;
            var cols = Columns;
            _col = Mathf.Clamp(_col + delta, 0, cols.Count - 1);
            SpeechService.Say(cols[_col].Header + ": " + CellText(_col), Priority.Immediate, "table");
        }

        private static void SpeakFullRow()
        {
            if (_view.Count == 0) return;
            var cols = Columns;
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
            if (_view.Count == 0) return;
            var row = Current();
            if (LiveInteractable(row))
            {
                Navigator.Click(row.Button);
                return;
            }
            // Camera may still be in flight to this row — wait for its button to
            // enable rather than refusing off stale state; refuse only on timeout
            // (genuinely locked markers never enable).
            _pendingCommit = row;
            _pendingCommitUntil = Time.unscaledTime + 2f;
        }

        private static StationAtlas.Row _pendingCommit;
        private static float _pendingCommitUntil;

        private static void TickPendingCommit()
        {
            if (_pendingCommit == null) return;
            if (!Active()) { _pendingCommit = null; return; }
            if (LiveInteractable(_pendingCommit))
            {
                var row = _pendingCommit;
                _pendingCommit = null;
                Navigator.Click(row.Button);
                return;
            }
            if (Time.unscaledTime > _pendingCommitUntil)
            {
                SpeechService.Say(_pendingCommit.Name + ": " + W.CommitDisabled,
                    Priority.Immediate, "table");
                _pendingCommit = null;
            }
        }

        // ---------- Camera ----------

        private static void CameraTo(StationAtlas.Row row)
        {
            if (!Plugin.MapTableCamera.Value || row == null) return;
            // The Hub is a fixed single view (live 2026-07-22: no clamp, no scroll
            // input, every available marker in frustum at once) — never write there.
            if (_zone == 2) return;
            _cameraTarget = row;
            WriteAngle(StationAtlas.UnwrapForZone(_zone, row.Angle));
        }

        /// <summary>Camera write for other tables riding the same one-axis rig — the
        /// cloud table (owner live-confirmed 2026-07-20: W/S pans the cloud corridor
        /// identically, same Focus rig). Shares the config gate and the focus mute.</summary>
        public static void CameraToAngle(float angle)
        {
            if (!Plugin.MapTableCamera.Value) return;
            WriteAngle(angle);
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

        private static bool IsZoneRest(string state)
            => state == "Rim" || state == "Greenway" || state == "Hub";

        // ---------- Rows / cells ----------

        /// <summary>The view = the current zone's rows, characters interleaved in
        /// corridor order (owner ruling 2026-07-22: the game converts characters in
        /// place over locations, so they are ordinary rows).</summary>
        private static void BuildView()
        {
            _view = _rows.FindAll(r => r.Zone == _zone);
            _row = Mathf.Clamp(_row, 0, Mathf.Max(0, _view.Count - 1));
        }

        private static StationAtlas.Row Current() => _view[_row];

        private static string NameCell(StationAtlas.Row r)
        {
            var sb = new System.Text.StringBuilder();
            if (r.IsCharacter) sb.Append(W.CharacterPrefix);
            sb.Append(r.Name);
            if (r.IsNew) sb.Append(", ").Append(W.RowNew);
            // Disabled speaks ONLY when the marker is currently rendered and refuses
            // (live read — the open-time cache falsely flagged every off-camera row;
            // owner report 2026-07-20). Off-camera rows say nothing: no rendered truth
            // yet, and camera-follow enables them by the time Enter matters.
            if (r.Button != null && r.Button.activeInHierarchy && !LiveInteractable(r))
                sb.Append(", ").Append(W.RowDisabled);
            if (!string.IsNullOrEmpty(r.Tagline))
                sb.Append(". ").Append(r.Tagline);
            return sb.ToString();
        }

        private static bool LiveInteractable(StationAtlas.Row r)
        {
            var s = r.Button != null ? r.Button.GetComponent<UnityEngine.UI.Selectable>() : null;
            return s != null && r.Button.activeInHierarchy && s.IsInteractable();
        }

        private static string CellRaw(Column c) => c.Cell(Current());

        private static string CellText(int colIndex)
        {
            var col = Columns[colIndex];
            return CellRaw(col) ?? col.EmptyForm ?? "none";
        }

        /// <summary>Row report: name + flags, then POPULATED facets only (ruling 3 —
        /// the report carries the compression; empty cells stay visitable but silent
        /// here).</summary>
        private static string RowReport()
        {
            var cols = Columns;
            var sb = new System.Text.StringBuilder(CellRaw(cols[0]));
            sb.Append('.');
            for (int i = 1; i < cols.Count; i++)
            {
                string cell = CellRaw(cols[i]);
                if (cell == null) continue;
                // C3: zero counts are not populated facets ("Actions 0 actions.")
                // — the column visit's terse empty covers them (row rule: report
                // carries the compression).
                if (cell.StartsWith("0 ")) continue;
                sb.Append(' ').Append(cols[i].Header).Append(' ').Append(cell);
                if (!cell.EndsWith(".")) sb.Append('.');
            }
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
    }
}
