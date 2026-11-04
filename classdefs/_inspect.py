import json

d = json.load(open("classdefs/classdefs_catalog.json", encoding="utf-8"))
f = d["fields"]
for k in ("ITEMCAT", "ATTRIBUTE", "LEVELSTATAWARDS", "XPAWARD", "ITEMSIZES",
          "GRIDSIZE", "MESH", "AFFINITY", "SHADOWSET", "ITEMINDIV", "BASECLASSNAME"):
    s = f.get(k)
    if not s:
        print(k, "MISSING")
        continue
    vals = s["slots"][0]["values"][:6] if s["slots"] else []
    print(f"{k:18s} arity={s.get('arity')} multi={s['multi']} max={s['max_occurrences']} "
          f"engine={s['engine']} id={s.get('engine_id')} vals={vals}")
print()
print("classes:", len(d["classes"]), "| sample:", list(d["classes"])[:5])
print("Amazon:", d["classes"]["Amazon"])
