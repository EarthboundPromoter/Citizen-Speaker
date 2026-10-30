# Changelog

Changes in this fork (aradix85/Citizen-Speaker), on top of Citizen Speaker 0.9.7 by
EarthboundPromoter. Work branch: `fix/class-select-speech`. Every change was verified in
game through a local dev bridge (injected key presses, spoken output read back with frame
numbers). The guiding rule: stay as close as possible to the game's own behaviour, driving
its designed controls and FSM events.

## 0.9.8 (2026-10-30)

### Release
- Version 0.9.8, so this fork's build is never mistaken for upstream 0.9.7.
- The release zip is drop-in: it carries BepInEx 5.4.23.5 (x64) with the mod already in
  `BepInEx/plugins/`, plus the Tolk speech DLLs. Extract into the game folder; there is
  no separate BepInEx install. README install steps updated to match.
- Smoke-tested on Citizen Sleeper 1.4.14 from a clean install: BepInEx loads the mod,
  Tolk finds NVDA, the title speaks, the main menu walks, F1 answers, and Backspace
  leaves the slot list; no errors or warnings in the log.

### Action cards, dice, cycle end, tutorials, the cloud
- A spent card (a critical action already used, an opened cloud gate) reads what the card
  shows, "ACTION UNAVAILABLE", in the focus read, the location row and the cloud card
  row, instead of "Requires a die" / "Takes SOLHEIM CIPHER". Enter refuses with the same
  words. The card keeps its Gamepad Dice Slot interactable, so the mod took it for
  activatable; the Action Controller state (Action Completed and kin) now decides. A spent
  cloud card reads its rendered outcome text ("the old gate flips open") instead of the
  authored before-text.
- Cloud card row: "Takes SOLHEIM CIPHER. SOLHEIM CIPHER." says the item once.
- Enter on a die action with every die spent says "No dice left." instead of opening a
  picker with nothing to pick.
- END CYCLE blocked by pending scenes names them: "Complete active scenes to proceed.
  Active scenes: DRAGOS." (characters whose marker rests in Click to Play).
- C (vitals) says "Cryo 0" before the first cryo; Player_Bits is unset until then and the
  cell was dropped.
- Stepping through a tutorial with the arrows names the keys, as the full read does
  ("Press the O key to toggle this view", was "Press to toggle this view").

### Keys during the opening splash
- The engine splash (the logo crawl before the title) runs for about 8 s and cannot be
  skipped; nothing is selectable. A key there said "Nothing to focus." (or nothing at all
  in the first 8 s), as if the game had hung. Any key now says "Loading, please wait.",
  read from SplashScreen.isFinished, which the MAIN MENU FSM's WaitForSplashToFinish
  waits on. The key that launched the game, landing in the first frames, stays silent.
- The title's start button says "Press Enter to start". Its label is "CLICK TO START"
  for the mouse and "PRESS [A] TO START" for a gamepad, and the glyph was dropped, so it
  read "PRESS TO START". After a key during the splash, the landing line is just
  "Main menu." since the button itself now gives the instruction.
- The first arrow at the title no longer speaks the button twice: the game re-selects it
  when the input mode switches to gamepad, in the same frame as the dead-end repeat.
- README: version 0.9.7 (it still said 0.9.5).

### Class select and pause menu
- Class select: a Left/Right press during the ~0.5 s slide is no longer lost. The game
  ignores input mid-slide, so a quick second press spoke the same class again. The mod
  now speaks the class the press asks for and steps there with the game's own arrow once
  the carousel rests.
- Pause menu during a choice: focus stays on the pause menu. The mod's response-focus
  watcher pulled focus back to the first choice every 0.4 s, so arrows walked the
  choices behind the pause and Enter picked one while paused.

### Faster answers to keys
- A dialogue line the player asked for (Enter on Continue, a choice by Enter or number)
  interrupts the line still being read. Tolk_IsSpeaking is unsupported for NVDA and always
  returns false, so Queued lines went straight into NVDA's own queue and the new line had
  to wait behind the old one.
- Watchers answer the player's keys in the same frame (dice slot and odds, pause, drive
  log, character window, title overwrite warning); everything else and all lookups stay on
  the 0.4 s poll.
- "Paused.", "Scan." / "Leaving scan.", the character-table confirm and purchase result
  are Immediate.
- Station table: Enter on a marker that cannot enable refuses at once (was 2 s of
  silence); a table key during the arrival settle answers at once.
- Class select: Left/Right speak the destination at the key press from the carousel FSM
  (was after the ~0.5 s slide), with "class N of 3" in the carousel's own left-to-right
  order; edges say "First class." / "Last class."; a press mid-slide says where it lands.

### Fixes
- Title menus: Backspace goes back (class select, slot lists, overwrite warning,
  language) through the MAIN MENU FSM's own Back event; Rewired "Back" is bound to right
  mouse and gamepad only.
- The queued-duplicate guard dropped any repeat of the last queued line forever; it now
  covers lines still waiting or queued within the last second.
- First Down no longer skips the first row in options, the character table and the drive
  log.
- Drive log: Enter on a name or objectives cell, and Abandon, could close the expanded
  row; both keep it open.
- Options: an unreadable current value no longer makes Left/Right jump to an end.
- Station rows read real "0 of N" clocks again; "Station. Nothing here." no longer speaks
  while the zone is unknown during a load.
- The title overwrite warning and QR canvas speak after a quit to title too.
- Tutorial prompts are read in screen order, controller glyph labels map to the mod's
  keys, and there is no space before punctuation.
- Loading another save no longer speaks changes that never happened in it ("Cryo minus
  340"); energy is not announced twice when the action outcome already said it.
- A choice that ended a conversation no longer leaves the mod in "choosing a response".
- After Enter closes a tutorial and nothing else speaks, the screen is named ("Station
  map."); the key used to go silent.
- The spoken and logged version said 0.9.6.

### Performance
- The mode authority ran several whole-scene searches per frame (~0.8 ms per call, several
  calls per frame). FSM lookups are cached (safe: PlayMakerFSM.FsmList holds exactly the
  active and enabled FSMs) and the pause canvas and focus rotator are held references; a
  mode read now costs ~0.014 ms and station frame rate with and without the mod is within
  noise.
