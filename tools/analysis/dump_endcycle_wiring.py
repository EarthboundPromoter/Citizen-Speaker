"""Dump the End Cycle Dice Slot Button's serialized wiring from level1.

Finds GameObjects named 'Dice Slot Button' whose ancestor chain includes
'End Cycle Action', then prints every component: for Button, the full
m_OnClick persistent call list (target object, method, mode); for
MonoBehaviours, the script class name and (for PlayMakerFSM) the FSM name.
"""
import UnityPy, sys

DATA = r"C:\Program Files (x86)\Steam\steamapps\common\Citizen Sleeper\Citizen Sleeper_Data"
env = UnityPy.load(DATA + r"\level1")

from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
gen = TypeTreeGenerator("2021.3.33f1")
gen.load_local_game(r"C:\Program Files (x86)\Steam\steamapps\common\Citizen Sleeper")
env.typetree_generator = gen

# Build caches: path_id -> parsed object for GameObjects and Transforms.
gos, transforms = {}, {}
for obj in env.objects:
    if obj.type.name == "GameObject":
        gos[obj.path_id] = obj
    elif obj.type.name in ("Transform", "RectTransform"):
        transforms[obj.path_id] = obj

def go_name(pid):
    o = gos.get(pid)
    return o.read().m_Name if o else None

def ancestor_chain(go_reader, depth=8):
    """Walk transform parents, return list of GameObject names upward."""
    chain = []
    # find this GO's transform
    tr = None
    for comp in go_reader.m_Component:
        cpid = comp.component.path_id
        if cpid in transforms:
            tr = transforms[cpid].read()
            break
    while tr is not None and depth > 0:
        father = getattr(tr, "m_Father", None)
        if not father or father.path_id == 0:
            break
        ftr_obj = transforms.get(father.path_id)
        if ftr_obj is None:
            break
        ftr = ftr_obj.read()
        fgo_pid = ftr.m_GameObject.path_id
        chain.append(go_name(fgo_pid))
        tr = ftr
        depth -= 1
    return chain

def describe_pptr(pptr):
    if pptr is None or pptr.path_id == 0:
        return "(null)"
    pid = pptr.path_id
    if pid in gos:
        return f"GameObject '{go_name(pid)}'"
    # component -> resolve owning GameObject
    try:
        r = env.assets[0].objects[pid].read()
        gpid = r.m_GameObject.path_id
        return f"{type(r).__name__} on '{go_name(gpid)}'"
    except Exception:
        return f"(path_id {pid})"

found = 0
for pid, obj in gos.items():
    go = obj.read()
    if go.m_Name != "Dice Slot Button":
        continue
    chain = ancestor_chain(go)
    if "End Cycle Action" not in [c for c in chain if c]:
        continue
    found += 1
    print(f"=== Dice Slot Button pid={pid} chain: {' < '.join(c or '?' for c in chain)}")
    for comp in go.m_Component:
        cpid = comp.component.path_id
        try:
            centry = None
            for asset in env.assets:
                if cpid in asset.objects:
                    centry = asset.objects[cpid]
                    break
            if centry is None:
                print(f"  [component pid={cpid}: not found]")
                continue
            tname = centry.type.name
            if tname == "MonoBehaviour":
                tree = centry.read_typetree()
                script_pid = tree.get("m_Script", {}).get("m_PathID")
                sname = "?"
                for asset in env.assets:
                    if script_pid in asset.objects:
                        try:
                            sname = asset.objects[script_pid].read().m_ClassName
                        except Exception:
                            pass
                        break
                print(f"  MonoBehaviour script={sname}")
                if "fsm" in tree:
                    fsm = tree["fsm"]
                    print(f"    FSM name: {fsm.get('name')!r}  startState: {fsm.get('startState')!r}")
                    for st in fsm.get("states", []):
                        print(f"    state: {st.get('name')!r} actionData actions: {st.get('actionData', {}).get('actionNames')}")
                        for t in st.get("transitions", []):
                            print(f"      transition: {t.get('fsmEvent', {}).get('name')!r} -> {t.get('toState')!r}")
                    for ge in fsm.get("globalTransitions", []):
                        print(f"    GLOBAL transition: {ge.get('fsmEvent', {}).get('name')!r} -> {ge.get('toState')!r}")
                # uGUI Button typetree
                if "m_OnClick" in tree:
                    calls = tree["m_OnClick"].get("m_PersistentCalls", {}).get("m_Calls", [])
                    print(f"  Button.m_OnClick: {len(calls)} persistent call(s)")
                    for c in calls:
                        tgt = c.get("m_Target", {})
                        print(f"    -> target pid={tgt.get('m_PathID')} method={c.get('m_MethodName')!r} mode={c.get('m_Mode')}")
            else:
                print(f"  {tname}")
        except Exception as e:
            print(f"  [error reading component {cpid}: {e}]")
print(f"\nfound {found} matching button(s)")
