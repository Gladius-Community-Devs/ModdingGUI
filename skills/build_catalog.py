"""
build_catalog.py
================

Scans Gladius `skills.tok` files and produces a JSON catalog of every observed
value for every field, broken down per positional argument slot.

The resulting `skills_catalog.json` is intended to be loaded by the GUI to
populate dropdowns / word-banks for skill editing. The JSON file is
human-editable so the modding community can append newly-discovered values
without requiring a code change.

Usage:
    python build_catalog.py

By default it reads:
    skills.tok
    modded-skills.tok
(both expected to live next to this script)
and writes:
    skills_catalog.json

Re-running the script REGENERATES the "auto" portion of the catalog from the
.tok files. User-curated additions should be placed in the
`skills_catalog_overrides.json` file (see `merge_overrides` below) so that
they survive regeneration.
"""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

# ---------------------------------------------------------------------------
# Field policy
# ---------------------------------------------------------------------------
# Fields we DO NOT enumerate values for. They still appear in the catalog as
# stubs (with a `note` so the GUI can show user-friendly help text) but their
# `slots` array is empty.
EXCLUDED_FIELDS: dict[str, str] = {
    "SKILLUSECLASS": (
        "Class names. Sourced from classdefs.tok rather than enumerated here. "
        "The GUI should populate this dropdown from classdefs.tok at load time."
    ),
    "SKILLDISPLAYNAMEID": (
        "Integer ID into lookuptext_eng.txt. Sourced from that file; "
        "the GUI should resolve it there rather than from this catalog."
    ),
    "SKILLDESCRIPTIONID": (
        "Integer ID into lookuptext_eng.txt. Sourced from that file; "
        "the GUI should resolve it there rather than from this catalog."
    ),
    "SKILLCOSTS": (
        "Turn cost (decimal, 1.0 = 1 turn) and SP cost (x10). "
        "Free-form numeric input; not enumerated."
    ),
    "SKILLAFFCOST": (
        "Affinity cost. Free-form integer input; not enumerated."
    ),
    "SKILLCOMBATMODS": (
        "Combat modifier values: integer accuracy modifier, decimal damage "
        "multiplier. Free-form numeric input; not enumerated."
    ),
    "SKILLANIM": (
        "Animation name for the skill. "
        "Valid values are sourced at runtime from character .pan files via a "
        "multi-step lookup: (1) read each SKILLUSECLASS value on the current "
        "skill; (2) find the matching SKILLUSENAME entry in "
        "data/config/classdefs.tok to get the MESH name; (3) list "
        "data/characters/<mesh>/ for .pan files whose filename contains an "
        "underscore; (4) take everything after the underscore, strip the "
        "extension. Only values that exist across ALL resolved meshes should "
        "be offered. If the skill has no SKILLUSECLASS lines, show nothing "
        "and warn the user that a SKILLUSECLASS is required."
    ),
    "SKILLANIMSPEED": (
        "Animation name (slot 0) and speed multiplier (slot 1). "
        "The animation name follows the same lookup as SKILLANIM: resolve "
        "SKILLUSECLASS -> classdefs.tok MESH -> data/characters/<mesh>/ "
        ".pan files with underscores -> name after underscore. "
        "The speed multiplier is free-form numeric: a value of 0.5 makes the "
        "animation run at half speed (twice as slow); higher values speed it "
        "up. If the skill has no SKILLUSECLASS lines, show nothing and warn "
        "the user that a SKILLUSECLASS is required."
    ),
    "SKILLANIMTIME": (
        "Animation name (slot 0) and duration in seconds (slot 1). "
        "The animation name follows the same lookup as SKILLANIM: resolve "
        "SKILLUSECLASS -> classdefs.tok MESH -> data/characters/<mesh>/ "
        ".pan files with underscores -> name after underscore. "
        "The duration is free-form numeric: the animation is sped up or "
        "slowed down to fit exactly that many seconds (e.g. 0.5 = "
        "half-second). If the skill has no SKILLUSECLASS lines, show nothing "
        "and warn the user that a SKILLUSECLASS is required."
    ),
    "SKILLFXSWING": (
        "Swing FX reference. Sourced from FX/animation files; "
        "free-form string, not enumerated here."
    ),
    "SKILLSOUND": (
        "Sound reference. Sourced from sound/animation files; "
        "free-form string, not enumerated here."
    ),
    "SKILLFREE": (
        "References another skill by its internal name. Used for combo skills, "
        "transform/form abilities (so the resulting form has access to skills), "
        "and some riposte/counter abilities. Must match a SKILLCREATE name "
        "defined elsewhere in the file; not enumerated here."
    ),
    "SKILLPREREQ": (
        "References another skill by its internal name. Marks a prerequisite "
        "skill that must be learned before this one; typically used for skill "
        "progressions (e.g. Affinity Attack 1 unlocks Affinity Attack 2). "
        "Must match a SKILLCREATE name defined elsewhere in the file; "
        "not enumerated here."
    ),
    "SKILLSUBSKILL": (
        "References another skill by its internal name. Defines a skill that "
        "chains or combos from this one (e.g. a follow-up hit, counterattack "
        "trigger, or conditional activate payload). Must match a SKILLCREATE "
        "name defined elsewhere in the file; not enumerated here."
    ),
    "SKILLMULTIHITDATA": (
        "String of direction letters defining which tiles a multi-hit skill "
        "strikes, relative to the caster's facing. Each letter is one hit: "
        "A=front-left, B=in-front, C=front-right, D=right, "
        "E=back-right, F=behind, G=back-left, H=left. "
        "Free-form string input; not enumerated here."
    ),
    "SKILLMETER": (
        "Combat meter type. Valid values are sourced at runtime from "
        "data/config/skillmeter.tok. Parse every line beginning with "
        "METERCREATE: and take the first quoted argument "
        "(e.g. 'METERCREATE: \"pound1\" \"Masher\"' -> 'pound1'). "
        "The GUI should load and cache this file to populate the dropdown."
    ),
}

