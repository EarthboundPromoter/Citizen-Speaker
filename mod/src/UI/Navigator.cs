using System.Collections.Generic;
using CSAccess.Speech;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CSAccess.UI
{
    /// <summary>Keyboard navigation over the game's interactable uGUI elements.
    /// Selection goes through EventSystem so the FocusPatch announces it.</summary>
    internal static class Navigator
    {
        public static void Select(GameObject go)
        {
            SpeechService.FlushQueue();
            Patches.FocusPatch.NoteUserNavigation();
            Patches.FocusPatch.ClearCooldown(go);
            var es = EventSystem.current;
            if (es == null) return;
            if (es.currentSelectedGameObject == go)
            {
                // Re-announce even if unchanged.
                SpeechService.Say(Describe.Element(go, detailed: false), Priority.Immediate, "focus");
                return;
            }
            es.SetSelectedGameObject(go);
        }

        public static GameObject Current()
        {
            return EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        }

        /// <summary>Move selection the way a controller dpad press does: a uGUI move event
        /// through the game's own navigation graph. Never picks targets itself.</summary>
        public static void Move(MoveDirection direction)
        {
            SpeechService.FlushSource("focus");
            Patches.FocusPatch.NoteUserNavigation();
            var es = EventSystem.current;
            if (es == null) return;
            var current = es.currentSelectedGameObject;
            if (current == null || !current.activeInHierarchy)
            {
                // Nothing focused: adopt the first interactable selectable so the
                // navigation graph has a starting point.
                foreach (var s in Object.FindObjectsOfType<Selectable>())
                {
                    if (s.IsInteractable() && s.gameObject.activeInHierarchy)
                    {
                        es.SetSelectedGameObject(s.gameObject);
                        return;
                    }
                }
                SpeechService.Say("Nothing to focus.", Priority.Immediate, "nav");
                return;
            }
            var move = new AxisEventData(es)
            {
                moveDir = direction,
                moveVector = direction switch
                {
                    MoveDirection.Up => Vector2.up,
                    MoveDirection.Down => Vector2.down,
                    MoveDirection.Left => Vector2.left,
                    _ => Vector2.right,
                },
            };
            ExecuteEvents.Execute(current, move, ExecuteEvents.moveHandler);

            // Dead end (or sole focusable element): repeat the element bare — the repeat
            // itself is the dead-end signal, and an arrow press must never be silent.
            if (es.currentSelectedGameObject == current)
                SpeechService.Say(Describe.Element(current, detailed: false),
                    Priority.Immediate, "focus");
        }

        private static float _lastActivate = -1f;

        /// <summary>Activate the focused element. The game's keyboard map only navigates —
        /// it never submits — so Enter activation is the mod's job. Rapid repeats are
        /// debounced: scripted UI (tutorial chains) can be mid-transition right after an
        /// activation, and a second click there breaks the script.</summary>
        public static void ActivateCurrent()
        {
            var go = Current();
            if (go == null)
            {
                SpeechService.Say("Nothing focused.", Priority.Immediate, "nav");
                return;
            }
            if (Time.unscaledTime - _lastActivate < 0.3f) return;
            _lastActivate = Time.unscaledTime;
            Click(go);
        }

        public static void Click(GameObject go)
        {
            // A disabled Selectable swallows clicks silently — say so instead
            // (the game gates buttons this way, e.g. End Cycle during the intro).
            var selectable = go.GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable())
            {
                SpeechService.Say(Describe.Element(go, detailed: false) + ".", Priority.Immediate, "nav");
                return;
            }

            // Exactly ONE activation path, like Unity's own input module (submit for
            // keyboard/gamepad, pointer click as fallback). Firing both invokes a
            // Button's onClick twice per press — double-dispatching scripted events
            // (the tutorial-chain hangs, sessions 2 through 6).
            var es = EventSystem.current;
            bool activated = ExecuteEvents.Execute(go, new BaseEventData(es), ExecuteEvents.submitHandler);
            if (!activated)
            {
                var ped = new PointerEventData(es);
                ExecuteEvents.Execute(go, ped, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(go, ped, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(go, ped, ExecuteEvents.pointerUpHandler);
                activated = ExecuteEvents.Execute(go, ped, ExecuteEvents.pointerClickHandler);
            }
            if (!activated)
                SpeechService.Say("Not activatable.", Priority.Immediate, "nav");
        }
    }
}
