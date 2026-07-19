"""Stream fsm-census.jsonl once, extract structural info for a set of named
mechanisms into separate output files. Never emit narrative text: string param
values are redacted unless they look like structural identifiers (start with
$, event:, target:, go:, or are short single-token strings)."""
import json, re, os

SRC = r"C:\Users\IATPFNJ624\CitizenSleeperAccess\tools\analysis\corpus\fsm-census.jsonl"
OUT = r"C:\Users\IATPFNJ624\CitizenSleeperAccess\tools\analysis\scratch_out"
os.makedirs(OUT, exist_ok=True)

STRUCTURAL_PREFIX = ("$", "event:", "target:", "go:")

def safe_val(v):
    if isinstance(v, str):
        if v.startswith(STRUCTURAL_PREFIX):
            return v
        if len(v) <= 40 and not re.search(r"[.!?,;:]", v) and "\n" not in v:
            return v
        return "<text len=%d>" % len(v)
    if isinstance(v, (int, float, bool)) or v is None:
        return v
    if isinstance(v, list):
        return [safe_val(x) for x in v]
    if isinstance(v, dict):
        return {k: safe_val(x) for k, x in v.items()}
    return "<obj>"

def compact_fsm(d, include_actions=True):
    out = {
        "path": d.get("path"),
        "fsm": d.get("fsm"),
        "active": d.get("active"),
        "startState": d.get("startState"),
        "states": [],
        "globalTransitions": d.get("globalTransitions"),
        "events": d.get("events"),
        "variables": d.get("variables"),
    }
    for st in d.get("states", []):
        s = {"name": st.get("name"), "transitions": st.get("transitions")}
        if include_actions:
            acts = []
            for a in st.get("actions", []):
                params = a.get("params")
                if isinstance(params, dict):
                    params = {k: safe_val(v) for k, v in params.items()}
                acts.append({"type": a.get("type"), "params": params})
            s["actions"] = acts
        out["states"].append(s)
    return out

# name -> (match function on path's last segment or full path, output filename)
MATCHERS = []

def by_leaf(name):
    return lambda d: d.get("path", "").rstrip("/").split("/")[-1] == name

def by_leaf_in(names):
    return lambda d: d.get("path", "").rstrip("/").split("/")[-1] in names

def by_path_contains(sub):
    return lambda d: sub in d.get("path", "")

def by_leaf_contains(sub):
    return lambda d: sub in d.get("path", "").rstrip("/").split("/")[-1]

JOBS = [
    ("action_controller", by_leaf("Action Controller")),
    ("dice_gamepad_system", by_leaf("Dice Gamepad System")),
    ("gamepad_dice_slot", by_leaf("Gamepad Dice Slot")),
    ("dice_cursor", by_leaf_contains("Dice Cursor")),
    ("dice_slot_button", by_leaf("Dice Slot Button")),
    ("input_pauser", by_leaf("Input Pauser")),
    ("character_notifications_fsm", by_path_contains("Character Notifications")),
    ("pause_canvas_fsm", by_path_contains("PAUSE")),
    ("drive_log_fsm", by_path_contains("Drive Log")),
    ("drive_system_fsm", by_path_contains("Drive System")),
    ("character_ui_button", by_leaf("Character UI Button")),
    ("character_window_fsm", by_path_contains("Character Window")),
    ("cycle_controller", by_leaf("Cycle Controller")),
    ("energy_bar_system", by_leaf("Energy Bar System")),
    ("condition_system", by_leaf("Condition System")),
    ("step_clock", by_leaf_contains("Step ")),
    ("modifier", by_leaf_contains("Modifier")),
    ("leave_button", by_leaf("Leave Button")),
    ("scan_button", by_path_contains("Scan Button")),
    ("reroll", by_path_contains("REROLL")),
    ("action_groups_scope", by_path_contains("Action Groups")),
    ("ui_selector", by_leaf("UI Selector")),
    ("response_menu", by_leaf_contains("Response Menu")),
    ("dialogue_panel", by_leaf("Dialogue Panel")),
    ("tutorial_system", by_path_contains("Tutorial System")),
    ("gamepad_manager", by_leaf("Gamepad Manager")),
    ("action_skill_display", by_leaf_contains("Skill Display")),
    ("checker", by_leaf_contains("Checker")),
    ("tracked_quest_object", by_path_contains("Drive Tracker HUD")),
]

writers = {name: open(os.path.join(OUT, name + ".jsonl"), "w", encoding="utf-8") for name, _ in JOBS}
counts = {name: 0 for name, _ in JOBS}

total = 0
with open(SRC, encoding="utf-8") as f:
    for line in f:
        total += 1
        d = json.loads(line)
        for name, fn in JOBS:
            try:
                if fn(d):
                    c = compact_fsm(d)
                    writers[name].write(json.dumps(c) + "\n")
                    counts[name] += 1
            except Exception as e:
                pass

for name, _ in JOBS:
    writers[name].close()

print("total lines:", total)
for name, _ in JOBS:
    print(f"{name}: {counts[name]}")