# Fields tracked with special positional rules.
# SKILLCREATE: slot 0 is the internal name (free-form, do NOT enumerate),
#              slots 1 and 2 ARE enumerated.
SPECIAL_FIELDS: dict[str, dict[int, str]] = {
    "SKILLCREATE": {
        0: "free-form internal skill name; not enumerated",
    },
}

# Fields whose values should be constrained to a numeric range rather than
# enumerated. The GUI should treat these as numeric inputs with these bounds.
NUMERIC_RANGE_FIELDS: dict[str, dict[str, Any]] = {
    "SKILLLEVEL": {
        "type": "int",
        "min": 1,
        "max": 30,
        "note": (
            "Integer 1\u201330 (vanilla also uses 0 for innate/auto-granted skills)."
        ),
    },
    "SKILLJOBPOINTCOST": {
        "type": "int",
        "min": -1,
        "max": None,
        "note": (
            "Integer. -1 = hidden skill, 0 = free skill, "
            ">=1 = costs that many Job Points."
        ),
    },
}

# Numbered "mirror" fields. Anything matching <BASE><digit> is collapsed to
# <BASE> in the catalog. Discovered automatically; this set is just for
# documentation / debugging.
MIRROR_SUFFIX_RE = re.compile(r"^(SKILL[A-Z]+?)(\d+)$")

# ---------------------------------------------------------------------------
# Parsing
# ---------------------------------------------------------------------------
# Tokenizer for the right-hand side of a `FIELD: ...` line.
# Handles: "quoted strings", integers, floats (including negatives), commas.
TOKEN_RE = re.compile(
    r'''
    "([^"]*)"                     # group 1: quoted string
    |                             # OR
    (-?\d+\.\d+)                  # group 2: float
    |                             # OR
    (-?\d+)                       # group 3: int
    |                             # OR
    ([A-Za-z_][A-Za-z0-9_\-]*)    # group 4: bareword (rare, but observed)
    ''',
    re.VERBOSE,
)

LINE_RE = re.compile(r'^\s*([A-Z][A-Z0-9]+)\s*:\s*(.*?)\s*(?://.*)?$')


def parse_value(raw: str) -> tuple[Any, str]:
    """Return (python_value, type_label) for a single token-source string."""
    m = TOKEN_RE.fullmatch(raw.strip())
    if not m:
        return raw, "string"
    if m.group(1) is not None:
        return m.group(1), "string"
    if m.group(2) is not None:
        return float(m.group(2)), "float"
    if m.group(3) is not None:
        return int(m.group(3)), "int"
    return m.group(4), "string"


