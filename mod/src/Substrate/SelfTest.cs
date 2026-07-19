using UnityEngine;

namespace CSAccess.Substrate
{
    /// <summary>
    /// W1 substrate liveness check — log-only, no speech, no game-state writes.
    /// Retries until the Lua database is up, then logs one snapshot of every
    /// allowlisted value plus hook liveness, and a second snapshot on the first
    /// FsmSignals dispatch. This is the evidence the next-launch verification pass
    /// reads from BepInEx/LogOutput.log (invariant 6: nothing rides these adapters
    /// until the log shows them answering).
    /// </summary>
    internal static class SelfTest
    {
        private static bool _luaDone;
        private static bool _signalsDone;
        private static float _nextTry;

        public static void Tick()
        {
            if ((_luaDone && _signalsDone) || Time.unscaledTime < _nextTry) return;
            _nextTry = Time.unscaledTime + 5f;

            if (!_luaDone)
            {
                int? cycle = LuaStore.CycleNumber();
                if (cycle != null)
                {
                    _luaDone = true;
                    Plugin.Log.LogInfo("[Substrate] Lua adapter up. cycle=" + cycle
                        + " energy=" + Fmt(LuaStore.Energy())
                        + " condition=" + Fmt(LuaStore.Condition())
                        + " bits=" + Fmt(LuaStore.Bits())
                        + " upgradePoints=" + Fmt(LuaStore.UpgradePoints())
                        + " drivePoints=" + Fmt(LuaStore.DrivePoints())
                        + " class=" + (LuaStore.PlayerClass() ?? "?")
                        + " skills(END/ENG/ENGR/INT/INTU)="
                        + Fmt(LuaStore.SkillValue(LuaStore.Skill.Endure)) + "/"
                        + Fmt(LuaStore.SkillValue(LuaStore.Skill.Engage)) + "/"
                        + Fmt(LuaStore.SkillValue(LuaStore.Skill.Engineer)) + "/"
                        + Fmt(LuaStore.SkillValue(LuaStore.Skill.Interface)) + "/"
                        + Fmt(LuaStore.SkillValue(LuaStore.Skill.Intuit))
                        + " introComplete=" + LuaClocks.IntroComplete());

                    // Anchor probe rides the same one-shot: misses self-log inside Anchors.
                    Plugin.Log.LogInfo("[Substrate] Anchors: activeAction=" + Name(Anchors.ActiveAction())
                        + " dialoguePanel=" + Name(Anchors.DialoguePanel())
                        + " responseMenu=" + Name(Anchors.ResponseMenu())
                        + " leaveButton=" + Name(Anchors.LeaveButton())
                        + " saver=" + Name(Anchors.Saver())
                        + " uiSelector=" + Name(Anchors.UISelector()));
                }
            }

            if (!_signalsDone && FsmSignals.EntryCount > 0)
            {
                _signalsDone = true;
                Plugin.Log.LogInfo("[Substrate] FsmSignals hook alive: " + FsmSignals.EntryCount
                    + " state entries observed.");
            }
        }

        private static string Fmt(int? v) => v?.ToString() ?? "?";
        private static string Name(GameObject go) => go != null ? go.name : "null";
    }
}
