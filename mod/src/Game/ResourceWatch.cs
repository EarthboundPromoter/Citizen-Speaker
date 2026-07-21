using System.Collections.Generic;
using CSAccess.Speech;
using CSAccess.Substrate;
using UnityEngine;

namespace CSAccess.Game
{
    /// <summary>Ambient cryo / inventory / points lane (A4 audit, owner architecture
    /// ruling 2026-07-21): the outcome composer only voices what renders as effect
    /// lines, so every resolution-time debit (the Lowend toll's silent 60 cryo),
    /// RNG-resolved grant, drive-completion point, and dialogue-driven Lua write was
    /// silent. This watch is the BASE truth for resource changes; the interaction
    /// paths (outcome effect lines, character-table purchase compose) stay the
    /// preferred voice and this lane stands down when they spoke.
    ///
    /// Machinery (corpus): the game ships its own change-watchers — 24 item
    /// "* Manager" FSMs plus "Cryo Chits Manager" under Bottom UI/Inventory/
    /// Inventory Data, each polling its source (Player_Bits / INV_*) into its own
    /// "Item Amount" float every frame and passing through an "Updating Inventory"
    /// state on every change. We subscribe those states and read the manager's own
    /// float; the item label is the manager's object name. Points have no
    /// always-active render loop (character-window FSMs sleep while it is closed),
    /// so Player_UpgradePoints / Player_DrivePoints poll Lua directly at 0.5 s.
    ///
    /// Channel rules: first sight is a silent baseline; announces hold ~1.2 s and
    /// are dropped when the interaction lane already voiced the change (effect-line
    /// stamp from ComposeEffect; purchase-compose stamp from CharacterTable).
    /// Wording provisional (owner calibration).</summary>
    internal static class ResourceWatch
    {
        private struct PendingSay
        {
            public string Subject;   // upper-case match key for the suppression check
            public string Text;
            public float Due;
        }

        private static readonly Dictionary<string, float> Last =
            new Dictionary<string, float>();
        private static readonly List<PendingSay> Queue = new List<PendingSay>();

        private static float _pointsPollAt;
        private static int _upgradePoints = int.MinValue;
        private static int _drivePoints = int.MinValue;

        public static void Init()
        {
            FsmSignals.Subscribe(null, "Updating Inventory", OnManagerUpdate);
            FsmSignals.Subscribe(null, "Updating Inventory 2", OnManagerUpdate);
        }

        private static void OnManagerUpdate(PlayMakerFSM fsm, string state)
        {
            string owner = fsm.gameObject.name;
            if (!owner.EndsWith(" Manager")) return;
            var parent = fsm.transform.parent;
            if (parent == null || parent.name != "Inventory Data") return;

            float value = fsm.FsmVariables.GetFsmFloat("Item Amount")?.Value ?? float.NaN;
            if (float.IsNaN(value)) return;

            bool known = Last.TryGetValue(owner, out float was);
            Last[owner] = value;
            if (!known) return; // baseline (boot / first render), silent
            float delta = value - was;
            if (delta == 0f) return;

            string label = owner == "Cryo Chits Manager"
                ? "Cryo"
                : owner.Substring(0, owner.Length - " Manager".Length);
            string sign = delta > 0 ? "plus" : "minus";
            int amount = Mathf.RoundToInt(Mathf.Abs(delta));
            Queue.Add(new PendingSay
            {
                Subject = label.ToUpperInvariant(),
                Text = label + " " + sign + " " + amount + ", now " + Mathf.RoundToInt(value) + ".",
                Due = Time.unscaledTime + 1.2f,
            });
        }

        public static void Tick()
        {
            PollPoints();
            for (int i = Queue.Count - 1; i >= 0; i--)
            {
                var p = Queue[i];
                if (Time.unscaledTime < p.Due) continue;
                Queue.RemoveAt(i);
                if (InteractionLaneSpoke(p.Subject))
                {
                    Plugin.Log.LogInfo("[ResourceWatch] suppressed (interaction lane spoke): "
                        + p.Text);
                    continue;
                }
                SpeechService.Say(p.Text, Priority.Queued, "resource");
            }
        }

        /// <summary>The stand-down check: an outcome effect line naming this resource,
        /// or the character-table purchase compose (points), spoke just now.</summary>
        private static bool InteractionLaneSpoke(string subjectUpper)
        {
            if (subjectUpper == "UPGRADE POINTS" || subjectUpper == "DRIVE POINTS")
                return Time.unscaledTime - UI.CharacterTable.LastPurchaseSpokeAt < 2.5f;
            if (Time.unscaledTime - Watchers.RecentEffectBodiesAt > 2.5f) return false;
            foreach (var body in Watchers.RecentEffectBodies)
                if (body.Contains(subjectUpper)) return true;
            return false;
        }

        private static void PollPoints()
        {
            if (Time.unscaledTime < _pointsPollAt) return;
            _pointsPollAt = Time.unscaledTime + 0.5f;
            PollPoint(Substrate.LuaStore.UpgradePoints(), ref _upgradePoints, "Upgrade points",
                "UPGRADE POINTS");
            PollPoint(Substrate.LuaStore.DrivePoints(), ref _drivePoints, "Drive points",
                "DRIVE POINTS");
        }

        private static void PollPoint(int? current, ref int last, string label, string subject)
        {
            if (current == null) return;
            if (last == int.MinValue) { last = current.Value; return; } // baseline
            int delta = current.Value - last;
            if (delta == 0) return;
            last = current.Value;
            string sign = delta > 0 ? "plus" : "minus";
            Queue.Add(new PendingSay
            {
                Subject = subject,
                Text = label + " " + sign + " " + Mathf.Abs(delta) + ", now " + current.Value + ".",
                Due = Time.unscaledTime + 1.2f,
            });
        }
    }
}
