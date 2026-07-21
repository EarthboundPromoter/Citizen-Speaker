# Citizen Speaker

**Version 0.8 — beta**

A screen-reader mod for **Citizen Sleeper**: it speaks the game's interface, dice,
clocks, and story through NVDA or JAWS (via [Tolk](https://github.com/dkager/tolk))
so the game can be played without sight, start to finish, entirely by keyboard.

Beta — the whole game surface is built and almost all of it has been verified in
live play. Please report anything that sounds wrong or stays silent when it
shouldn't.

## About Citizen Sleeper

From the [Steam store page](https://store.steampowered.com/app/1578650/Citizen_Sleeper/):

> One of 2022's most acclaimed indies, the Game Awards-nominated Citizen Sleeper is a Tabletop-inspired narrative RPG set on Erlin's Eye, a ruined space station that is home to thousands of people trying to survive on the edges of an interstellar capitalist society.
>
> You are a sleeper, a digitised human consciousness in an artificial body, owned by a corporation that wants you back. Thrust amongst the unfamiliar and colourful inhabitants of the Eye, you need to build friendships, earn your keep, and navigate the factions of this strange metropolis, if you hope to survive to see the next cycle.
>
> Inspired by Tabletop Roleplaying games, Citizen Sleeper uses Dice, Clocks and Drives to create a player-led experience, where you choose your path in a rich and responsive world. Each cycle you use the dice you are dealt to choose what to do with your time. Make or break alliances, uncover truths and escape your hunters. Survive and ultimately thrive, one cycle at a time, and sometimes against the clock!
>
> The station plays host to characters from all walks of life, trying to eke out an existence among the stars. Salvagers, engineers, hackers, bartenders, street-food vendors, each has a history which brought them here. You choose which of them you wish to help, and together you will shape your future.
>
> Eventually, you will learn to use your dice to hack into the station's cloud to access decades of digital data, uncover new areas and unlock secrets. This is your unique power, and with it you can change your future. Corporate secrets, rogue AIs and troves of lost data await those willing to dive into the depths of the station's networks.
>
> Essen-Arp: to them you are just property, one more asset in a portfolio that stretches across the stars. You are the product of an abusive system, in a universe where humanity's expansion is marked by exploitation and extraction. Escape the makers of your decaying body, and chart your own path in a richly imagined, deeply relevant sci-fi world which explores ideas of precarity, personhood and freedom.
>
> Citizen Sleeper now includes all three post-launch DLC episodes FLUX, REFUGE and PURGE, which expand the game and introduce additional characters and locations to Erlin's Eye in an exciting new late game storyline.
>
> — [Citizen Sleeper on Steam](https://store.steampowered.com/app/1578650/Citizen_Sleeper/)

## Requirements

- **[Citizen Sleeper](https://store.steampowered.com/app/1578650/Citizen_Sleeper/)**
  (Steam, Windows).
- A screen reader — **[NVDA](https://www.nvaccess.org/download/)** or **JAWS**
  (or any other reader supported by [Tolk](https://github.com/dkager/tolk);
  the Tolk speech DLLs are bundled in the release zip).
- **[BepInEx 5 (x64)](https://github.com/BepInEx/BepInEx/releases)** — the Unity
  mod loader the mod runs on; installation below.
- Start your screen reader *before* launching the game.

## Installing

1. Install **BepInEx 5 (x64)**: download the latest 5.x `BepInEx_win_x64` zip from
   [BepInEx releases](https://github.com/BepInEx/BepInEx/releases) and extract it
   into the Citizen Sleeper game folder (the one containing `Citizen Sleeper.exe`).
   Launch the game once and quit, so BepInEx creates its folders.
2. Download the latest Citizen Speaker zip from the
   [Releases page](https://github.com/EarthboundPromoter/Citizen-Speaker/releases/latest)
   and extract it: put `CSAccess.dll` into `BepInEx/plugins/`, and `Tolk.dll` +
   `nvdaControllerClient64.dll` beside `Citizen Sleeper.exe`.
3. Launch the game. You'll hear "Citizen Sleeper Access loaded. Press F1 for
   commands." To update, replace `CSAccess.dll` with the newer one.

## How the mod works

Everything the game shows, it speaks: dialogue reads automatically with numbered
choices, dice, clocks, meters and outcomes are announced as they change, and menus
talk as you move through them.

The station and the cloud are browsed as **tables**. Up and Down walk the rows
(locations, characters, or network nodes — the camera follows), Left and Right
step through a row's columns, Space reads the whole row, and Enter goes there.
At the station, Slash switches tabs (station zones, characters, tracked drives).
At a location, the same grammar continues: rows are the action cards and clocks,
and Enter on an action starts it — pick a die with the arrows, Enter to slot it,
and Enter again to begin. Results, clock ticks, and anything newly unlocked are
spoken as they happen; press N anytime to replay the last station change.

F1 always speaks the keys that work on the current screen.

## Keys

| Key | Function |
|-----|----------|
| **Arrows** | Move through rows and columns (tables, dialogue, menus, die picker). |
| **Enter** | Activate / commit the current row / skip credits. |
| **Space** | Read the full current row or focused element. |
| **Slash** | Next tab (station table). |
| **Backspace** | Back / cancel. |
| **1–9** | Pick a dialogue response. |
| **C** | Vitals — cycle, energy, condition, cryo. |
| **V** | Dice. |
| **K** | Clocks. |
| **N** | Last station change. |
| **L** | Where am I. |
| **Z** | Repeat last speech. |
| **R** | Re-read the current dialogue line or tutorial. |
| **Shift + R** | Reroll dice (once you own the perk for it). |
| **I** | Inventory. |
| **U** | Character window (skills and upgrades). |
| **J** | Drive log (quests). |
| **O** | Toggle the cloud scan. |
| **T** | Continue a tutorial popup. |
| **Ctrl + X** | Switch between table navigation and the game's native focus navigation. |
| **F1** | Contextual help. |
| **F3** | Write a diagnostic snapshot to the log (useful in bug reports). |
| **Esc** | Pause (the game's own). |

## What's tested and what isn't

Everything should work: the full station loop, dialogue, dice, locations, clocks,
the cloud, hacking, skills and perks, the journal, and the save slots have all
been verified in live blind play. Two things have **not** been live tested yet:

- **Zone transitions** — the tabs that carry you between station zones later in
  the game (the machinery is built and the rules are conservative, but no live
  run has crossed them yet).
- **The endings** — the end-of-game sequences speak provisionally ("End
  sequence", then "Credits. Enter to skip"), and the credits themselves are a
  placeholder announcement rather than a full reading. Enter reveals and presses
  the game's skip button, which the game otherwise only offers to mouse and
  controller players.

**Known issue:** selecting a cloud network node has an intermediate step with no
feedback yet — opening a node takes one more Enter press than a typical action
card. Press Enter again if a node seems unresponsive.

## Privacy

The mod makes no network connections and collects nothing. (The repository also
contains a developer-only debugging bridge; it is not part of releases.)

## License and credits

This mod is released under the [MIT License](LICENSE). It contains no game
assets or game content.

- **Citizen Sleeper** by [Jump Over The Age](https://www.jumpovertheage.com/) —
  created and developed by **Gareth Damian Martin** — published by
  [Fellow Traveller](https://www.fellowtraveller.games/). Buy the game on
  [Steam](https://store.steampowered.com/app/1578650/Citizen_Sleeper/).
- [Tolk](https://github.com/dkager/tolk) by Davy Kager
  ([license](TOLK_LICENSE.txt)).
- [NVDA](https://www.nvaccess.org/) by NV Access
  ([controller client license](NVDA_LICENSE.txt)).
