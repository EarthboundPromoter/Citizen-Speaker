# Citizen Sleeper — game architecture survey

Assembled 2026-07-18 by static extraction only (game not running, nothing modified).
Sources: Managed assemblies read with dnfile 0.18.0 (pure-Python .NET metadata parser,
pip-installed for this survey), scenes and asset containers read with UnityPy 1.25.2.
Scratch scripts: `asm_survey.py`, `class_detail.py`, `scene_survey.py`, `assets_survey.py`,
`globals_dump.py` in the session scratchpad. Spoiler discipline applies throughout: story
entities the repo docs do not already name are counted, not named; no narrative prose was
read or quoted.

## Orientation overview

Citizen Sleeper is a Unity 2021.3.33f1 Mono game (PlayerSettings: Jump Over the Age /
Citizen Sleeper) built almost entirely out of middleware. The custom C# footprint is
astonishingly small: the game's own namespaced code is three enums (`CitizenSleeper.Class`,
`CitizenSleeper.Rating`, `CitizenSleeper.Skill`) plus a handful of global-namespace helper
MonoBehaviours (`PlaymakerInkProxy`, `ControllerIconPrompt`, `LocalizationManager`,
`FontManager`, and cosmetic FX scripts). Everything that plays the game — cycle logic,
dice, actions, clocks, UI windows, the intro script, endings — is PlayMaker FSM data
serialized into the scenes: level1 alone carries 4,936 PlayMakerFSM components. Narrative
is Ink (four localized `CS-Main Ink` JSON TextAssets in sharedassets0) delivered through
Pixel Crushers' Dialogue System (source-compiled into Assembly-CSharp-firstpass, no
separate DLL), which also owns drives (as Dialogue System quests), saves (PixelCrushers
SaveSystem writing `save_N.dat` to LocalLow), and localization text tables. Input is
Rewired; audio is 100% FMOD Studio (2.1.11, one 105 MB master bank); text is TextMeshPro.
There are exactly three scenes: level0 = main title, level1 = the entire game, level2 =
a loading stub. The persistent manager stack (Dialogue Manager + save system + Rewired
Input Manager + localization) lives in level0 and survives into level1 via
DontDestroyOnLoad-style persistence — level1 has no input-manager root of its own.