def tokenize_args(rhs: str) -> list[tuple[Any, str]]:
    """Split `rhs` (the part after `FIELD:`) into typed tokens, in order."""
    out: list[tuple[Any, str]] = []
    for m in TOKEN_RE.finditer(rhs):
        if m.group(1) is not None:
            out.append((m.group(1), "string"))
        elif m.group(2) is not None:
            out.append((float(m.group(2)), "float"))
        elif m.group(3) is not None:
            out.append((int(m.group(3)), "int"))
        else:
            out.append((m.group(4), "string"))
    return out


def normalize_field_name(name: str) -> str:
    """Collapse mirror/numbered fields (SKILLSTATUS2 -> SKILLSTATUS)."""
    m = MIRROR_SUFFIX_RE.fullmatch(name)
    if m:
        base, _digit = m.groups()
        # Don't collapse fields that legitimately end in a digit (none known)
        return base
    return name


# ---------------------------------------------------------------------------
# Field reference (skills_field_reference.txt)
# ---------------------------------------------------------------------------
# skills_field_reference.txt is hand-maintained by modders and documents each
# field with a small keyword grammar:
#
#   FIELD:    <FIELDNAME>
#   FORMAT:   free-text shape description
#   MULTI:    yes|no   (may the field repeat inside one record?)
#   VALUES:   a | b | c (documented enum values; placeholders like
#                        <Any>, class: <name>, (same list as ...) are dropped)
#   LINK:     <OTHER_FIELD>   (must appear together)
#   REQUIRES: <FIELD> present | <FIELD> = <value> | <FIELD>.part = <value>
#   NOTES:    free text
#   ---       ends the block
#
# Those keywords are the structural validation rules, so they are merged into
# the generated catalog instead of being re-parsed in C#. Enum values documented
# here are unioned into the observed values (more permissive, never removes).
REFERENCE_FILENAME = "skills_field_reference.txt"

_REF_KEY_RE = re.compile(
    r"^\s*(FIELD|FORMAT|MULTI|VALUES|LINK|REQUIRES|NOTES)\s*:\s*(.*)$")

_REQUIRES_EQUALS_RE = re.compile(
    r"^(?P<field>[A-Z][A-Z0-9]*)(?:\.(?P<part>[A-Za-z]+))?\s*=\s*(?P<value>.+)$")
_REQUIRES_PRESENT_RE = re.compile(
    r"^(?P<field>[A-Z][A-Z0-9]*)\s+present$", re.IGNORECASE)
# A bare field name means "this field must also be present".
_REQUIRES_BARE_RE = re.compile(r"^(?P<field>[A-Z][A-Z0-9]*)$")


def _is_placeholder_value(value: str) -> bool:
    """True for documentation placeholders that are not real enum values."""
    v = value.strip()
    if not v:
        return True
    if "<" in v or ">" in v:          # <Any>, class: <name>, skill: <skill name>
        return True
    if v.startswith("("):             # (same list as SKILLSTATUSSITUATION...)
        return True
    return False


def _split_values(raw: str) -> list[str]:
    return [p.strip() for p in raw.split("|")
            if p.strip() and not _is_placeholder_value(p)]


def _parse_requires(raw: str) -> dict[str, Any]:
    """Parse one REQUIRES: line into a structured rule, or keep it as prose."""
    text = raw.strip()
    if not text:
        return {}
    m = _REQUIRES_EQUALS_RE.match(text)
    if m:
        req: dict[str, Any] = {
            "field": m.group("field"),
            "equals": m.group("value").strip().strip('"'),
        }
        if m.group("part"):
            req["part"] = m.group("part")
        return req
    m = _REQUIRES_PRESENT_RE.match(text)
    if m:
        return {"field": m.group("field"), "present": True}
    m = _REQUIRES_BARE_RE.match(text)
    if m:
        return {"field": m.group("field"), "present": True}
    # Prose ("This is a required line prior to SKILLMOVETOATTACKMOD: ..."):
    # preserved for humans, never enforced.
    return {"raw": text}


