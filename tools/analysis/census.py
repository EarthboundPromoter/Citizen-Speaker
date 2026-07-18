"""Census controller-prompt UI elements from Citizen Sleeper scene data.
Unity stores GameObject names as length-prefixed UTF-8; a plain ASCII-run scan
recovers them. We collect names matching prompt/button patterns."""
import re, sys, collections

FILES = [
    r"C:\Program Files (x86)\Steam\steamapps\common\Citizen Sleeper\Citizen Sleeper_Data\level0",
    r"C:\Program Files (x86)\Steam\steamapps\common\Citizen Sleeper\Citizen Sleeper_Data\level1",
    r"C:\Program Files (x86)\Steam\steamapps\common\Citizen Sleeper\Citizen Sleeper_Data\level2",
    r"C:\Program Files (x86)\Steam\steamapps\common\Citizen Sleeper\Citizen Sleeper_Data\resources.assets",
    r"C:\Program Files (x86)\Steam\steamapps\common\Citizen Sleeper\Citizen Sleeper_Data\sharedassets2.assets",
]

ascii_run = re.compile(rb"[\x20-\x7e]{4,}")

prompt_names = collections.Counter()
action_names = collections.Counter()
button_names = collections.Counter()

# Rewired action names typically look like short PascalCase/UI tokens; we grep
# for known-context markers instead of guessing.
PROMPT_PAT = re.compile(r"prompt", re.I)
BUTTON_PAT = re.compile(r"button", re.I)
REWIRED_HINTS = ("UISubmit", "UICancel", "UIHorizontal", "UIVertical", "Scan", "Leave",
                 "Reroll", "ZoomIn", "ZoomOut", "Map", "Inventory", "Character",
                 "Drive", "Cycle", "Skip", "Pause")

hint_hits = collections.Counter()

for path in FILES:
    try:
        data = open(path, "rb").read()
    except OSError as e:
        print("skip", path, e)
        continue
    for m in ascii_run.finditer(data):
        s = m.group().decode("ascii", "ignore")
        if len(s) > 80:
            continue
        if PROMPT_PAT.search(s):
            prompt_names[s] += 1
        elif BUTTON_PAT.search(s):
            button_names[s] += 1
        for h in REWIRED_HINTS:
            if s == h:
                hint_hits[s] += 1

print("=== PROMPT-NAMED OBJECTS ===")
for name, n in sorted(prompt_names.items()):
    print(f"{n:4d}  {name}")
print()
print("=== EXACT REWIRED-HINT TOKENS ===")
for name, n in sorted(hint_hits.items()):
    print(f"{n:4d}  {name}")
print()
print("=== BUTTON-NAMED OBJECTS (top 60 by count) ===")
for name, n in button_names.most_common(60):
    print(f"{n:4d}  {name}")
