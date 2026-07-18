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
