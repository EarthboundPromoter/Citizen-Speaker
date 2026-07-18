# Citizen Sleeper Access

Screen-reader accessibility mod for Citizen Sleeper (Steam, Unity 2021.3 Mono), built on BepInEx 5 + HarmonyX, speaking through [Tolk](https://github.com/dkager/tolk) (NVDA, JAWS, SAPI, and other readers).

## Layout

- `mod/` — **CSAccess**, the accessibility mod itself.
- `bridge/` — **CSAccessBridge**, a dev-only localhost HTTP bridge (port 8330) used to inspect and drive the running game during development. Not part of a user-facing release.

## Install (development machine)

1. BepInEx 5 x64 extracted into the game folder (done).
2. `Tolk.dll` + `nvdaControllerClient64.dll` beside `Citizen Sleeper.exe` (done).
3. `steam_appid.txt` containing `1578650` beside the exe lets you launch the exe directly (Steam must be running).
4. Build and deploy either plugin:
   ```
   cd mod    && dotnet build -c Release   # -> copy bin/Release/CSAccess.dll       to <game>/BepInEx/plugins/
   cd bridge && dotnet build -c Release   # -> copy bin/Release/CSAccessBridge.dll to <game>/BepInEx/plugins/
   ```

## Keyboard commands (v0.1)

| Key | Action |
|---|---|
| Up / Down | Move focus through interactable UI elements |
| Enter | Activate focused element (announces action outcomes) |
| Space | Describe focused element in detail |
| Tab / Shift+Tab | Cycle actions at the current location |
| L / Shift+L | Cycle available locations and characters on the station map |
| 1–9 | Pick dialogue choice (menu open) / slot that die into the focused action |
| D | Read dice values |
| K | Read clocks at this location |
| C | Read status (cryo, drives) |
| F2 | Repeat current dialogue line |
| R | Repeat last speech |
| [ / ] | Speech history back / forward |
| ` (grave) | Stop speech |
| T | Continue tutorial popup |
| F1 | Speak command help |

## Architecture

- **Speech** (`mod/src/Speech/`): Tolk P/Invoke + `SpeechService` — Immediate (interrupting) vs Queued announcements, queue pumped only while the reader is idle, 200-entry history, rich-text stripping. Every utterance is logged as `[Speech:<source>]`.
- **Capture** (`mod/src/Patches/`): Harmony postfixes on PixelCrushers Dialogue System `ShowSubtitle` / `ShowResponses` / `ShowAlert` (both `UnityUIDialogueUI` and `StandardDialogueUI`, covering whichever the prefab uses) and on `EventSystem.SetSelectedGameObject` for focus announcements.
- **Watchers** (`mod/src/Watchers.cs`): polled activation watchers for tutorial popups and character notifications (no code path to hook — they are PlayMaker-driven), plus gamepad-UI-mode enforcement.
- **Game queries** (`mod/src/Game/GameQueries.cs`): reads dice values, clock progress, action data straight from PlayMaker FSM variables (`PlayMakerFSM.FsmList`); drives the dice flow through the game's own gamepad FSMs: `Gamepad Dice Slot` → `Click`, `Dice Cursor N` → `Click`, then the action's START ACTION button.
- **Describe** (`mod/src/UI/Describe.cs`): GameObject → spoken label rules built from the game's naming conventions (`Response: <text>`, `Dice Slot Button`, `Location Button`, `* Action` panels, `* Canvas` owners).

## Key game facts (from the decompile + runtime survey)

- Game logic/UI orchestration is PlayMaker FSMs in scene data; almost no game C#.
- Narrative flows through the Dialogue System's Ink integration — **not** `PlaymakerInkProxy` (hooks on it never fire in practice).
- Dialogue UI prefab: `CS Dialogue Manager/Canvas/TMP CS Dialogue UI 1` (legacy `UnityUIDialogueUI` + TMP wrappers).
- HUD root: `Letterbox Canvas/` (Dice UI, action groups, tutorial system, notifications, drive tracker).
- Station map: world-space canvases under `ERLIN MAIN/1_Station UI/` with ordinary uGUI `Location Button` / `Character Button`.
- Die values: `Dice UI/Dice Slot N/Die` FSM variable `DiceValue`; action data: `Action Controller` FSM variables; outcomes: controller states `Positive/Neutral/Negative Outcome`.
- The global `Gamepad Manager` FSM (event `Gamepad`) switches the UI into controller mode, which spawns per-action `Gamepad Dice Slot` FSMs — the sanctioned non-mouse dice path this mod drives.