def parse_field_reference(path: Path) -> dict[str, dict[str, Any]]:
    """Parse the hand-written field reference into per-field rule specs.

    Tolerant by design: continuation lines are folded into the preceding
    keyword, comments are skipped, and unparseable REQUIRES prose is kept
    verbatim rather than rejected. Blocks whose field name normalizes to the
    same base (SKILLSTATUS / SKILLSTATUS2) are merged.
    """
    fields: dict[str, dict[str, Any]] = {}
    if not path.exists():
        print(f"[warn] missing field reference: {path}")
        return fields

    state: dict[str, Any] | None = None
    keyword: str | None = None

    def finish(block: dict[str, Any] | None) -> None:
        if block is None or not block["name"]:
            return
        target = fields.setdefault(normalize_field_name(block["name"]), {
            "names": [], "format": [], "multi": None,
            "values": [], "links": [], "requires": [], "notes": [],
        })
        target["names"].append(block["name"])
        target["format"].extend(block["format"])
        if block["multi"] is not None:
            target["multi"] = block["multi"] or bool(target["multi"])
        target["values"].extend(block["values"])
        target["links"].extend(block["links"])
        target["requires"].extend(block["requires"])
        target["notes"].extend(block["notes"])

    for raw_line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        line = raw_line.lstrip("\ufeff")
        if line.strip() == "---":
            finish(state)
            state, keyword = None, None
            continue

        m = _REF_KEY_RE.match(line)
        if m:
            kw, value = m.group(1), m.group(2).strip()
            if kw == "FIELD":
                finish(state)
                state = {"name": value.split()[0].rstrip(":") if value else "",
                         "format": [], "multi": None, "values": [],
                         "links": [], "requires": [], "notes": []}
                keyword = None
                continue
            if state is None:
                continue
            keyword = kw
            if kw == "MULTI":
                state["multi"] = value.lower().startswith("y")
            elif kw == "VALUES":
                state["values"].extend(_split_values(value))
            elif kw == "LINK":
                if value:
                    state["links"].append(value.strip())
            elif kw == "REQUIRES":
                req = _parse_requires(value)
                if req:
                    state["requires"].append(req)
            elif kw in ("FORMAT", "NOTES") and value:
                state[kw.lower()].append(value)
            continue

        # Continuation line for the current keyword (VALUES/FORMAT/NOTES wrap).
        if state is None or keyword is None:
            continue
        text = line.strip()
        if not text or text.startswith("#"):
            continue
        if keyword in ("FORMAT", "NOTES"):
            state[keyword.lower()].append(text)
        elif keyword == "VALUES":
            state["values"].extend(_split_values(text))
        elif keyword == "REQUIRES":
            req = _parse_requires(text)
            if req:
                state["requires"].append(req)

    finish(state)
    return fields


# ---------------------------------------------------------------------------
# Catalog assembly
# ---------------------------------------------------------------------------
class FieldData:
    __slots__ = ("slots", "had_numeric", "had_string", "mirrors")

    def __init__(self) -> None:
        # slots[i] = {"types": set[str], "values": set[Any]}
        self.slots: list[dict[str, set]] = []
        self.mirrors: set[str] = set()

    def record(self, original_name: str, args: list[tuple[Any, str]]) -> None:
        self.mirrors.add(original_name)
        while len(self.slots) < len(args):
            self.slots.append({"types": set(), "values": set()})
        for i, (val, kind) in enumerate(args):
            self.slots[i]["types"].add(kind)
            # Only enumerate string values; numerics are tracked by type only.
            if kind == "string":
                self.slots[i]["values"].add(val)


def parse_file(path: Path, store: dict[str, FieldData]) -> None:
    with path.open("r", encoding="utf-8", errors="replace") as fh:
        for line in fh:
            line = line.rstrip("\n")
            if not line or line.lstrip().startswith("//"):
                continue
            m = LINE_RE.match(line)
            if not m:
                continue
            raw_name, rhs = m.group(1), m.group(2)
            if not raw_name.startswith("SKILL") and raw_name != "NUMENTRIES":
                continue
            if raw_name == "NUMENTRIES":
                continue
            args = tokenize_args(rhs)
            if not args:
                continue
            base = normalize_field_name(raw_name)
            store.setdefault(base, FieldData()).record(raw_name, args)


