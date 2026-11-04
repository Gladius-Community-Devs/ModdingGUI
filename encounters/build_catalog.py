"""
build_catalog.py — encounters catalog generator
===============================================

Scans `data/encounters/**/*.enc` and produces `encounters_catalog.json`: the header keys,
the per-team keys, the `UNITDB` argument count, and a small index of encounter files.

The catalog drives `ModdingGUI.Core.Api.EncounterValidator`, which warns on keys the engine
never sees (a typo'd header is silently ignored in game) and on short/long `UNITDB` rows.

Regenerate with:

    py -3 encounters/build_catalog.py
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
DEFAULT_ROOT = REPO.parent / "clean-bec-unpacked" / "data" / "encounters"
OUT_PATH = HERE / "encounters_catalog.json"

KEY_RE = re.compile(r"^\s*([A-Z][A-Z0-9_]*)(?:\s*:\s*|\s+)(.*)$")
TEAM_RE = re.compile(r"^\s*TEAM\s*:\s*([^,]*),\s*(.*)$")
TOKEN_RE = re.compile(r'"([^"]*)"|(-?\d+\.\d+)|(-?\d+)|([A-Za-z_<][\w<>\' \-]*)')


def token_count(value: str) -> int:
    return len(list(TOKEN_RE.finditer(value)))


def scan(path: Path) -> dict[str, Any]:
    header_keys: Counter = Counter()
    team_keys: Counter = Counter()
    unit_arity: Counter = Counter()
    encounters: list[dict[str, Any]] = []
    teams_total = 0
    units_total = 0

    for file in sorted(path.rglob("*.enc")):
        text = file.read_text(encoding="latin-1")
        name: str | None = None
        in_team = False
        current_switch: int | None = None
        units: dict[int, int] = defaultdict(int)
        switches: list[int] = []

        for raw in text.replace("\r\n", "\n").split("\n"):
            line = raw.strip()
            if not line or line.startswith("//"):
                continue

            team = TEAM_RE.match(raw)
            if team:
                in_team = True
                teams_total += 1
                try:
                    current_switch = int(team.group(1).strip())
                except ValueError:
                    current_switch = None
                if current_switch is not None:
                    switches.append(current_switch)
                continue

            match = KEY_RE.match(raw)
            if not match:
                continue
            key = match.group(1).upper()
            value = match.group(2).strip()

            if in_team:
                if key == "UNITDB":
                    units_total += 1
                    unit_arity[token_count(value)] += 1
                    if current_switch is not None:
                        units[current_switch] += 1
                else:
                    team_keys[key] += 1
            else:
                header_keys[key] += 1
                if key == "ENCOUNTERNAME":
                    name = value.strip().strip('"')

        encounters.append({
            "path": file.relative_to(path).as_posix(),
            "name": name or file.stem,
            "teams": switches,
            "units": {str(k): v for k, v in units.items()},
        })

    return {
        "header_keys": dict(sorted(header_keys.items())),
        "team_keys": dict(sorted(team_keys.items())),
        "unit_arity": dict(sorted(unit_arity.items())),
        "encounters": encounters,
        "totals": {
            "files": len(encounters),
            "teams": teams_total,
            "units": units_total,
        },
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate encounters_catalog.json")
    parser.add_argument("--root", type=Path, default=DEFAULT_ROOT)
    parser.add_argument("--out", type=Path, default=OUT_PATH)
    args = parser.parse_args()

    if not args.root.exists():
        raise SystemExit(f"encounter folder not found: {args.root} (pass --root)")

    data = scan(args.root)
    catalog = {
        "_meta": {
            "format_version": 1,
            "generated_from": "data/encounters/**/*.enc",
            "totals": data["totals"],
            "unit_arity": data["unit_arity"],
        },
        "header_keys": data["header_keys"],
        "team_keys": data["team_keys"],
        "encounters": data["encounters"],
    }

    args.out.write_text(json.dumps(catalog, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")

    print(f"Wrote {args.out}")
    print(f"  files:        {data['totals']['files']}")
    print(f"  teams:        {data['totals']['teams']}")
    print(f"  units:        {data['totals']['units']}")
    print(f"  header keys:  {len(data['header_keys'])}")
    print(f"  team keys:    {len(data['team_keys'])}")
    print(f"  UNITDB arity: {data['unit_arity']}")


if __name__ == "__main__":
    main()
