"""Extract the candidate UI state map from level1.

For each subtree of interest, list GameObjects that carry a PlayMakerFSM,
with the FSM's recoverable state/event/variable name strings (ASCII runs
from the serialized blob, filtered against known action-class noise).
Output: markdown-ish text for the state map document.
"""
import UnityPy, struct, re

DATA = r"C:\Program Files (x86)\Steam\steamapps\common\Citizen Sleeper\Citizen Sleeper_Data"
env = UnityPy.load(DATA + r"\level1")
gos = {o.path_id: o for o in env.objects if o.type.name == "GameObject"}
by_pid = {}
for asset in env.assets:
    by_pid.update(asset.objects)

# transform helpers
def transform_of(go_reader):
    for c in go_reader.m_Component:
        e = by_pid.get(c.component.path_id)
        if e is not None and e.type.name in ("Transform", "RectTransform"):
            return e.read()
    return None

def children_of(tr):
    out = []
    for ch in getattr(tr, "m_Children", []):
        e = by_pid.get(ch.path_id)
        if e is None:
            continue
        ctr = e.read()
        gpid = ctr.m_GameObject.path_id
        if gpid in gos:
            out.append((gos[gpid].read(), ctr))
    return out

NOISE = re.compile(
    r"^(HutongGames|PixelCrushers|UnityEngine|System\.|com\.|FMOD|TMPro|"
    r"FINISHED$|SendEvent$|.*\.cs$|m_|Assembly)"
)

def fsm_strings(raw, cap=40):
    runs = [s.decode() for s in re.findall(rb"[ -~]{3,}", raw)]
    keep, seen = [], set()
    for s in runs:
        if NOISE.match(s) or len(s) > 48:
            continue
        if s in seen:
            continue
        seen.add(s)
        keep.append(s)
        if len(keep) >= cap:
            break
    return keep

def describe(go_reader, indent, depth):
    fsm_notes = []
    for c in go_reader.m_Component:
        e = by_pid.get(c.component.path_id)
        if e is None or e.type.name != "MonoBehaviour":
            continue
        raw = e.get_raw_data()
        # PlayMakerFSM blobs contain this marker string
        if b"HutongGames" in raw or b"fsm" in raw[:200]:
            fsm_notes.append(fsm_strings(raw))
    tag = f" [FSM x{len(fsm_notes)}]" if fsm_notes else ""
    active = "" if go_reader.m_IsActive else " (INACTIVE)"
    print(f"{indent}{go_reader.m_Name}{active}{tag}")
    for notes in fsm_notes:
        print(f"{indent}  strings: {', '.join(notes)}")
    if depth > 0:
        tr = transform_of(go_reader)
        if tr:
            for cgo, ctr in children_of(tr):
                describe(cgo, indent + "  ", depth - 1)

TARGETS = [
    ("Intro Sequence", 3),
    ("Gamepad Manager", 1),
    ("UI Reselector", 1),
    ("Focus Rotator", 1),
    ("Location Controller", 1),
    ("PAUSE", 2),
    ("DEBUG Intro Skipper", 1),
]

# find roots by name
roots = {}
for pid, entry in by_pid.items():
    if entry.type.name not in ("Transform", "RectTransform"):
        continue
    tr = entry.read()
    f = getattr(tr, "m_Father", None)
    if f is None or f.path_id == 0:
        gpid = tr.m_GameObject.path_id
        if gpid in gos:
            roots[gos[gpid].read().m_Name] = gos[gpid].read()

for name, depth in TARGETS:
    if name in roots:
        print(f"\n===== {name} =====")
        describe(roots[name], "", depth)

# Letterbox Canvas: just its 18 direct children with FSM flags (depth 1)
print("\n===== Letterbox Canvas (direct children) =====")
describe(roots["Letterbox Canvas"], "", 1)
