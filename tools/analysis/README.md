# Static analysis scripts (UnityPy)

Working patterns for reading Citizen Sleeper's serialized scenes and FSM blobs.
Python 3.13 with `UnityPy` and `TypeTreeGeneratorAPI` installed (pip, machine-wide).

Game data root (hardcoded in scripts):
`C:\Program Files (x86)\Steam\steamapps\common\Citizen Sleeper\Citizen Sleeper_Data`
Scenes: `level0`–`level2`. Managed assemblies: `Managed\`.

- `dump_state_map.py` — walk a scene subtree, list GameObjects carrying PlayMakerFSM
  with recoverable state/event/variable name strings (ASCII-run extraction from the
  serialized blob, noise-filtered). Source for docs/ui-state-map.md.
- `dump_endcycle_wiring.py` — read a Button's serialized `m_OnClick` persistent calls
  (target path_id + method + string arg). How the End Cycle sanctioned path was proven.
- `resolve_sendevent.py` — scene-wide scan for FSMs referencing a named event.
- `census.py` — strings-scan census across scene files (controller prompt census origin).

PlayMakerFSM blobs are binary typetree data; full parse needs TypeTreeGeneratorAPI
(Cecil-backed, can also enumerate assembly classes), but ASCII-run extraction recovers
state/event/variable names reliably. Rewired action names are NOT strings-recoverable.

Discipline: read-only against the game install; scripts write output to files, never
modify game data. Never quote Ink/dialogue/outcome narrative prose in reports —
structural identifiers only (spoiler rule).
