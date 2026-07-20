using System.Collections.Generic;
using CSAccess.Speech;
using CSAccess.Substrate;
using UnityEngine;

namespace CSAccess.Game
{
    /// <summary>Silent value-check bounces spoken with their reason (owner ruling
    /// 2026-07-20). Corpus: cryo and item/value-check cards accept the die into
    /// Slotted, run Value Check (cryo: Player_Bits vs Cryo Cost; item: held count vs
    /// Item Cost), and on failure ForceUnslotDice → Unslot Dice — the die pops back
    /// out with zero feedback. Cloud cipher gates run the identical template, so this
    /// covers both surfaces.
    ///
    /// Clock: Unslot Dice entry with Value Check as the PREVIOUS state — a manual
    /// retraction enters Unslot Dice from elsewhere and must stay silent, so every
    /// state entry for these controllers is tracked and the previous name checked.
    /// Content: the controller's own just-read floats (the game wrote them during the
    /// check) + the rendered Cost Label. Wording provisional.</summary>
    internal static class RefusalWatch
    {
        private struct PendingSay
        {
            public string Text;
            public float Due;
        }

        private static readonly Dictionary<int, string> LastState = new Dictionary<int, string>();
        private static readonly List<PendingSay> Queue = new List<PendingSay>();

        public static void Init()
        {
            FsmSignals.Subscribe("Action Controller", null, OnState);
            FsmSignals.Subscribe("Action Cryo Controller", null, OnState);
        }

        private static void OnState(PlayMakerFSM fsm, string state)
        {
            int id = fsm.GetInstanceID();
            LastState.TryGetValue(id, out string prev);
            LastState[id] = state;
            if (state != "Unslot Dice" || prev != "Value Check") return;

            // Runs inside the game's state switch — compose cheaply, speak from Tick.
            var vars = fsm.FsmVariables;
            string text;
            if (fsm.gameObject.name == "Action Cryo Controller")
            {
                var costLabel = vars.GetFsmString("Cost Label")?.Value?.Trim();
                string cost = !string.IsNullOrEmpty(costLabel)
                    ? costLabel
                    : Mathf.RoundToInt(vars.GetFsmFloat("Cryo Cost")?.Value ?? 0f).ToString();
                int holding = Mathf.RoundToInt(vars.GetFsmFloat("Bits")?.Value ?? 0f);
                text = "Not enough cryo. Costs " + cost + ", holding " + holding + ".";
            }
            else
            {
                int cost = Mathf.RoundToInt(vars.GetFsmFloat("Item Cost")?.Value ?? 0f);
                int holding = Mathf.RoundToInt(vars.GetFsmFloat("Bits")?.Value ?? 0f);
                text = "Missing the required item. Needs " + cost
                    + ", holding " + holding + ".";
            }
            Queue.Add(new PendingSay { Text = text, Due = Time.unscaledTime + 0.1f });
        }

        public static void Tick()
        {
            for (int i = Queue.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < Queue[i].Due) continue;
                SpeechService.Say(Queue[i].Text, Priority.Queued, "refusal");
                Queue.RemoveAt(i);
            }
            if (LastState.Count > 400) LastState.Clear();
        }
    }
}