def build_catalog(tok_files: list[Path]) -> dict[str, Any]:
    store: dict[str, FieldData] = {}
    for p in tok_files:
        if p.exists():
            parse_file(p, store)
        else:
            print(f"[warn] missing source file: {p}")

    catalog: dict[str, Any] = {
        "_meta": {
            "format_version": 2,
            "generated_from": [p.name for p in tok_files],
            "notes": [
                "This file is auto-generated by build_catalog.py but is "
                "INTENTIONALLY human-editable: add new values to any "
                "field's slot \"values\" array as the community discovers "
                "them. Re-running the generator merges in any newly-seen "
                "values without removing your additions, AS LONG AS your "
                "additions live in skills_catalog_overrides.json.",
                "Mirror/numbered fields (e.g. SKILLSTATUS2, "
                "SKILLSTATUSCONDITION3) are collapsed onto their base "
                "field name; the original observed names are listed in "
                "the field's \"mirrors\" array for reference.",
                "Numeric slots only record the type(s) observed (int/"
                "float). String slots record the full set of observed "
                "values.",
                "Format version 2 adds the structural rules taken from "
                "skills_field_reference.txt: \"format\" (shape prose), "
                "\"multi\" (may repeat inside one record), \"links\" "
                "(fields that must appear together), \"requires\" "
                "(structured prerequisites; unparseable prose is kept "
                "under \"raw\" and is never enforced) and \"notes\". "
                "Enum values documented in the reference file are unioned "
                "into the observed values.",
            ],
            "external_sources": {
                "SKILLUSECLASS": "classdefs.tok",
                "SKILLDISPLAYNAMEID": "lookuptext_eng.txt",
                "SKILLDESCRIPTIONID": "lookuptext_eng.txt",
                "SKILLANIM*": "animation files (anim/*.tok and FX defs)",
                "SKILLFXSWING": "FX / animation files",
                "SKILLSOUND": "sound / animation files",
            },
        },
        "fields": {},
    }

    # Always emit the documented field set even if a field had zero
    # occurrences, so the GUI has a complete schema to render against.
    all_fields = sorted(set(store.keys())
                        | set(EXCLUDED_FIELDS.keys())
                        | set(NUMERIC_RANGE_FIELDS.keys())
                        | set(SPECIAL_FIELDS.keys()))

    for fname in all_fields:
        entry: dict[str, Any] = {}

        if fname in EXCLUDED_FIELDS:
            entry["enumerate"] = False
            entry["note"] = EXCLUDED_FIELDS[fname]
            entry["slots"] = []
            data = store.get(fname)
            if data:
                entry["mirrors"] = sorted(data.mirrors)
                # Still record types/arity so the GUI knows the shape.
                entry["slots"] = [
                    {
                        "types": sorted(s["types"]),
                        "values": [],  # intentionally empty
                    }
                    for s in data.slots
                ]
            catalog["fields"][fname] = entry
            continue

        if fname in NUMERIC_RANGE_FIELDS:
            r = NUMERIC_RANGE_FIELDS[fname]
            entry["enumerate"] = False
            entry["note"] = r["note"]
            entry["numeric"] = {
                "type": r["type"],
                "min": r["min"],
                "max": r["max"],
            }
            entry["slots"] = []
            data = store.get(fname)
            if data:
                entry["mirrors"] = sorted(data.mirrors)
            catalog["fields"][fname] = entry
            continue

        data = store.get(fname)
        entry["enumerate"] = True
        entry["mirrors"] = sorted(data.mirrors) if data else [fname]

        slots_out: list[dict[str, Any]] = []
        if data:
            for i, s in enumerate(data.slots):
                slot_entry: dict[str, Any] = {
                    "types": sorted(s["types"]),
                    "values": sorted(v for v in s["values"]
                                     if isinstance(v, str)),
                }
                # Apply per-field special-case overrides (e.g. SKILLCREATE
                # slot 0 is free-form).
                if fname in SPECIAL_FIELDS and i in SPECIAL_FIELDS[fname]:
                    slot_entry["enumerate"] = False
                    slot_entry["note"] = SPECIAL_FIELDS[fname][i]
                    slot_entry["values"] = []
                slots_out.append(slot_entry)
        entry["slots"] = slots_out
        catalog["fields"][fname] = entry

    return catalog


