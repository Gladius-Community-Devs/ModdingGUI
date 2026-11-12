#!/usr/bin/env python3
"""Prove that tools_v009 packs byte-identically to tools_v008.

    python verify_vs_v008.py <project-folder> [--platform GC]

<project-folder> is any *_BEC holding a filelist.txt whose parent has a *_ISO sibling (a real ModdingGUI project), e.g.
.../demo_hero. Both archives are written to a temp directory and compared by SHA-256; the exit code is 0 only when they
match.

The point of the check: v009 changes the checksum implementation, and the archive stores that checksum per entry, so a
wrong equivalent would produce a subtly broken archive rather than an obvious failure.
"""

import argparse
import hashlib
import os
import shutil
import subprocess
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
PARENT = os.path.dirname(HERE)


def find_folder(project, suffix):
    for name in os.listdir(project):
        if name.lower().endswith(suffix.lower()) and os.path.isdir(os.path.join(project, name)):
            return os.path.join(project, name)
    return None


def discover_v008():
    """Find tools_v008.py: this folder sits next to the GladiusTools checkout in some layouts, elsewhere in others."""
    here = HERE
    candidates = []
    for _ in range(4):
        here = os.path.dirname(here)
        candidates.append(os.path.join(here, "GladiusTools", "Tools", "tools_v008.py"))
    candidates.append(os.path.abspath(os.path.join(PARENT, "..", "GladiusTools", "Tools", "tools_v008.py")))

    for candidate in candidates:
        if os.path.isfile(candidate):
            return candidate
    return candidates[0]


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def pack(entry, bec_folder, filelist, output, platform):
    started = time.perf_counter()
    result = subprocess.run(
        [sys.executable, entry, "bec", "pack", bec_folder, output, filelist, "--platform", platform],
        cwd=os.path.dirname(entry),
        capture_output=True,
        text=True,
    )
    elapsed = time.perf_counter() - started
    if result.returncode != 0:
        print(result.stdout[-2000:])
        print(result.stderr[-2000:], file=sys.stderr)
        raise SystemExit(f"{os.path.basename(entry)} failed with exit {result.returncode}")
    return elapsed


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("project")
    parser.add_argument("--platform", default="GC", choices=["GC", "PS2", "XBOX"])
    parser.add_argument("--v008", help="path to tools_v008.py (default: discovered next to the GladiusTools checkout)")
    args = parser.parse_args()

    project = os.path.abspath(args.project)
    bec_folder = find_folder(project, "_BEC")
    if bec_folder is None:
        raise SystemExit(f"No *_BEC folder in {project}")

    filelist = os.path.join(bec_folder, "filelist.txt")
    if not os.path.isfile(filelist):
        raise SystemExit(f"No filelist.txt in {bec_folder}")

    entries = {
        "v008": args.v008 or discover_v008(),
        "v009": os.path.join(HERE, "tools_v009.py"),
    }
    for label, entry in entries.items():
        if not os.path.isfile(entry):
            raise SystemExit(f"{label}: no entry point at {entry}")

    work = tempfile.mkdtemp(prefix="bec-verify-")
    results = {}
    try:
        for label, entry in entries.items():
            output = os.path.join(work, f"{label}.bec")
            elapsed = pack(entry, bec_folder, filelist, output, args.platform)
            results[label] = (output, os.path.getsize(output), sha256(output), elapsed)
            print(f"{label}: {elapsed:6.2f}s  {os.path.getsize(output):>12,} bytes  {results[label][2]}")

        v008, v009 = results["v008"], results["v009"]
        print()
        if v008[2] == v009[2]:
            speedup = (v008[3] / v009[3]) if v009[3] else 0
            print(f"IDENTICAL ({speedup:.1f}x faster with v009)")
            return 0

        print("DIFFERENT - the checksum change is not equivalent, do not ship this", file=sys.stderr)
        print(f"  v008 {v008[1]:,} bytes {v008[2]}", file=sys.stderr)
        print(f"  v009 {v009[1]:,} bytes {v009[2]}", file=sys.stderr)
        return 1
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
