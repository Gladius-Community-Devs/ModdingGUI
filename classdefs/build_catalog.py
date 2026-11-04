"""
build_catalog.py — classdefs catalog generator
==============================================

Scans `classdefs.tok` and produces `classdefs_catalog.json`: every key the file uses,
the argument shapes observed for it, its repeat behaviour, and the engine field id from
`dol/classdefs_fields.json` (extracted from the GameCube boot DOL).

The catalog is consumed by `ModdingGUI.Core.Classes.ClassDefsCatalog`; it is data, not
code, so new keys/values discovered by modders need no C# change. Regenerate with:

    py -3 classdefs/build_catalog.py

Inputs (sibling of this script unless --source is given):
    ../clean-bec-unpacked/data/config/classdefs.tok  (or --source <path>)
    ../do../dol/classdefs_fields.json                (engine truth)

The copyrighted .tok input is not shipped; the generated catalog is.
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
REPO = HERE.parent
DEFAULT_SOURCE = REPO.parent / "clean-bec-unpacked" / "data" / "config" / "classdefs.tok"
DEFAULT_ENGINE = REPO / "dol" / "classdefs_fields.json"
OUT_PATH = HERE / "classdefs_catalog.json"

HEADER_RE = re.compile(r"^CREATECLASS:\s*(\S+)")
KEY_RE = re.compile(r"^\s*([A-Z][A-Z0-9]*)\s*:\s*(.*)$")

# A quoted string, a decimal, an integer, or a bare word (weapon / Bow / <Any>).
TOKEN_RE = re.compile(
    r"""
    "([^"]*)"                     # 1: quoted string
    | (-?\d+\.\d+)                # 2: float
    | (-?\d+)                     # 3: int
    | ([A-Za-z_<][\w<>' \-]*)     # 4: bare word (may contain <Any>)
    """,
    re.VERBOSE,
)

# Keys whose values are better treated as opaque text in the catalog.
FREE_TEXT = {"SKILLUSENAME", "UNITSETNAME", "MESH", "VOICELINEPREFIX", "SOUNDTABLENAME"}


def tokenize(value: str) -> list[tuple[Any, str]]:
    """Split a classdefs value into (value, type) tokens."""
    out: list[tuple[Any, str]] = []
    for match in TOKEN_RE.finditer(value):
        if match.group(1) is not None:
            out.append((match.group(1), "string"))
        elif match.group(2) is not None:
            out.append((float(match.group(2)), "float"))
        elif match.group(3) is not None:
            out.append((int(match.group(3)), "int"))
        else:
            out.append((match.group(4).strip(), "string"))
    return out


def parse_classes(text: str) -> list[dict[str, Any]]:
    """Split classdefs.tok into blocks of (key, raw value) pairs, in source order."""
    classes: list[dict[str, Any]] = []
    current: dict[str, Any] | None = None

    for raw_line in text.replace("\r\n", "\n").split("\n"):
        line = raw_line.rstrip()
        header = HEADER_RE.match(line)
        if header:
            current = {"name": header.group(1), "fields": []}
            classes.append(current)
            continue
        if current is None:
            continue
        stripped = line.strip()
        if not stripped or stripped.startswith("//"):
            continue
        match = KEY_RE.match(line)
        if match:
            current["fields"].append((match.group(1), match.group(2).strip()))
    return classes


def build_field_stats(classes: list[dict[str, Any]]) -> dict[str, Any]:
    """Per-key argument shapes, observed values and repeat behaviour.

    Slot positions are *argument* positions (ITEMCAT has three: category, subcategory,
    style), not occurrence indices — a key that repeats still has one shape.
    """
    slots: dict[str, dict[int, dict[str, set]]] = defaultdict(lambda: defaultdict(
        lambda: {"types": set(), "values": set()}))
    max_occurrences: Counter = Counter()
    classes_using: Counter = Counter()

    for cls in classes:
        per_key: Counter = Counter()
        for key, raw in cls["fields"]:
            per_key[key] += 1
            classes_using[key] += 1
            for position, (value, kind) in enumerate(tokenize(raw)):
                slot = slots[key][position]
                slot["types"].add(kind)
                # Bare-word arguments are enumerable; free-text names are not useful
                # as an enum, and quoted strings are usually references to other files.
                if kind == "string" and key not in FREE_TEXT:
                    slot["values"].add(str(value))
        for key, count in per_key.items():
            max_occurrences[key] = max(max_occurrences[key], count)

    fields: dict[str, Any] = {}
    for key in sorted(slots):
        by_position = slots[key]
        ordered = [by_position[position] for position in sorted(by_position)]
        fields[key] = {
            "multi": max_occurrences[key] > 1,
            "max_occurrences": max_occurrences[key],
            "classes_using": classes_using[key],
            "arity": len(ordered),
            "slots": [
                {"types": sorted(slot["types"]), "values": sorted(slot["values"])}
                for slot in ordered
            ],
            "enumerate": any(slot["values"] for slot in ordered),
        }
    return fields


def annotate_engine(fields: dict[str, Any], engine: dict[str, dict[str, Any]]) -> Counter:
    stats: Counter = Counter()
    for key, spec in fields.items():
        row = engine.get(key)
        spec["engine"] = row is not None
        if row is None:
            stats["engine_unknown"] += 1
            continue
        spec["engine_id"] = row["id"]
        stats["engine_known"] += 1

    for name, row in engine.items():
        if name in fields or name in ("CREATECLASS", "NUMCLASSDEFS"):
            continue
        fields[name] = {
            "engine": True,
            "engine_id": row["id"],
            "multi": False,
            "max_occurrences": 1,
            "classes_using": 0,
            "slots": [],
            "enumerate": False,
            "engine_only": True,
        }
        stats["engine_only"] += 1
    return stats


def summarise_classes(classes: list[dict[str, Any]]) -> dict[str, Any]:
    """Small per-class summary so consumers can list/inspect without parsing .tok."""
    summary: dict[str, Any] = {}
    for cls in classes:
        first: dict[str, str] = {}
        for key, raw in cls["fields"]:
            first.setdefault(key, raw.strip().strip('"'))
        summary[cls["name"]] = {
            "displayNameId": _as_int(first.get("DISPLAYNAMEID")),
            "descriptionId": _as_int(first.get("DESCRIPTIONID")),
            "mesh": first.get("MESH"),
            "skillUseName": first.get("SKILLUSENAME"),
            "keys": sorted({k for k, _ in cls["fields"]}),
        }
    return summary


def _as_int(value: str | None) -> int | None:
    if value is None:
        return None
    try:
        return int(value)
    except ValueError:
        return None


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate classdefs_catalog.json")
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--engine", type=Path, default=DEFAULT_ENGINE)
    parser.add_argument("--out", type=Path, default=OUT_PATH)
    args = parser.parse_args()

    if not args.source.exists():
        raise SystemExit(f"classdefs.tok not found: {args.source} (pass --source)")

    text = args.source.read_text(encoding="latin-1")
    classes = parse_classes(text)
    fields = build_field_stats(classes)

    engine: dict[str, dict[str, Any]] = {}
    if args.engine.exists():
        for row in json.loads(args.engine.read_text(encoding="utf-8")):
            engine[row["name"]] = row

    engine_stats = annotate_engine(fields, engine)

    catalog = {
        "_meta": {
            "format_version": 1,
            "generated_from": [args.source.name],
            "class_count": len(classes),
            "engine": {
                "source": "GameCube bootfile.dol classdefs field table (0x803038B0)",
                "fields_known": engine_stats["engine_known"],
                "fields_added_from_engine": engine_stats["engine_only"],
                "fields_not_in_engine": engine_stats["engine_unknown"],
            },
        },
        "fields": fields,
        "classes": summarise_classes(classes),
    }

    args.out.write_text(json.dumps(catalog, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    print(f"Wrote {args.out}")
    print(f"  classes:        {len(classes)}")
    print(f"  keys:           {len(fields)}")
    print(f"  engine known:   {engine_stats['engine_known']} (+{engine_stats['engine_only']} engine-only)")
    print(f"  engine unknown: {engine_stats['engine_unknown']}")


if __name__ == "__main__":
    main()