# ---------------------------------------------------------------------------
# Overrides
# ---------------------------------------------------------------------------
def merge_overrides(catalog: dict[str, Any], overrides_path: Path) -> None:
    """Merge user-curated additions from a sibling overrides file.

    Overrides file shape (all keys optional):
        {
          "fields": {
            "SKILLSTATUS": {
              "slots": [
                {"values": ["my new status name"]}
              ]
            }
          }
        }
    Only `values` lists are merged (set-union, sorted). Nothing is removed.
    """
    if not overrides_path.exists():
        return
    try:
        with overrides_path.open("r", encoding="utf-8") as fh:
            ov = json.load(fh)
    except (OSError, json.JSONDecodeError) as e:
        print(f"[warn] could not read overrides {overrides_path}: {e}")
        return

    fields_ov = ov.get("fields", {}) if isinstance(ov, dict) else {}
    for fname, fov in fields_ov.items():
        target = catalog["fields"].setdefault(fname, {
            "enumerate": True,
            "mirrors": [fname],
            "slots": [],
        })
        slots_ov = fov.get("slots", [])
        for i, sov in enumerate(slots_ov):
            while len(target.get("slots", [])) <= i:
                target.setdefault("slots", []).append(
                    {"types": ["string"], "values": []}
                )
            extra = sov.get("values", [])
            merged = sorted(set(target["slots"][i].get("values", [])) | set(extra))
            target["slots"][i]["values"] = merged


# ---------------------------------------------------------------------------
# Rule support — does the corpus actually obey the documented rules?
# ---------------------------------------------------------------------------
# The catalog is generated from observation; skills_field_reference.txt is
# hand-written intent. Where they disagree the corpus wins: a documented rule
# that vanilla data violates is marked advisory ("strict": false) rather than
# deleted, so the knowledge survives and validation does not cry wolf on valid
# vanilla records. Fix the reference file, regenerate, and the rule becomes
# strict again automatically.
RECORD_START_RE = re.compile(r"^SKILLCREATE\s*:")


def scan_records(paths: list[Path]) -> list[dict[str, list[str]]]:
    """Per-record map of normalized field name -> list of raw value strings."""
    records: list[dict[str, list[str]]] = []
    current: dict[str, list[str]] | None = None

    for path in paths:
        if not path.exists():
            continue
        with path.open("r", encoding="utf-8", errors="replace") as fh:
            for line in fh:
                line = line.rstrip("\r\n")
                if RECORD_START_RE.match(line):
                    current = {}
                    records.append(current)
                    continue
                if current is None or not line.strip() or line.lstrip().startswith("//"):
                    continue
                m = LINE_RE.match(line)
                if not m:
                    continue
                name, rhs = m.group(1), m.group(2)
                if not name.startswith("SKILL") or name == "NUMENTRIES":
                    continue
                current.setdefault(normalize_field_name(name), []).append(rhs)
    return records


def _first_token_text(raw: str) -> str:
    args = tokenize_args(raw)
    return str(args[0][0]) if args else ""


def mark_unsupported_rules(
    catalog: dict[str, Any],
    records: list[dict[str, list[str]]],
) -> dict[str, int]:
    """Mark documented rules the observed corpus contradicts as advisory.

    Repeat legality is NOT decided here any more: the engine's own behaviour
    (``dol_skill_fields.json``) is authoritative, because the corpus can repeat a
    field the engine merely overwrites (last-wins) as easily as one it
    accumulates. See :func:`annotate_engine`.
    """
    counts = {"links_advisory": 0, "requires_advisory": 0}

    for fname, spec in catalog["fields"].items():
        links = spec.get("links")
        if links:
            violations = 0
            for rec in records:
                if fname not in rec:
                    continue
                for link in links:
                    if normalize_field_name(link) not in rec:
                        violations += 1
            if violations:
                spec["links_strict"] = False
                spec["link_violations"] = violations
                counts["links_advisory"] += 1

        for req in spec.get("requires") or []:
            target = req.get("field")
            if not target:
                continue
            target = normalize_field_name(target)
            violations = 0
            for rec in records:
                if fname not in rec:
                    continue
                values = rec.get(target)
                if not values:
                    violations += 1
                elif req.get("equals") is not None:
                    wanted = str(req["equals"]).lower()
                    if not any(_first_token_text(v).lower() == wanted for v in values):
                        violations += 1
            if violations:
                req["strict"] = False
                req["violations"] = violations
                counts["requires_advisory"] += 1

    return counts


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
# ---------------------------------------------------------------------------
# Engine truth (extracted from the GameCube DOL)
# ---------------------------------------------------------------------------
# dol_skill_fields.json is produced from the boot DOL: every field name the
# engine's tokenizer recognises, its internal id, argument shape and repeat
# behaviour, read out of SkillLoadTokenFile (runtime record) and FUN_80026ec0
# (battle/effect record). It is the authority on legality — the corpus cannot
# be, because a modded file happily repeats a field the engine merely overwrites.
DOL_FIELDS_FILENAME = "dol_skill_fields.json"

