using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HutongGames.PlayMaker;
using UnityEngine;

namespace CSAccessBridge
{
    /// <summary>One-shot FSM structure census. The offline typetree parse fails on every
    /// PlayMakerFSM blob in this build, so the running game is the only faithful parser:
    /// PlayMaker has already deserialized states, actions, and parameters into memory.
    /// Writes one JSON line per FSM for offline analysis (the "corpus" of build-plan.md
    /// section 5). Observation-only: reads structures, changes nothing.
    ///
    /// WARNING: action parameters embed narrative strings (outcome texts, dialogue refs) —
    /// the output file is SPOILER-LADEN and must never be read aloud or quoted to the
    /// mod's owner. Analysis reports from it carry structural identifiers only.</summary>
    internal static class FsmCensus
    {
        private const int MaxString = 200;
        private const int MaxItems = 64;

        public static object Run(string file)
        {
            string path = string.IsNullOrEmpty(file)
                ? Path.Combine(BepInEx.Paths.GameRootPath, "fsm-census.jsonl")
                : file;
            // FsmList only holds awakened FSMs; objects inactive since scene load (dice
            // cursors, locked locations, unopened surfaces) never register there. Sweep ALL
            // loaded scene FSMs instead — reading FsmStates forces PlayMaker's lazy
            // deserialization without running the machine.
            int count = 0, errors = 0, inactive = 0;
            var seen = new HashSet<int>();
            using (var w = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                {
                    if (fsm == null || fsm.gameObject == null) continue;
                    if (!fsm.gameObject.scene.IsValid()) continue; // skip prefabs/assets
                    if (!seen.Add(fsm.GetInstanceID())) continue;
                    if (!fsm.gameObject.activeInHierarchy) inactive++;
                    try
                    {
                        w.WriteLine(Json.Serialize(Describe(fsm)));
                        count++;
                    }
                    catch (Exception)
                    {
                        errors++;
                    }
                }
            }
            return new Dictionary<string, object>
            {
                ["file"] = path,
                ["fsms"] = count,
                ["inactiveIncluded"] = inactive,
                ["errors"] = errors,
            };
        }

        private static object Describe(PlayMakerFSM fsm)
        {
            var states = new List<object>();
            foreach (var s in fsm.FsmStates)
            {
                var transitions = new List<object>();
                foreach (var tr in s.Transitions)
                    transitions.Add(tr.EventName + " -> " + tr.ToState);
                var actions = new List<object>();
                foreach (var a in s.Actions)
                {
                    if (a == null) { actions.Add(null); continue; }
                    actions.Add(new Dictionary<string, object>
                    {
                        ["type"] = a.GetType().Name,
                        ["params"] = ActionParams(a),
                    });
                }
                states.Add(new Dictionary<string, object>
                {
                    ["name"] = s.Name,
                    ["transitions"] = transitions,
                    ["actions"] = actions,
                });
            }

            var globals = new List<object>();
            foreach (var tr in fsm.FsmGlobalTransitions)
                globals.Add(tr.EventName + " -> " + tr.ToState);

            var events = new List<object>();
            foreach (var e in fsm.FsmEvents) events.Add(e.Name);

            var vars = new Dictionary<string, object>();
            foreach (var v in fsm.FsmVariables.GetAllNamedVariables())
                vars[v.Name] = v.VariableType.ToString();

            return new Dictionary<string, object>
            {
                ["path"] = UiQuery.PathOf(fsm.gameObject),
                ["active"] = fsm.gameObject.activeInHierarchy,
                ["fsm"] = fsm.FsmName,
                ["startState"] = fsm.Fsm != null ? fsm.Fsm.StartState : null,
                ["states"] = states,
                ["globalTransitions"] = globals,
                ["events"] = events,
                ["variables"] = vars,
            };
        }

        private static object ActionParams(FsmStateAction action)
        {
            var result = new Dictionary<string, object>();
            foreach (var f in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object val;
                try { val = f.GetValue(action); }
                catch { continue; }
                try { result[f.Name] = Render(val, 0); }
                catch { result[f.Name] = "<err>"; }
            }
            return result;
        }

        /// <summary>Compact, analysis-oriented rendering of a PlayMaker action parameter.
        /// Variables render as $Name so wiring is greppable; literals render as values.</summary>
        private static object Render(object val, int depth)
        {
            if (val == null || depth > 3) return null;

            if (val is NamedVariable nv)
                return nv.UsesVariable ? "$" + nv.Name : LiteralOf(nv);
            if (val is FsmEvent ev)
                return "event:" + ev.Name;
            if (val is FsmOwnerDefault od)
                return od.OwnerOption == OwnerDefaultOption.UseOwner
                    ? "owner"
                    : Render(od.GameObject, depth + 1);
            if (val is FsmEventTarget target)
                return "target:" + target.target +
                       (target.gameObject != null ? "/" + Render(target.gameObject, depth + 1) : "") +
                       (target.fsmName != null && !string.IsNullOrEmpty(target.fsmName.Value)
                           ? "#" + target.fsmName.Value : "");
            if (val is GameObject go)
                return "go:" + go.name;
            if (val is UnityEngine.Object uo)
                return uo.GetType().Name + ":" + uo.name;
            if (val is string s)
                return s.Length > MaxString ? s.Substring(0, MaxString) + "..." : s;
            if (val is IEnumerable en && !(val is string))
            {
                var list = new List<object>();
                foreach (var item in en)
                {
                    list.Add(Render(item, depth + 1));
                    if (list.Count >= MaxItems) { list.Add("..."); break; }
                }
                return list;
            }
            var t = val.GetType();
            if (t.IsPrimitive || t.IsEnum) return val.ToString();
            return t.Name;
        }

        private static object LiteralOf(NamedVariable nv)
        {
            try
            {
                var raw = nv.RawValue;
                if (raw is UnityEngine.Object uo) return uo.GetType().Name + ":" + uo.name;
                if (raw is string s && s.Length > MaxString) return s.Substring(0, MaxString) + "...";
                return raw?.ToString();
            }
            catch { return "<val>"; }
        }
    }
}