The consequence for the mod: there is no game C# layer to hook for game state. The seams
are (a) engine/middleware classes (uGUI, TMP, EventSystem, Dialogue System UIs — already
the mod's Harmony targets), (b) PlayMaker FSM variables/events/states read by name, and
(c) rendered text. This survey confirms the mod's instincts and sharpens several beliefs;
see SURPRISES.

## 1. Assembly layer

`Citizen Sleeper_Data\Managed\` holds 133 DLLs. Breakdown:

- Engine: 75 `UnityEngine.*` module DLLs plus `UnityEngine.dll`, `UnityEngine.UI.dll`,
  `Unity.TextMeshPro.dll`, `Unity.Timeline.dll`, `Unity.Postprocessing.Runtime.dll`,
  `Cinemachine.dll`, `Unity.ProBuilder.*`, `Unity.MemoryProfiler.dll`, plus BCL
  (`mscorlib`, `System.*`, `netstandard`, `Mono.*`).
- Game code: `Assembly-CSharp.dll` (2,564 typedefs, 13,788 methods) and
  `Assembly-CSharp-firstpass.dll` (1,067 typedefs, 6,295 methods). See below — most of
  this is asset-store source, not game logic.
- Third-party stack, precisely:
  - PlayMaker 1.9.0.p20 (`PlayMaker.dll` file version 1.9.0.31, product 1.9.0.p20).
    Plus, compiled into Assembly-CSharp: `HutongGames.PlayMaker.Actions` (985 action
    classes), Cinemachine/tween/ecosystem action packs, and `PlayMakerUi*Event` proxies.
  - Rewired (`Rewired_Core.dll` 1.9 MB + `Rewired_Windows*.dll` + native
    `Rewired_DirectInput.dll`, `Rewired_WindowsGamingInput.dll` in Plugins\x86_64;
    version string not embedded). Also `Rewired.Integration.PlayMaker` — 424 PlayMaker
    action classes for Rewired — and `Rewired.UI.ControlMapper` inside Assembly-CSharp.
  - Pixel Crushers Dialogue System: NO standalone DLL. `PixelCrushers` (89 types),
    `PixelCrushers.DialogueSystem` (273 types), Ink support, wrappers, and a bundled Lua
    interpreter (`Language.Lua`, 67 types) are compiled into Assembly-CSharp-firstpass;
    80 `PixelCrushers.DialogueSystem.PlayMaker` action classes sit in Assembly-CSharp.
  - Ink runtime: `Ink-Libraries.dll` (242 KB); story format is inkVersion 19 (from the
    story JSON headers).
  - FMOD: `FMODUnity.dll`, `FMODUnityResonance.dll`; native `fmodstudio.dll` 2.1.11.
  - MoreMountains: `MoreMountains.Tools.dll`, `MoreMountains.InventoryEngine.dll`
    (+ Demos DLL) — present but apparently unused by the game proper (see SURPRISES).
  - Misc asset-store: `Tayx.Graphy` (perf monitor), `LeTai.TranslucentImage` (the
    ubiquitous blur), `ch.sycoforge.Decal` (EasyDecal 2020.2.4), `sc.posteffects.runtime`
    (SCPE), `ConditionalExpression.dll`, `Whinarn.UnityMeshSimplifier`,
    `com.rlabrecque.steamworks.net` (Steamworks.NET), `XGamingRuntime`/`XblPCSandbox`/
    `GdkUtilities` (Xbox GDK), `Mono.WebBrowser`, VLB (Volumetric Light Beam, in
    firstpass), Dreamteck Splines, PolyFew, SoftMasking, Unity UI Extensions (137 types),
    DragQueen2D — all inside the two game assemblies.

Game-authored code, exhaustively (from `asm_survey.py` + `class_detail.py`):

- `CitizenSleeper.Class` enum: MACHINIST, OPERATOR, EXTRACTOR.
- `CitizenSleeper.Rating` enum: SAFE, RISKY, DANGER.
- `CitizenSleeper.Skill` enum (and a duplicate `Com.CitizenSleeper.Skill`): ENGINEER,
  INTERFACE, ENDURE, INTUIT, ENGAGE.
- `PlaymakerInkProxy` (MonoBehaviour): thin Ink wrapper — StartStory, canContinue,
  Continue, currentChoiceCount, getChoiceString, chooseChoiceIndex, choosePathString,
  get/set variable by type, tag getters. This is the PlayMaker-to-Ink bridge the docs
  hypothesized; it is 19 methods, nothing more.
- `ControllerIconPrompt` (MonoBehaviour): fields iconText, DefaultText, PlayStationText,
  XboxText, SwitchText + per-platform font sizes. Note: there is NO keyboard variant —
  static confirmation that the game has zero keyboard prompt support (triage report 1).
- `LocalizationManager`, `FontManager`, `LocalizedFontSize`, `LocalizedImage`: language
  switching, TMP fallback-font swapping, per-language font-size and sprite overrides.
- Platform services: `SteamManager`, `PlayStationManager`, `XboxOnePlatformService`,
  `WindowsStorePlatformService`, `GamingRuntimeManagerMk2`, `Gdk`, `GamePad`.
- Cosmetic/utility: `BillboardFX`, `SkyboxRotator`, `VersionNumSetter`, `HighlightSnap`,
  `SetHighlight` (hover/drop highlight relays), `InfoBubble`, `CandidateFilter`,
  `GetScreenShot`, `GlitchImageEffect`, `FPSCounter`.

Everything else in the two assemblies is middleware source. There is no game data model
in C#: no Player class, no Action class, no Clock class, no Stat class.

## 2. Responsibility map

Where each concern actually lives, and the seam the mod can hook:

- Game logic (cycle, dice, actions, clocks, gating, intro script, endings): PlayMaker
  FSMs serialized in level1 (4,936 FSM components; 202 PlayMakerFixedUpdate). Seam: read
  `PlayMakerFSM.FsmVariables` by name, observe ActiveStateName, Harmony-hook
  `Fsm.Event`/state entry if event-level detail is needed. There is no C# alternative.
- Narrative text: Ink JSON (sharedassets0) → `DialogueSystemInkIntegration` +
  `PlaymakerInkProxy` → Dialogue System conversation → `StandardDialogueUI` /
  `StandardUISubtitlePanel` / `StandardUIMenuPanel` (instances under the persistent
  CS Dialogue Manager, prefab in sharedassets1). Seam: the mod's existing
  ShowSubtitle/ShowResponses/ShowAlert patches are the right layer.
- Drives/quests: Dialogue System quest machinery — `StandardUIQuestTracker` (Drive
  Tracker HUD), `UnityUIQuestLogWindow` (CS Drive Log), PlayMaker global event
  `CheckQuestCompletion`, GameObject global `Tracked Quest Object`. Underlying state is
  the Dialogue System Lua database (quest fields), synced from Ink variables. Seam:
  `PixelCrushers.DialogueSystem.QuestLog` static API — a richer, un-hooked source than
  scraping the rendered HUD.
- Input: Rewired. Level0 root `Rewired Input Manager` (`InputManager` +
  `NintendoSwitchInputManager` + `RewiredCinemachineBridge`) persists into level1. Both
  scenes' EventSystem carries `StandaloneInputModule` AND `RewiredStandaloneInputModule`.
  FSMs consume input via the 424 Rewired PlayMaker actions. Action names are not
  strings-recoverable statically (tools README); enumerate live via
  `ReInput.mapping.Actions`.
- UI state / modality: PlayMaker FSMs on the window/controller objects (see docs
  ui-state-map.md) plus a registry of GameObject-typed PlayMaker globals that point at
  the live anchor objects (section 4/SURPRISES below).
- Stats: PlayMaker FSM variables only (Cycle Controller locals et al.). No C# stat store
  exists anywhere in the assemblies.
- Saves: PixelCrushers SaveSystem stack on the persistent manager (section 5).
- Localization of UI strings: `LocalizeUI` components (4,627 in level1) fed by Dialogue
  System TextTables (`CS-TextTable`, `CS-TextTable UI` in sharedassets0) via
  `UILocalizationManager`; fonts swapped by `FontManager`.
- Audio: FMOD `StudioEventEmitter` (934 in level1); no Unity audio path in use.

## 3. Scene inventory

BuildSettings (globalgamemanagers) lists exactly three scenes:
`Assets/CS/1 - CS - MAIN TITLE.unity` (level0), `Assets/CS/2 - CS - MAIN.unity` (level1),
`Assets/CS/LOADING.unity` (level2).

### level0 — main title (1,135 objects, 420 MonoBehaviours, 20 PlayMakerFSM)

Roots: WORLD (skybox/sun FX), FPSCounter (inactive), EventSystem (Standalone +
RewiredStandalone input modules), Menu Ambience / Menu Music (FMOD), LocalizationManager
(+FontManager), Main Camera, MAIN MENU (FSM; child `Demo Menu` containing Title Canvas,
Landing Canvas, Menu Canvas, Slot Menu, Language Menu (own FSM), Warning Menu, Character
Select Canvas, QR Code Canvas), Rewired Input Manager, Platform Manager (FSM),
PlayMakerGUI, **CS Dialogue Manager** (DialogueSystemController, InstantiatePrefabs,
InputDeviceManager + InputDeviceManagerRewired, DialogueSystemInkIntegration,
UILocalizationManager, StandardSceneTransitionManager, DialogueSystemSaver, SaveSystem,
BinaryDataSerializer, CrossPlatformSavedGameDataStorer, PlayMakerFSM; children: dialogue
Canvas, Load Canvas), PlayStationPlatformService, SteamManager, XboxPlatformService,
Gamepad Manager (FSM), LOADING canvas (inactive). The manager stack here persists into
level1 (level1 has no Rewired root, no dialogue manager, no save system of its own).

### level1 — the entire game (231,750 objects; 54,928 GameObjects; 49,803 MonoBehaviours)

MonoBehaviour census highlights (scene_survey.txt): Image 16,387; TextMeshProUGUI 10,102;
PlayMakerFSM 4,936; LocalizeUI 4,627; UICircle 3,037; TranslucentImage 2,279;
StudioEventEmitter 934; Button 918; EventTrigger 612; Cinemachine vcam stack ~362 each;
ControllerIconPrompt 15; exactly one each of StandardUIQuestTracker,
UnityUIQuestLogWindow, EventSystem.

Roots (complete list, with major containers 2 levels deep):

- `PAUSE` (FSM) → Pause Canvas (inactive): Fader, RESUME/OPTIONS/QUIT TO MENU/QUIT GAME
  buttons, two warning menus, Options Menu, `Time SInce Last Autosave` (FSM+TMP).
- `[Decal Root]`, `Gamepad Manager` (FSM), `Main Ambience`, `PlayMakerGUI`, `EventSystem`
  (Standalone + RewiredStandalone modules).
- `World Rotator` (skybox/sun/nebula FX, FSMs).
- `Saver` (INACTIVE root with FSM — autosave machinery).
- `Cycle Controller` (FSM + FixedUpdate; the stats/cycle owner) → Cycle Scene Manager
  (FSM), Fader Canvas, `Cycle RNGs` (inactive): Scrap Freighter RNG, Ship Position RNG
  (per-cycle randomizers).
- `Preferences Init` (FSM).
- `ERLIN MAIN` — the station. Children:
  - `1_Station UI`: `Locations` (FSM; **110 location canvases**), `Characters` (FSM; **39
    children**), `Drive Triggers` (FSM; **49 children**), `Hacking UI` (inactive; **52
    children**), `Agent Spawners` (4), `One-shot Autoplay Scenes` (**95 children** —
    scripted scene players).
  - `2_Erlin's Eye`: station geometry (spokes, gimbal, 912-child interior ring, ships).
  - `3_Lights + Effects`, `4_Hacking Visual`, `5_Station Audio` (15 zone ambience
    emitters named after station zones).
- `Flux Ending Controller` (FSM) and `Ending Controller` (FSM) → End Credits canvas
  (inactive). Ending machinery; not previously in any doc.
- `Location Controller` (FSM), `Focus Rotator` → Focus (FSM; Station Orbital Control,
  UI selector (inactive FSM), Zoomed Out Listener), `UI Reselector` (inactive FSM).
- `Intro Sequence` (FSM) → Intro Fader Canvas (FSM).
- `Main Camera` → UI Camera (FSM), Hack Camera (inactive; Blur + Hack UI cameras),
  Cinemachine TopRig/MiddleRig/BottomRig, Far/Close listeners.
- `Jukebox` (inactive; FSM + 4 FMOD emitters).
- DEBUG roots (all inactive): `DEBUG Intro Skipper`, `DEBUG Energy Tester`,
  `DEBUG TOGGLES`, `DEBUG - Selection Tracker`, `DEBUG Capture`,
  `DEBUG - Global Variable Setter`, `DEBUG - R Tracker`.
- `Letterbox Canvas` (FSM) — the whole 2D game UI, 18 children:
  - `1_Action Groups` (FSM; **177 per-location action groups**, all inactive by default;
    dice-action machinery per docs section 6b).
  - `2_Hacking Action Groups` (**exactly 40 groups**: network nodes, agents, gates,
    ports, nests, ConSec, flux nodes — counts, not names, per spoiler rule).
  - `Letterbox BG` (bars), `Corners Container`.
  - `Drive System` (FSM) → Drive Tracker HUD (StandardUIQuestTracker), CS Drive Log
    (UnityUIQuestLogWindow + CanvasGroup + Animator), Drive Log Button (FSM),
    Gamepad Prompt L (FSM + ControllerIconPrompt).
  - `Character Notifications` (FSM) → Drive / Perk / Breakdown notification stacks.
  - `Character Window` (CanvasGroup + Animator, no FSM on the window) → BG, Character
    Portrait (FSM), Upgrade Tracker (FSM; 16 children), SKILL List.
  - `Character UI` → Character UI Button (FSM, 9 children).
  - `Top UI`: Ghost Trackers (FSM; 3 children), Leave Button (inactive; FSM), Energy UI
    (9 children), Scan Button (FSM), Dice UI (10 children), Dice Revealer (5 children),
    Active Frame, Outline, FPS (inactive).
  - `Breakdown UI` (Animator + CanvasGroup; static art incl. WIPER).
  - `Bottom UI` → Inventory (FSM; 10 children).
  - `Tutorial System` (Canvas + CanvasGroup): **11 tutorial panels**, each with own FSM —
    Intro and Control (PROMPTS), Dice/Condition/Energy, Drive (PROMPTS), Navigation
    (PROMPTS), Character Screen (PROMPTS), New Action 1, New Action 2, Clock, Cloud
    (PROMPTS), Hacking Action, Breakdown — plus Button (the continue button), Character/
    Hacking/Breakdown Tutorial Triggers, and `Input Pauser` (FSM + FixedUpdate).
  - `Perks Manager` (FSM) → **3 per-perk FSM children**, names skill-prefixed (ENGINEER/
    INTERFACE/ENGAGE — matching the three `* Action Performed` global events).
  - `Map Key` (legend; 2 column lists), `Saving` (inactive dice throbber),
    `Cinematic Effects (Stings)` (FSM; Red/White/Flux visual stings), `Scan Fader` (FSM),
    `DEBUG MENU` (inactive).

### level2 — LOADING (28 objects)

Camera + canvas + 2 images + FMOD emitter. A transition stub; nothing to map.

## 4. Asset containers

- `globalgamemanagers.assets`: 4,341 MonoScripts (the script registry) + 3 textures.
- `resources.assets` (926 objects): the PlayMaker globals asset (see below), TMP
  settings/default fonts (LiberationSans SDF + fallback, EmojiOne sprites), 21 stray
  AudioClips and MoreMountains/Dialogue System demo items (asset-pack leftovers — e.g.
  demo voice clips, ArmorItem/BombItem pickers; NOT game content), 399 textures,
  2 fonts. Notable: `PlayMakerGlobals` (6,832-byte blob) — the design-time global
  variable/event table. Recovered names (globals_dump.txt) include bools/ints/floats
  `CycleCanEnd`, `SlottedDiceGlobal`, `SlottedDiceValueGlobal`, `Gamepad`, `Hacking?`,
  `Data?`, `Action View?`, `Scenes Active?`, `Autoplay Waiting`, `GameInitialised`,
  `Global Home Grade`, `Time Since Save`, `Save on Next Leave`, `GlobalBoostValue`,
  `Selected Slot Position`; GameObject-typed globals forming an anchor registry
  (`ActiveAction`, `Leave Button`, `Dialogue Panel`, `Response Menu`, `Subtitle Panel`,
  `Tutorial System`, `Tutorial Text`, `Dice Gamepad System`, `UI Selector`,
  `UI Reselector`, `Scan Button`, `Character UI Button`, `Upgrade Button`, `Inventory`,
  `Inventory Display`, `Item Name`, `Item Description`, `Tracked Quest Object`,
  `Locations`, `Characters`, `Action Groups`, `Home Actions`, `Saver`, `Perk Manager`,
  ~24 `* Item` object globals — inventory item objects, counted not named); and the
  global event roster (`EndCycle` is consumed per docs; also `RollDice`, `Slot1..3`,
  `DiceSlotted`, `UseDice`, `ForceUnslotDice`, `Reroll`, `Fix Breakdown`,
  `Continue Shift`, `Leave`, `HubTransit`/`RimTransit`/`GreenwayTransit`,
  `CheckQuestCompletion`, `Engineer/Interface/Engage Action Performed`, `Force Save`,
  music/SFX volume events, `INIT`, `Language`).
- `sharedassets0.assets` (853 objects): **all narrative and dialogue data.** Four
  TextAssets all named `CS-Main Ink` (1.16/1.24/1.38/1.48 MB), each compiled Ink JSON,
  inkVersion 19 — evidently per-language story variants (DialogueSystemInkIntegration has
  an `inkJSONLocalizationAssets` field). Location + format only; content not read.
  Also: `DialogueDatabase :: CS Database Repaired` (the single Dialogue System database),
  TextTables `CS-TextTable` and `CS-TextTable UI`, the dialogue UI prefab
  (StandardDialogueUI, subtitle/menu panels, response button, typewriter effect),
  Rewired controller data (167 HardwareJoystickMap + ControllerDataFiles), 16 TMP font
  assets + 17 source fonts (Kontrapunkt, Rigid Square, Monument Extended, Noto CJK,
  etc.), post-processing profiles.
- `sharedassets1.assets` (1,921 objects): world art (1,214 meshes) plus prefabs:
  **51 DialogueActor components** (the dialogue cast size, counted not named), the CS
  Dialogue Manager prefab (same manager stack as level0's root), one FsmTemplate.
- `sharedassets2.assets`: empty (PreloadData only).
- `StreamingAssets\`: `Master.bank` (105 MB) + `Master.strings.bank` — the entire FMOD
  audio; `SaveIcon.png`, `SaveIcon_PS5.png`.
- **No SpriteAtlas objects exist in any container.** Map-marker glyphs are individual
  Sprites (187 in resources.assets, 52 in sharedassets0, 139 in sharedassets1); glyph
  transcoding must key on `Image.sprite.name` / `Image.overrideSprite.name` at runtime,
  not on an atlas.

## 5. Save system

Chain (level0 CS Dialogue Manager components + firstpass classes): PixelCrushers
`SaveSystem` + `DialogueSystemSaver` + `BinaryDataSerializer` (binary, not JSON) +
`CrossPlatformSavedGameDataStorer` — a wrapper subclass of
`PixelCrushers.DiskSavedGameDataStorer` (23 methods; in `PixelCrushers.Wrappers`,
Assembly-CSharp-firstpass) that routes to platform services (Steam/GDK/PSN) on consoles
and disk on PC. On this machine the store is
`C:\Users\IATPFNJ624\AppData\LocalLow\Jump Over the Age\Citizen Sleeper\` containing
`save_1.dat` and `saveinfo.dat` (listing only — not opened; owner's save is precious and
spoiler-laden). Dialogue System state (Lua database, quest states, Ink variable state via
`DialogueSystemInkIntegration.OnRecordPersistentData`) rides inside the same save.
PlayMaker-side autosave wiring: `Saver` root FSM, globals `Save on Next Leave`,
`Time Since Save`, events `Force Save` / `Force Save Options` / `Save`.

## 6. Reality check against current docs/mod

What the mod currently touches (mod\src, read-only skim): Harmony patches on
`EventSystem.SetSelectedGameObject` (FocusPatch), `StandardDialogueUI` and
`UnityUIDialogueUI` ShowSubtitle/ShowResponses/ShowAlert (DialoguePatches); everything
else is path-string `GameObject.Find` (Letterbox Canvas subtrees, PAUSE, MAIN MENU/Demo
Menu/Character Select Canvas) plus named FSM variable reads (`DiceValue`, `Positive?`,
`Cycle Clock?`, Cycle Controller locals) and `FsmVariables.GlobalVariables`.

Checked against the game-that-is: the ui-state-map's modal-layer inventory, anchor
choices, End Cycle wiring, dice-archetype split, and debug-object roster all match the
serialized scene. The corrections and extensions are below.

## SURPRISES

1. **There is no game C# to hook — at all.** The game's authored code is 3 enums and a
   dozen helper MonoBehaviours; every mechanic is PlayMaker FSM data (4,936 FSMs in
   level1). Any plan phrased as "find the game's X class" is void: there is no
   Player/Action/Clock/Stat class. FSM names and variables are the only game-logic API.
   (asm_survey.txt; scene_survey.txt census.)
2. **`Player_Condition` / `Player_Energy` / `Player_Class` / `Player_UpgradePoints`,
   `IntroComplete`, and the `C_*` tracker names are NOT in the serialized PlayMakerGlobals
   table** (globals_dump.txt has none of them), yet ui-state-map section 2 calls them
   "PlayMaker GLOBALS". Either they are runtime-registered globals, or the FSM-blob
   strings were locals/targets misread as globals. Meanwhile `CycleCanEnd` — which the
   docs treat as an Intro Sequence FSM local — IS a design-time global. The stats read
   plan (triage 4/7) should treat Cycle Controller locals as primary and verify the
   underscore names live before relying on them.
3. **The game ships its own anchor registry.** GameObject-typed PlayMaker globals point
   at the live sanctioned objects: `ActiveAction`, `Leave Button`, `Dialogue Panel`,
   `Response Menu`, `Subtitle Panel`, `Tutorial System`, `Tutorial Text`, `Dice Gamepad
   System`, `UI Selector`, `UI Reselector`, `Upgrade Button`, `Character UI Button`,
   `Inventory Display`, `Item Name`, `Item Description`, `Tracked Quest Object`, etc.
   This is a direct implementation path for the tier-2 focus doctrine (engage via the
   game's own anchors) and could replace many of the mod's hardcoded `GameObject.Find`
   paths with the game's own pointers. (globals_dump.txt.)
4. **The persistent manager stack lives in the TITLE scene.** CS Dialogue Manager
   (dialogue + saves + Ink + input device management) and the Rewired Input Manager are
   level0 roots; level1 contains neither. The game only works via title → main flow, and
   mod init that waits for level1 objects will still find these managers pre-existing
   from level0. (scene_survey.txt level0/level1 root lists.)
5. **Drives are Dialogue System quests.** StandardUIQuestTracker + UnityUIQuestLogWindow
   + `CheckQuestCompletion` + `Tracked Quest Object` + DialogueSystemSaver. The
   authoritative drive state is the Dialogue System Lua database (QuestLog API), which
   the mod never queries — it scrapes the rendered HUD. A QuestLog-based C query would be
   strictly richer and still rendered-sanctioned (quest titles/states are shown in the
   drive log window).
6. **Dialogue System is source-compiled into Assembly-CSharp-firstpass** — no
   PixelCrushers DLL. Version not recoverable statically. The scene uses
   `StandardDialogueUI` (sharedassets0 prefab); `UnityUIDialogueUI` appears in no scene
   or asset census, so the mod's UnityUIDialogueUI patches are dead code (harmless, but
   the Standard* patches are the live ones — except UnityUIQuestLogWindow, which IS the
   live drive-log window class).
7. **No sprite atlas exists.** The parked "map-marker glyph atlas" assumption is false:
   zero SpriteAtlas objects in all containers; marker glyphs are loose Sprites. Glyph
   transcoding must map sprite names at runtime.
8. **Narrative storage confirmed and localized ×4.** All story text is four `CS-Main Ink`
   compiled-Ink TextAssets (inkVersion 19) in sharedassets0 plus the `CS Database
   Repaired` DialogueDatabase and two TextTables. UI-string localization runs through
   4,627 LocalizeUI components — any mod feature that string-matches rendered labels is
   language-fragile by design.
9. **Inventory is FSM + globals, not MoreMountains.** InventoryEngine DLLs ship and demo
   item assets sit in resources.assets, but no InventoryEngine component appears in any
   scene; the game's inventory is the `Bottom UI/Inventory` FSM plus ~24 `* Item`
   GameObject globals. Do not hook InventoryEngine.
10. **Unmapped load-bearing systems the mod is blind to:** `Saver` root FSM + autosave
    globals (pause menu already renders autosave age); `Character Notifications` root
    (Drive/Perk/Breakdown notification stacks — game-rendered popups the mod never
    announces; only DS alerts are hooked); `One-shot Autoplay Scenes` (95 scripted scene
    players, gated by `Autoplay Waiting`/`Scenes Active?` globals — the "flurry" moments);
    `Cycle RNGs` (Scrap Freighter RNG, Ship Position RNG — per-cycle world changes);
    `Ending Controller`/`Flux Ending Controller`/`End Credits`/`Cinematic Effects
    (Stings)` (endgame flow, will eventually need modality handling); `Jukebox`;
    `Map Key` (a rendered legend — useful transcode source for marker glyph meanings).
11. **Both input modules coexist** on every EventSystem (Standalone + Rewired). Which one
    dispatches submit/navigation events matters for synthetic input; the Rewired module
    is presumably active with the Standalone as editor fallback — verify live, since the
    mod's Enter/arrow synthesis rides whichever module is processing.
12. **Tutorial census:** 11 tutorial panels (not an open set), 3 triggers, 1 Input
    Pauser, 1 shared continue Button. Five panels are marked (PROMPTS) — exactly the ones
    needing report-1 prompt transcoding. ControllerIconPrompt has Default/PS/Xbox/Switch
    variants only — the game cannot render keyboard prompts, confirming the transcode
    design direction.
13. **Scale numbers for planning:** 110 location canvases, 177 station action groups, 40
    hacking action groups, 39 character objects, 49 drive triggers, 51 dialogue actors,
    918 buttons in level1. Any per-object registration approach must be lazy.

## OPEN QUESTIONS (need live observation or deeper dives)

1. Where do `Player_*`, `IntroComplete`, `C_*` actually live at runtime — runtime-added
   globals (visible in `FsmVariables.GlobalVariables`) or FSM locals only? (Surprise 2;
   subsystem agents' territory overlaps here.)
2. Are the GameObject anchor globals (surprise 3) kept current by the game in all modes,
   and is reading them mid-transition safe?
3. Which of the four `CS-Main Ink` assets maps to which language, and does
   `DialogueSystemInkIntegration.currentLanguage` follow `LocalizationManager`?
4. Which input module actually dispatches UI events (surprise 11), and what are the
   Rewired action names (enumerate live via `ReInput.mapping`)?
5. Is the Dialogue System Lua quest table the authoritative drive state at runtime, and
   do quest display names match the rendered drive log (spoiler-safe read path)?
6. Input Pauser's actual gating scope (long-standing; docs section 8 task 4).
7. What exactly does DEBUG Intro Skipper set (docs section 7 caution) — its FSM blob is
   readable statically if a clean post-intro test state is wanted.
8. Whether `Saving`/`Saver` activity windows should suppress announcements (autosave
   throbber is a rendered state the mod could speak).