# "bit-set (OR)", "class set" and "conditional slot" all accumulate; scalar and
# "status slot N" fields are last-wins (the status pairs simply give two slots).
_ACCUMULATING_SLOT_MARKERS = ("OR", "set", "conditional")


def load_engine_fields(path: Path) -> dict[str, dict[str, Any]]:
    """Load the DOL-derived engine field table (name -> row)."""
    if not path.exists():
        print(f"[warn] missing engine field table: {path}")
        return {}
    try:
        with path.open("r", encoding="utf-8") as fh:
            rows = json.load(fh)
    except (OSError, json.JSONDecodeError) as exc:
        print(f"[warn] could not read {path}: {exc}")
        return {}
    return {row["name"]: row for row in rows if row.get("name")}


def annotate_engine(
    catalog: dict[str, Any],
    engine: dict[str, dict[str, Any]],
) -> dict[str, int]:
    """Attach engine id, cardinality and accumulation semantics to each field."""
    counts = {"engine_known": 0, "engine_only_added": 0, "accumulating": 0}
    covered: set[str] = set()

    for fname, spec in catalog["fields"].items():
        # Dedupe: the base name also appears in `mirrors`.
        names = list(dict.fromkeys([fname] + list(spec.get("mirrors") or [])))
        hits = [engine[n] for n in names if n in engine]
        covered.update(n for n in names if n in engine)
        spec["engine"] = bool(hits)
        if not hits:
            continue

        counts["engine_known"] += 1
        ids = sorted({h["id"] for h in hits})
        spec["engine_id"] = ids[0] if len(ids) == 1 else ids
        spec["engine_parser"] = hits[0].get("parser")
        spec["engine_arity"] = hits[0].get("arity")
        spec["engine_slot"] = hits[0].get("slot")

        maxima = [h.get("max_occurrences") for h in hits]
        if any(m is None for m in maxima):
            spec["max_occurrences"] = None          # unbounded / accumulating
        elif len({h["id"] for h in hits}) > 1:
            # Distinct engine ids sharing one catalog name (SKILLFREE1..4,
            # SKILLEXCLUSION1..2): each field is single-use, so the name-level
            # allowance is the number of slots.
            spec["max_occurrences"] = sum(m for m in maxima if m) or None
        else:
            spec["max_occurrences"] = max(m for m in maxima if m)
        accumulates = any(
            any(marker in (h.get("slot") or "") for marker in _ACCUMULATING_SLOT_MARKERS)
            for h in hits
        )
        spec["accumulates"] = accumulates
        if accumulates:
            counts["accumulating"] += 1

    # Engine fields that never appear in the corpus are still legal — document
    # them so the editor and the validator know they exist.
    for name in sorted(engine):
        if name in covered:
            continue
        row = engine[name]
        catalog["fields"][name] = {
            "enumerate": False,
            "note": "Engine-legal field with no vanilla occurrences.",
            "mirrors": [name],
            "slots": [],
            "engine": True,
            "engine_id": row["id"],
            "engine_parser": row.get("parser"),
            "engine_arity": row.get("arity"),
            "engine_slot": row.get("slot"),
            "max_occurrences": row.get("max_occurrences"),
            "accumulates": any(
                marker in (row.get("slot") or "")
                for marker in _ACCUMULATING_SLOT_MARKERS
            ),
            "engine_only": True,
        }
        counts["engine_only_added"] += 1

    return counts


