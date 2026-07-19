using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CSAccessBridge
{
    [HarmonyPatch(typeof(PlaymakerInkProxy), nameof(PlaymakerInkProxy.Continue))]
    internal static class InkContinuePatch
    {
        private static void Postfix(string __result)
        {
            WatchLog.Add("ink.line", __result);
        }
    }

    [HarmonyPatch(typeof(PlaymakerInkProxy), nameof(PlaymakerInkProxy.chooseChoiceIndex))]
    internal static class InkChoosePatch
    {
        private static void Prefix(PlaymakerInkProxy __instance, int i)
        {
            string text;
            try { text = "[" + i + "] " + __instance.getChoiceString(i); }
            catch { text = "[" + i + "]"; }
            WatchLog.Add("ink.choose", text);
        }
    }

    [HarmonyPatch(typeof(PlaymakerInkProxy), nameof(PlaymakerInkProxy.choosePathString))]
    internal static class InkPathPatch
    {
        private static void Prefix(string path)
        {
            WatchLog.Add("ink.path", path);
        }
    }

    [HarmonyPatch(typeof(setTextmeshProUGUIText), "OnEnter")]
    internal static class TmpTextActionPatch
    {
        private static void Postfix(setTextmeshProUGUIText __instance)
        {
            try
            {
                var target = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
                WatchLog.Add("ui.text", __instance.textString.Value, target != null ? UiQuery.PathOf(target) : null);
            }
            catch
            {
                WatchLog.Add("ui.text", __instance.textString?.Value);
            }
        }
    }

    [HarmonyPatch(typeof(UiTextSetText), "OnEnter")]
    internal static class LegacyTextActionPatch
    {
        private static void Postfix(UiTextSetText __instance)
        {
            try
            {
                var target = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
                WatchLog.Add("ui.textlegacy", __instance.text?.Value, target != null ? UiQuery.PathOf(target) : null);
            }
            catch { }
        }
    }

    /// <summary>Every Button activation — game-, mouse- or mod-initiated — lands in
    /// /watch as ui.press. Button.Press is private and version-sensitive, so the
    /// patch skips itself (Prepare) rather than failing the whole PatchAll if the
    /// method is absent in this uGUI build.</summary>
    [HarmonyPatch]
    internal static class ButtonPressPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
            => AccessTools.Method(typeof(UnityEngine.UI.Button), "Press");

        private static bool Prepare()
        {
            bool found = AccessTools.Method(typeof(UnityEngine.UI.Button), "Press") != null;
            if (!found) Plugin.Log.LogWarning("Button.Press not found — ui.press watch events disabled.");
            return found;
        }

        private static void Postfix(UnityEngine.UI.Button __instance)
        {
            try
            {
                WatchLog.Add("ui.press", __instance.name, UiQuery.PathOf(__instance.gameObject));
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(EventSystem), nameof(EventSystem.SetSelectedGameObject),
        typeof(GameObject), typeof(BaseEventData))]
    internal static class SelectionPatch
    {
        private static void Postfix(GameObject selected)
        {
            WatchLog.Add("ui.select",
                selected != null ? selected.name : "(deselected)",
                selected != null ? UiQuery.PathOf(selected) : null);
        }
    }
}
