# Development notes

Moved from the pre-0.8 root README when it became the user-facing document.
Architecture and the work plan live in [build-plan.md](build-plan.md); the input
design in [input-model.md](input-model.md); ground-truth decodes under
[verification/](verification/).

## Layout

- `mod/` — **CSAccess**, the accessibility mod itself (`CSAccess.dll`).
- `bridge/` — **CSAccessBridge**, a dev-only localhost HTTP bridge (port 8330)
  used to inspect the running game during development. Observation-only by rule;
  local-only and untracked (gitignored) — it never ships.
- `tools/` — analysis tooling (UnityPy serialized-statics readers, the FSM
  corpus; corpus jsonl files are untracked and regenerable via the bridge's
  `/fsmcensus`).

## Dev install

1. BepInEx 5 x64 extracted into the game folder.
2. `Tolk.dll` + `nvdaControllerClient64.dll` beside `Citizen Sleeper.exe`.
3. `steam_appid.txt` containing `1578650` beside the exe lets you launch the exe
   directly (Steam must be running).
4. Build and deploy either plugin:
   ```
   cd mod    && dotnet build -c Release   # -> copy bin/Release/CSAccess.dll       to <game>/BepInEx/plugins/
   cd bridge && dotnet build -c Release   # -> copy bin/Release/CSAccessBridge.dll to <game>/BepInEx/plugins/
   ```
5. Snapshot `BepInEx/LogOutput.log` on game exit before relaunching — BepInEx
   overwrites it per launch. Every utterance logs as `[Speech:<source>]`.