def merge_reference(
    catalog: dict[str, Any],
    reference: dict[str, dict[str, Any]],
    source_name: str,
) -> dict[str, int]:
    """Fold the field reference's structural rules into catalog entries.

    Documents rules only: nothing observed in the corpus is removed. Fields
    that appear only in the reference are added with `reference_only: true`
    so consumers can tell guidance apart from observed reality.
    """
    counts = {"fields": 0, "with_values": 0, "with_links": 0,
              "with_requires": 0, "multi_yes": 0, "reference_only": 0}
    seen_requires: set[str] = set()

    for fname in sorted(reference):
        spec = reference[fname]
        entry = catalog["fields"].get(fname)
        if entry is None:
            entry = {
                "enumerate": False,
                "mirrors": sorted(set(spec["names"])) or [fname],
                "slots": [],
                "reference_only": True,
            }
            catalog["fields"][fname] = entry
            counts["reference_only"] += 1

        if spec["format"]:
            entry["format"] = " ".join(spec["format"]).strip()

        if spec["multi"] is not None:
            entry["multi"] = bool(spec["multi"] or entry.get("multi"))
            if entry["multi"]:
                counts["multi_yes"] += 1

        if spec["notes"]:
            # Keep a single human-readable `note` key (the GUI binds `Note`).
            extra = " ".join(spec["notes"]).strip()
            existing = entry.get("note")
            entry["note"] = f"{existing} {extra}".strip() if existing else extra

        links = sorted({l for l in spec["links"] if l and l != fname})
        if links:
            entry["links"] = links
            counts["with_links"] += 1

        requires: list[dict[str, Any]] = []
        seen_requires.clear()
        for req in spec["requires"]:
            key = json.dumps(req, sort_keys=True)
            if key in seen_requires:
                continue
            seen_requires.add(key)
            requires.append(req)
        if requires:
            entry["requires"] = requires
            counts["with_requires"] += 1

        values = sorted(set(spec["values"]))
        if values:
            counts["with_values"] += 1
            if entry.get("enumerate"):
                slots = entry.setdefault("slots", [])
                if not slots:
                    slots.append({"types": ["string"], "values": []})
                slots[0]["values"] = sorted(
                    set(slots[0].get("values", [])) | set(values))

        counts["fields"] += 1

    catalog["_meta"]["reference"] = {
        "file": source_name,
        "fields_documented": counts["fields"],
        "fields_with_values": counts["with_values"],
        "fields_with_links": counts["with_links"],
        "fields_with_requires": counts["with_requires"],
        "multi_yes": counts["multi_yes"],
        "reference_only_fields": counts["reference_only"],
    }
    return counts


def main() -> None:
    here = Path(__file__).resolve().parent
    tok_files = [here / "skills.tok", here / "modded-skills.tok"]
    catalog = build_catalog(tok_files)

    reference = parse_field_reference(here / REFERENCE_FILENAME)
    ref_counts = merge_reference(catalog, reference, REFERENCE_FILENAME)

    engine = load_engine_fields(here / DOL_FIELDS_FILENAME)
    engine_counts = annotate_engine(catalog, engine)

    records = scan_records(tok_files)
    rule_counts = mark_unsupported_rules(catalog, records)
    catalog["_meta"]["rule_support"] = {
        "records_scanned": len(records),
        "links_marked_advisory": rule_counts["links_advisory"],
        "requires_marked_advisory": rule_counts["requires_advisory"],
    }
    catalog["_meta"]["engine"] = {
        "source": "GameCube boot DOL (SkillLoadTokenFile / FUN_80026ec0 field tables)",
        "fields_known": engine_counts["engine_known"],
        "fields_added_from_engine": engine_counts["engine_only_added"],
        "fields_accumulating": engine_counts["accumulating"],
    }

    overrides_path = here / "skills_catalog_overrides.json"
    merge_overrides(catalog, overrides_path)

    out_path = here / "skills_catalog.json"
    with out_path.open("w", encoding="utf-8") as fh:
        json.dump(catalog, fh, indent=2, ensure_ascii=False, sort_keys=False)

    # Quick stats
    n_fields = len(catalog["fields"])
    n_enum_fields = sum(1 for f in catalog["fields"].values()
                        if f.get("enumerate"))
    n_values = sum(len(s.get("values", []))
                   for f in catalog["fields"].values()
                   for s in f.get("slots", []))
    print(f"Wrote {out_path}")
    print(f"  fields:           {n_fields}")
    print(f"  enumerated:       {n_enum_fields}")
    print(f"  string values:    {n_values}")
    print(f"  reference fields: {ref_counts['fields']} "
          f"(values {ref_counts['with_values']}, "
          f"links {ref_counts['with_links']}, "
          f"requires {ref_counts['with_requires']}, "
          f"multi {ref_counts['multi_yes']}, "
          f"reference-only {ref_counts['reference_only']})")
    print(f"  rule support:     records {len(records)}, "
          f"links advisory {rule_counts['links_advisory']}, "
          f"requires advisory {rule_counts['requires_advisory']}")
    print(f"  engine truth:     known {engine_counts['engine_known']}, "
          f"added {engine_counts['engine_only_added']}, "
          f"accumulating {engine_counts['accumulating']}")


if __name__ == "__main__":
    main()
