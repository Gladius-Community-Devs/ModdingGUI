"""
build_catalog.py — schools and leagues catalogs
===============================================

One generator for the two remaining structured families, because they share a shape
(header keys plus named blocks):

  data/school/*.tok          header (NAME/HERO/GOLD) + CREATEUNIT blocks
  data/towns/leagues/*.tok   header (OFFICENAME/RECRUIT/SCHOOL) + LEAGUE blocks,
                             each holding ENCOUNTER blocks

The catalogs record which keys the corpus uses, so the validators can say "the game ignores
this key" instead of guessing. They also record the block counts for quick sanity checks.

    py -3 tables/build_catalog.py
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
REPO = HERE.parent
DATA = REPO.parent / "clean-bec-unpacked" / "data"

KEY_RE = re.compile(r"^\s*([A-Z][A-Z0-9_]*)(?:\s*:\s*|\s+)(.*)$")
UNIT_RE = re.compile(r'^\s*CREATEUNIT\s*:\s*"([^"]*)"\s*,\s*"([^"]*)"', re.IGNORECASE)


def scan_schools(root: Path) -> dict[str, Any]:
    header: Counter = Counter()
    unit: Counter = Counter()
    files = 0
    units = 0

    for file in sorted(root.glob("*.tok")):
        files += 1
        in_unit = False
        for raw in file.read_text(encoding="latin-1").replace("\r\n", "\n").split("\n"):
            if not raw.strip() or raw.strip().startswith("//"):
                continue
            if UNIT_RE.match(raw):
                in_unit = True
                units += 1
                continue
            match = KEY_RE.match(raw)
            if not match:
                continue
            key = match.group(1).upper()
            (unit if in_unit else header)[key] += 1

    return {"files": files, "units": units,
            "header_keys": dict(sorted(header.items())),
            "unit_keys": dict(sorted(unit.items()))}


LEAGUE_RE = re.compile(r'^\s*LEAGUE\s*:?\s+"([^"]*)"', re.IGNORECASE)
ENCOUNTER_RE = re.compile(r'^\s*ENCOUNTER\s*:?\s+"([^"]*)"', re.IGNORECASE)


def scan_leagues(root: Path) -> dict[str, Any]:
    header: Counter = Counter()
    league_keys: Counter = Counter()
    encounter_keys: Counter = Counter()
    files = 0
    leagues = 0
    encounters = 0
    prizes: Counter = Counter()

    for file in sorted(root.glob("*.tok")):
        files += 1
        section = "header"
        for raw in file.read_text(encoding="latin-1").replace("\r\n", "\n").split("\n"):
            if not raw.strip() or raw.strip().startswith("//"):
                continue
            if LEAGUE_RE.match(raw):
                section = "league"
                leagues += 1
                continue
            if ENCOUNTER_RE.match(raw):
                section = "encounter"
                encounters += 1
                continue
            match = KEY_RE.match(raw)
            if not match:
                continue
            key = match.group(1).upper()
            if section == "header":
                header[key] += 1
            elif section == "league":
                league_keys[key] += 1
            else:
                encounter_keys[key] += 1
                if key == "TEAMS":
                    prizes[match.group(2).strip()] += 1

    return {"files": files, "leagues": leagues, "encounters": encounters,
            "header_keys": dict(sorted(header.items())),
            "league_keys": dict(sorted(league_keys.items())),
            "encounter_keys": dict(sorted(encounter_keys.items())),
            "teams_shapes": dict(prizes.most_common(8))}


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate school and league catalogs")
    parser.add_argument("--data", type=Path, default=DATA)
    args = parser.parse_args()

    schools = scan_schools(args.data / "school")
    leagues = scan_leagues(args.data / "towns" / "leagues")

    (HERE / "schools_catalog.json").write_text(json.dumps({
        "_meta": {"format_version": 1, "generated_from": "data/school/*.tok",
                  "files": schools["files"], "units": schools["units"]},
        "header_keys": schools["header_keys"],
        "unit_keys": schools["unit_keys"],
    }, indent=1) + "\n", encoding="utf-8")

    (HERE / "leagues_catalog.json").write_text(json.dumps({
        "_meta": {"format_version": 1, "generated_from": "data/towns/leagues/*.tok",
                  "files": leagues["files"], "leagues": leagues["leagues"],
                  "encounters": leagues["encounters"]},
        "header_keys": leagues["header_keys"],
        "league_keys": leagues["league_keys"],
        "encounter_keys": leagues["encounter_keys"],
        "teams_shapes": leagues["teams_shapes"],
    }, indent=1) + "\n", encoding="utf-8")

    print(f"schools: {schools['files']} files, {schools['units']} units, "
          f"{len(schools['header_keys'])} header keys, {len(schools['unit_keys'])} unit keys")
    print(f"leagues: {leagues['files']} files, {leagues['leagues']} leagues, "
          f"{leagues['encounters']} encounters, "
          f"{len(leagues['header_keys'])}/{len(leagues['league_keys'])}/{len(leagues['encounter_keys'])} "
          f"keys (header/league/encounter)")


if __name__ == "__main__":
    main()
