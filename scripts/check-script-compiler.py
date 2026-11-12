#!/usr/bin/env python3
"""Gate the bundled script compiler against the DOG compiler, over every vanilla .scp.

    python scripts/check-script-compiler.py [--exe <tools_v009.exe>] [--oracle <DOGCodeCompiler.exe>]

Two modes:

  * ``--oracle`` set: compile every script with both and require byte identity. This is the correctness gate: the DOG
    compiler is the reference semantics, and the app now ships our reimplementation instead of it.
  * no ``--oracle``: compare against the ``.scb`` the game ships in ``data/script/bin``. Those were built by a *different*
    (retail) compiler revision and differ from both compilers, so this mode reports rather than gates.

Defaults are read from the demo project next to the app binary, which is what the validation session used.
"""

import argparse
import os
import subprocess
import sys
import tempfile


def compile_with_exe(exe, source, output):
    result = subprocess.run([exe, "scbc", source, output], capture_output=True, text=True)
    return os.path.exists(output), result


def compile_with_oracle(oracle, source, output):
    result = subprocess.run([oracle, source, output], capture_output=True, text=True)
    return os.path.exists(output), result


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)

    default_bec = os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
        "bin", "Debug", "net8.0-windows", "win-x64", "demo_hero", "demo_hero_BEC")

    parser.add_argument("--bec", default=default_bec, help="a project _BEC folder holding data/script")
    parser.add_argument("--exe", default=None, help="tools_v009.exe (default: tools/v009 beside the app)")
    parser.add_argument("--oracle", default=None, help="DOGCodeCompiler.exe, for the byte-identity gate")
    args = parser.parse_args()

    exe = args.exe
    if exe is None:
        exe = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                           "bin", "Debug", "net8.0-windows", "win-x64", "tools", "v009", "tools_v009.exe")

    script_dir = os.path.join(args.bec, "data", "script")
    bin_dir = os.path.join(script_dir, "bin")
    if not os.path.isdir(script_dir):
        raise SystemExit(f"no data/script under {args.bec}")
    if not os.path.isfile(exe):
        raise SystemExit(f"no compiler exe at {exe}")

    scripts = sorted(f for f in os.listdir(script_dir) if f.lower().endswith(".scp"))
    print(f"compiler: {exe}")
    print(f"scripts : {len(scripts)} in {script_dir}")
    if args.oracle:
        print(f"oracle  : {args.oracle}")

    work = tempfile.mkdtemp(prefix="scbc-gate-")
    ours_path = os.path.join(work, "ours.scb")
    reference_path = os.path.join(work, "reference.scb")

    equal = differ = errors = 0
    failures = []

    try:
        for name in scripts:
            source = os.path.join(script_dir, name)
            for path in (ours_path, reference_path):
                if os.path.exists(path):
                    os.remove(path)

            ok, result = compile_with_exe(exe, source, ours_path)
            if not ok:
                errors += 1
                failures.append((name, "compiler failed: " + (result.stderr or result.stdout).strip()[:80]))
                continue
            ours = open(ours_path, "rb").read()

            if args.oracle:
                ok, _ = compile_with_oracle(args.oracle, source, reference_path)
                if not ok:
                    errors += 1
                    failures.append((name, "oracle failed"))
                    continue
                reference = open(reference_path, "rb").read()
                label = "oracle"
            else:
                shipped = os.path.join(bin_dir, os.path.splitext(name)[0] + ".scb")
                if not os.path.exists(shipped):
                    continue
                reference = open(shipped, "rb").read()
                label = "shipped .scb"

            if ours == reference:
                equal += 1
            else:
                differ += 1
                limit = min(len(ours), len(reference))
                offset = next((i for i in range(limit) if ours[i] != reference[i]), limit)
                failures.append((name, f"{len(ours)} vs {len(reference)} bytes, first diff @0x{offset:x}"))
    finally:
        import shutil
        shutil.rmtree(work, ignore_errors=True)

    print()
    print(f"identical : {equal}")
    print(f"different : {differ}")
    print(f"errors    : {errors}")
    if failures:
        print("\nfirst failures:")
        for name, detail in failures[:12]:
            print(f"  {name:44} {detail}")

    if args.oracle:
        print()
        print("GATE PASSED" if differ == 0 and errors == 0 else "GATE FAILED")
        return 0 if differ == 0 and errors == 0 else 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
