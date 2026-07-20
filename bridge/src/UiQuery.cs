using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CSAccessBridge
{
    /// <summary>All game inspection/interaction. Every method here must run on the main thread.</summary>
    internal static class UiQuery
    {
        private static readonly Dictionary<int, GameObject> IdCache = new Dictionary<int, GameObject>();

        public static string PathOf(GameObject go)
        {
            if (go == null) return null;
            var parts = new List<string>();
            for (var t = go.transform; t != null; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>Resolve "@instanceId" or a full transform path to a GameObject (scene objects only, including inactive).</summary>
        public static GameObject Resolve(string spec)
        {
            if (string.IsNullOrEmpty(spec)) return null;

            if (spec.StartsWith("@") && int.TryParse(spec.Substring(1), out int id))
            {
                if (IdCache.TryGetValue(id, out var cached) && cached != null)
                    return cached;
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (!t.gameObject.scene.IsValid()) continue;
                    IdCache[t.gameObject.GetInstanceID()] = t.gameObject;
                }
                IdCache.TryGetValue(id, out var found);
                return found;
            }

            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (!t.gameObject.scene.IsValid()) continue;
                if (PathOf(t.gameObject) == spec) return t.gameObject;
            }
            return null;
        }

        private static int Register(GameObject go)
        {
            int id = go.GetInstanceID();
            IdCache[id] = go;
            return id;
        }

        // ---------- Read endpoints ----------

        public static object Scenes()
        {
            var scenes = new List<object>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                var roots = new List<object>();
                foreach (var root in s.GetRootGameObjects())
                {
                    roots.Add(new Dictionary<string, object>
                    {
                        ["name"] = root.name,
                        ["id"] = Register(root),
                        ["active"] = root.activeSelf,
                        ["children"] = root.transform.childCount,
                    });
                }
                scenes.Add(new Dictionary<string, object>
                {
                    ["name"] = s.name,
                    ["loaded"] = s.isLoaded,
                    ["roots"] = roots,
                });
            }
            return new Dictionary<string, object> { ["scenes"] = scenes };
        }

        public static object Hierarchy(GameObject root, int depth, bool includeInactive)
        {
            if (root == null) throw new ArgumentException("object not found");
            int budget = 2500;
            return DumpNode(root, depth, includeInactive, ref budget);
        }

        private static object DumpNode(GameObject go, int depth, bool includeInactive, ref int budget)
        {
            var node = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["id"] = Register(go),
                ["active"] = go.activeInHierarchy,
            };

            var comps = new List<string>();
            foreach (var c in go.GetComponents<Component>())
                if (c != null) comps.Add(c.GetType().Name);
            node["components"] = comps;

            var tmp = go.GetComponent<TMP_Text>();
            if (tmp != null && !string.IsNullOrEmpty(tmp.text)) node["text"] = tmp.text;
            var legacy = go.GetComponent<Text>();
            if (legacy != null && !string.IsNullOrEmpty(legacy.text)) node["text"] = legacy.text;

            if (depth > 0 && budget > 0)
            {
                var children = new List<object>();
                foreach (Transform child in go.transform)
                {
                    if (!includeInactive && !child.gameObject.activeInHierarchy) continue;
                    if (--budget <= 0) { children.Add("...truncated..."); break; }
                    children.Add(DumpNode(child.gameObject, depth - 1, includeInactive, ref budget));
                }
                if (children.Count > 0) node["children"] = children;
            }
            else if (go.transform.childCount > 0)
            {
                node["childCount"] = go.transform.childCount;
            }
            return node;
        }

        public static object Texts()
        {
            var items = new List<object>();
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (!t.gameObject.scene.IsValid() || !t.gameObject.activeInHierarchy) continue;
                if (string.IsNullOrWhiteSpace(t.text)) continue;
                items.Add(new Dictionary<string, object>
                {
                    ["path"] = PathOf(t.gameObject),
                    ["id"] = Register(t.gameObject),
                    ["text"] = t.text,
                });
            }
            foreach (var t in Resources.FindObjectsOfTypeAll<Text>())
            {
                if (!t.gameObject.scene.IsValid() || !t.gameObject.activeInHierarchy) continue;
                if (string.IsNullOrWhiteSpace(t.text)) continue;
                items.Add(new Dictionary<string, object>
                {
                    ["path"] = PathOf(t.gameObject),
                    ["id"] = Register(t.gameObject),
                    ["text"] = t.text,
                    ["legacy"] = true,
                });
            }
            return new Dictionary<string, object> { ["texts"] = items };
        }

        public static object Fsms(string filter, bool activeOnly)
        {
            var items = new List<object>();
            foreach (var fsm in PlayMakerFSM.FsmList)
            {
                if (fsm == null || fsm.gameObject == null) continue;
                if (activeOnly && !fsm.gameObject.activeInHierarchy) continue;
                string path = PathOf(fsm.gameObject);
                if (!string.IsNullOrEmpty(filter) &&
                    path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (fsm.FsmName == null || fsm.FsmName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                items.Add(new Dictionary<string, object>
                {
                    ["path"] = path,
                    ["id"] = Register(fsm.gameObject),
                    ["fsm"] = fsm.FsmName,
                    ["state"] = fsm.ActiveStateName,
                    ["active"] = fsm.gameObject.activeInHierarchy && fsm.enabled,
                });
            }
            return new Dictionary<string, object> { ["count"] = items.Count, ["fsms"] = items };
        }

        /// <summary>World transforms by path filter, INCLUDING inactive objects (walks
        /// scene roots with GetComponentsInChildren(true) — FsmList and Find can't see
        /// disabled objects; this can). Read-only; capped. Built for the map-table
        /// verification: marker corridor angles + watching the Focus rig live.</summary>
        public static object Transforms(string filter, int max)
        {
            var items = new List<object>();
            for (int si = 0; si < UnityEngine.SceneManagement.SceneManager.sceneCount; si++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(si);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        string path = PathOf(t.gameObject);
                        if (!string.IsNullOrEmpty(filter)
                            && path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        var p = t.position;
                        var e = t.rotation.eulerAngles;
                        items.Add(new Dictionary<string, object>
                        {
                            ["path"] = path,
                            ["active"] = t.gameObject.activeInHierarchy,
                            ["pos"] = new float[] { p.x, p.y, p.z },
                            ["euler"] = new float[] { e.x, e.y, e.z },
                        });
                        if (items.Count >= max)
                            return new Dictionary<string, object>
                                { ["count"] = items.Count, ["truncated"] = true, ["transforms"] = items };
                    }
                }
            }
            return new Dictionary<string, object> { ["count"] = items.Count, ["transforms"] = items };
        }

        public static object FsmDetail(GameObject go, string name)
        {
            if (go == null) throw new ArgumentException("object not found");
            var results = new List<object>();
            foreach (var fsm in go.GetComponents<PlayMakerFSM>())
            {
                if (!string.IsNullOrEmpty(name) && fsm.FsmName != name) continue;

                var vars = new Dictionary<string, object>();
                var v = fsm.FsmVariables;
                foreach (var x in v.FloatVariables) vars[x.Name] = x.Value;
                foreach (var x in v.IntVariables) vars[x.Name] = x.Value;
                foreach (var x in v.BoolVariables) vars[x.Name] = x.Value;
                foreach (var x in v.StringVariables) vars[x.Name] = x.Value;
                foreach (var x in v.GameObjectVariables)
                    vars[x.Name] = x.Value != null ? PathOf(x.Value) : null;

                var states = new List<object>();
                foreach (var s in fsm.FsmStates)
                {
                    var transitions = new List<object>();
                    foreach (var tr in s.Transitions)
                        transitions.Add(tr.EventName + " -> " + tr.ToState);
                    states.Add(new Dictionary<string, object>
                    {
                        ["name"] = s.Name,
                        ["transitions"] = transitions,
                    });
                }

                var events = new List<string>();
                foreach (var e in fsm.FsmEvents) events.Add(e.Name);

                results.Add(new Dictionary<string, object>
                {
                    ["fsm"] = fsm.FsmName,
                    ["activeState"] = fsm.ActiveStateName,
                    ["variables"] = vars,
                    ["states"] = states,
                    ["events"] = events,
                });
            }
            return new Dictionary<string, object> { ["path"] = PathOf(go), ["fsms"] = results };
        }

        public static object Selectables()
        {
            var items = new List<object>();
            foreach (var s in Resources.FindObjectsOfTypeAll<Selectable>())
            {
                if (!s.gameObject.scene.IsValid() || !s.gameObject.activeInHierarchy) continue;
                string label = null;
                var tmp = s.GetComponentInChildren<TMP_Text>();
                if (tmp != null) label = tmp.text;
                else
                {
                    var legacy = s.GetComponentInChildren<Text>();
                    if (legacy != null) label = legacy.text;
                }
                items.Add(new Dictionary<string, object>
                {
                    ["path"] = PathOf(s.gameObject),
                    ["id"] = Register(s.gameObject),
                    ["type"] = s.GetType().Name,
                    ["interactable"] = s.IsInteractable(),
                    ["label"] = label,
                });
            }
            return new Dictionary<string, object> { ["selectables"] = items };
        }

        public static object Selection()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            return new Dictionary<string, object>
            {
                ["eventSystem"] = es != null,
                ["selected"] = sel != null ? PathOf(sel) : null,
                ["id"] = sel != null ? (object)Register(sel) : null,
            };
        }

        public static object Find(string query)
        {
            var items = new List<object>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                var go = t.gameObject;
                if (!go.scene.IsValid()) continue;
                if (go.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                items.Add(new Dictionary<string, object>
                {
                    ["path"] = PathOf(go),
                    ["id"] = Register(go),
                    ["active"] = go.activeInHierarchy,
                });
                if (items.Count >= 200) break;
            }
            return new Dictionary<string, object> { ["matches"] = items };
        }

        public static object InkState()
        {
            var proxy = UnityEngine.Object.FindObjectOfType<PlaymakerInkProxy>();
            if (proxy == null || proxy.story == null)
                return new Dictionary<string, object> { ["available"] = false };

            var story = proxy.story;
            var choices = new List<object>();
            foreach (var c in story.currentChoices)
            {
                choices.Add(new Dictionary<string, object>
                {
                    ["index"] = c.index,
                    ["text"] = c.text,
                });
            }
            return new Dictionary<string, object>
            {
                ["available"] = true,
                ["canContinue"] = story.canContinue,
                ["currentText"] = story.currentText,
                ["currentTags"] = new List<object>(story.currentTags ?? new List<string>()),
                ["choices"] = choices,
            };
        }

        // ---------- Interaction endpoints ----------

        public static object Select(GameObject go)
        {
            if (go == null) throw new ArgumentException("object not found");
            var es = EventSystem.current;
            if (es == null) throw new InvalidOperationException("no EventSystem");
            es.SetSelectedGameObject(go);
            return Ok(go);
        }

        public static object Click(GameObject go)
        {
            if (go == null) throw new ArgumentException("object not found");
            var es = EventSystem.current;
            var ped = new PointerEventData(es);
            var fired = new List<string>();
            if (ExecuteEvents.Execute(go, ped, ExecuteEvents.pointerEnterHandler)) fired.Add("pointerEnter");
            if (ExecuteEvents.Execute(go, ped, ExecuteEvents.pointerDownHandler)) fired.Add("pointerDown");
            if (ExecuteEvents.Execute(go, ped, ExecuteEvents.pointerUpHandler)) fired.Add("pointerUp");
            if (ExecuteEvents.Execute(go, ped, ExecuteEvents.pointerClickHandler)) fired.Add("pointerClick");
            if (ExecuteEvents.Execute(go, ped, ExecuteEvents.submitHandler)) fired.Add("submit");
            var result = Ok(go);
            ((Dictionary<string, object>)result)["handlers"] = fired;
            return result;
        }

        public static object Hover(GameObject go, bool exit)
        {
            if (go == null) throw new ArgumentException("object not found");
            var ped = new PointerEventData(EventSystem.current);
            bool handled = exit
                ? ExecuteEvents.Execute(go, ped, ExecuteEvents.pointerExitHandler)
                : ExecuteEvents.Execute(go, ped, ExecuteEvents.pointerEnterHandler);
            var result = Ok(go);
            ((Dictionary<string, object>)result)["handled"] = handled;
            return result;
        }

        public static object SendMsg(GameObject go, string message, GameObject arg)
        {
            if (go == null) throw new ArgumentException("object not found");
            if (arg != null)
                go.SendMessage(message, arg, SendMessageOptions.DontRequireReceiver);
            else
                go.SendMessage(message, SendMessageOptions.DontRequireReceiver);
            return Ok(go);
        }

        public static object SendFsmEvent(GameObject go, string fsmName, string eventName)
        {
            if (go == null) throw new ArgumentException("object not found");
            var sent = new List<string>();
            foreach (var fsm in go.GetComponents<PlayMakerFSM>())
            {
                if (!string.IsNullOrEmpty(fsmName) && fsm.FsmName != fsmName) continue;
                fsm.SendEvent(eventName);
                sent.Add(fsm.FsmName);
            }
            return new Dictionary<string, object> { ["ok"] = true, ["sentTo"] = sent };
        }

        public static object Broadcast(string eventName)
        {
            PlayMakerFSM.BroadcastEvent(eventName);
            return new Dictionary<string, object> { ["ok"] = true };
        }

        public static object Screenshot(string dir)
        {
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "shot_" + DateTime.Now.ToString("HHmmss_fff") + ".png");
            ScreenCapture.CaptureScreenshot(file);
            return new Dictionary<string, object> { ["ok"] = true, ["file"] = file, ["note"] = "written at end of frame; wait ~1s before reading" };
        }

        private static object Ok(GameObject go)
        {
            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["path"] = PathOf(go),
                ["id"] = Register(go),
            };
        }
    }
}
