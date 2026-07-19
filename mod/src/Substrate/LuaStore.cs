using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace CSAccess.Substrate
{
    /// <summary>
    /// Read-only adapter over the Dialogue System Lua database — the game's single
    /// authoritative store for persistent player state (verification brief F, LUA STORE).
    ///
    /// RENDER-PAIRING ALLOWLIST (binding, build-plan W1): this class exposes ONLY
    /// variables with a documented render pairing — somewhere the game shows the value
    /// to the player (rendered = player-reachable in-game, per owner ruling). Each getter
    /// cites its pairing. The other ~390 Lua variables are hidden story state and are
    /// never spoken; there is deliberately no generic Get(string) here.
    ///
    /// Clock-tier variables (gate behavior, never spoken) live in <see cref="LuaClocks"/>.
    /// </summary>
    internal static class LuaStore
    {
        // ---------- Player stats ----------

        /// <summary>Render pairing: HUD energy bar — Top UI/Energy UI/Energy Bar System
        /// reads and writes this Lua variable to drive the rendered meter (brief F).</summary>
        public static int? Energy() => Num("Player_Energy");

        /// <summary>Render pairing: HUD condition bar + band word (STABLE/FADING/...) —
        /// Top UI/Energy UI/Condition System (brief F).</summary>
        public static int? Condition() => Num("Player_Condition");

        /// <summary>Render pairing: the cryo amount rendered at
        /// Bottom UI/Inventory/ITEM Inventory UI/Cryo Slot /Amount (corpus reader trace).</summary>
        public static int? Bits() => Num("Player_Bits");

        /// <summary>Render pairing: character window skill list header
        /// (CSUI_SKILL_LIST_UPGRADES_AVAILABLE) + points readout, live-validated
        /// session 3.</summary>
        public static int? UpgradePoints() => Num("Player_UpgradePoints");

        /// <summary>Render pairing: Character Window/Upgrade Tracker/Drives Completed
        /// (corpus reader trace).</summary>
        public static int? DrivePoints() => Num("Player_DrivePoints");

        /// <summary>Render pairing: class name rendered by Character UI Button/Class Name
        /// and Character Window/Character Portrait (corpus reader trace).</summary>
        public static string PlayerClass() => Str("Player_Class");

        // ---------- Cycle ----------

        /// <summary>The live cycle number. Lua float `Cycle`, single writer Cycle
        /// Controller, read by every clock FSM (brief F cycle verdict). Render pairing:
        /// player-reachable via the save-slot label's class-and-cycle line (owner ruling:
        /// rendered = reachable, not necessarily on-screen this instant; build-plan G#3).
        /// Distinct from the save-slot menu's own string mechanism — don't conflate.</summary>
        public static int? CycleNumber() => Num("Cycle");

        // ---------- Skills ----------

        public enum Skill { Endure, Engage, Engineer, Interface, Intuit }

        private static readonly string[] SkillVars =
            { "ENDURE", "ENGAGE", "ENGINEER", "INTERFACE", "INTUIT" };

        /// <summary>The skill's value — the same number whose bucket (-1/0/+1/+2) the
        /// character window renders as the highlighted modifier row
        /// (Character Window/SKILL List/&lt;SKILL&gt;, corpus reader trace; build-plan G#2 —
        /// this read retires the modifier color heuristic).</summary>
        public static int? SkillValue(Skill skill) => Num(SkillVars[(int)skill]);

        /// <summary>The rendered modifier bucket (-1/0/+1/+2) for a skill, using the game's
        /// own Get Skill Rating mapping: FloatSwitch lessThan [0,1,2,3] → [-1,0,+1,+2]
        /// (character_window_fsm.jsonl, brief G#2). Render pairing: the highlighted cell of
        /// the action-card modifier row and the character window's skill row both render
        /// this bucket. Null when the variable is unavailable — or ≥3, where the game's
        /// FloatSwitch fires no event and the row would not highlight (mirrored, logged).</summary>
        public static string SkillModifier(Skill skill)
        {
            string var = SkillVars[(int)skill];
            try
            {
                var r = DialogueLua.GetVariable(var);
                if (!r.hasReturnValue || !r.isNumber) return null;
                float v = r.asFloat;
                if (v < 0f) return "-1";
                if (v < 1f) return "0";
                if (v < 2f) return "+1";
                if (v < 3f) return "+2";
                Plugin.Log.LogWarning("[Substrate] " + var + "=" + v
                    + " is outside the game's FloatSwitch range; no bucket spoken.");
                return null;
            }
            catch (System.Exception e) { LogFaultOnce(var, e); return null; }
        }

        /// <summary>Bucket lookup by the rendered skill word (action cards render the exact
        /// Lua variable names); null for a non-skill word.</summary>
        public static string SkillModifierForWord(string skillWord)
        {
            int idx = System.Array.IndexOf(SkillVars, skillWord);
            return idx >= 0 ? SkillModifier((Skill)idx) : null;
        }

        // ---------- Plumbing ----------

        private static bool _faultLogged;

        /// <summary>Presence-aware numeric read: null means "not available" (no database
        /// yet, or the variable is unset) — callers must distinguish that from a real 0.</summary>
        private static int? Num(string name)
        {
            try
            {
                var r = DialogueLua.GetVariable(name);
                if (!r.hasReturnValue || !r.isNumber) return null;
                return Mathf.RoundToInt(r.asFloat);
            }
            catch (System.Exception e) { LogFaultOnce(name, e); return null; }
        }

        private static string Str(string name)
        {
            try
            {
                var r = DialogueLua.GetVariable(name);
                if (!r.hasReturnValue || !r.isString) return null;
                string s = r.asString;
                return string.IsNullOrEmpty(s) || s == "nil" ? null : s;
            }
            catch (System.Exception e) { LogFaultOnce(name, e); return null; }
        }

        private static void LogFaultOnce(string name, System.Exception e)
        {
            if (_faultLogged) return;
            _faultLogged = true;
            Plugin.Log.LogWarning("[Substrate] Lua read failed (" + name + "): " + e.Message);
        }
    }

    /// <summary>
    /// Clock-tier Lua reads: these GATE behavior, they are never spoken (binding
    /// invariant 1 — signals are clocks, never content). Kept in a separate type so a
    /// speakable value can't be pulled from here by accident.
    /// </summary>
    internal static class LuaClocks
    {
        /// <summary>Intro gating flag (brief F correction: Lua, not a PlayMaker global).</summary>
        public static bool IntroComplete()
        {
            try
            {
                var r = DialogueLua.GetVariable("IntroComplete");
                return r.hasReturnValue && r.isNumber ? r.asFloat >= 1f
                     : r.hasReturnValue && r.isBool && r.asBool;
            }
            catch { return false; }
        }

        /// <summary>Breakdown scheduling value (feeds Cycle Controller's Breakdown? local,
        /// brief F). Gate-only; the rendered breakdown experience speaks for itself.</summary>
        public static int? BreakdownCycle()
        {
            try
            {
                var r = DialogueLua.GetVariable("BREAKDOWN_CYCLE");
                if (!r.hasReturnValue || !r.isNumber) return null;
                return Mathf.RoundToInt(r.asFloat);
            }
            catch { return null; }
        }
    }
}
