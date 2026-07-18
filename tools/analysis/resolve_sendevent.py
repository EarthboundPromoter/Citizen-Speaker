"""Resolve the End Cycle button's SendEvent target and string argument."""
import UnityPy

DATA = r"C:\Program Files (x86)\Steam\steamapps\common\Citizen Sleeper\Citizen Sleeper_Data"
env = UnityPy.load(DATA + r"\level1")
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
gen = TypeTreeGenerator("2021.3.33f1")
gen.load_local_game(r"C:\Program Files (x86)\Steam\steamapps\common\Citizen Sleeper")
env.typetree_generator = gen

gos = {o.path_id: o for o in env.objects if o.type.name == "GameObject"}
by_pid = {}
for asset in env.assets:
    by_pid.update(asset.objects)

# 1. The full onClick call including arguments, from the known Button pid 222582's owner.
for btn_pid in (222582, 195471):
    try:
        tree = by_pid[btn_pid].read_typetree()
    except Exception:
        # fall back: re-locate via the GameObject
        continue

# Simpler: re-read the Empty Container button GameObject (pid 22736) components raw.
go = gos[22736].read()
for comp in go.m_Component:
    cpid = comp.component.path_id
    entry = by_pid.get(cpid)
    if entry is None or entry.type.name != "MonoBehaviour":
        continue
    try:
        tree = entry.read_typetree()
    except Exception as e:
        print(f"pid {cpid}: unreadable ({e})")
        continue
    if "m_OnClick" not in tree:
        continue
    for c in tree["m_OnClick"]["m_PersistentCalls"]["m_Calls"]:
        args = c.get("m_Arguments", {})
        print("method:", c.get("m_MethodName"))
        print("mode:", c.get("m_Mode"))
        print("string arg:", repr(args.get("m_StringArgument")))
        print("target pid:", c.get("m_Target", {}).get("m_PathID"))
        tgt = by_pid.get(c["m_Target"]["m_PathID"])
        if tgt is not None:
            ttree = tgt.read_typetree()
            owner_pid = ttree.get("m_GameObject", {}).get("m_PathID")
            owner = gos.get(owner_pid)
            oname = owner.read().m_Name if owner else "?"
            print("target component on GameObject:", repr(oname))
            # walk its parent chain for context
            chain = []
            tr_pid = None
            ogo = owner.read()
            for oc in ogo.m_Component:
                if oc.component.path_id in by_pid and by_pid[oc.component.path_id].type.name in ("Transform", "RectTransform"):
                    tr = by_pid[oc.component.path_id].read()
                    for _ in range(8):
                        f = getattr(tr, "m_Father", None)
                        if not f or f.path_id == 0 or f.path_id not in by_pid:
                            break
                        tr = by_pid[f.path_id].read()
                        gpid = tr.m_GameObject.path_id
                        chain.append(gos[gpid].read().m_Name if gpid in gos else "?")
                    break
            print("target chain:", " < ".join(chain) or "(root)")
            # if it's a PlayMakerFSM, try to get its FSM name from raw bytes
            try:
                raw = tgt.get_raw_data()
                print("target MonoBehaviour raw size:", len(raw))
            except Exception:
                pass
