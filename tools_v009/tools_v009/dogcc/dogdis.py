#!/usr/bin/env python3
"""dogdis.py - disassemble DogCode EDOC bytecode to readable instructions.

Usage: python dogdis.py <file.scb> [--sym table]
Prints each instruction with opcode name and operands resolved against the
RTSS symbol pool when available.
"""
import struct, sys, os


from . import dogopcodes as OC
from . import scb_model as M

def disasm(path):
    version, sec = M.load(path)
    names = []
    if b"RTSS" in sec:
        try:
            names = M.str_pool(sec[b"RTSS"][2])[1]
        except Exception:
            names = []
    lits = []
    if b"RTSN" in sec:
        try:
            lits = M.str_pool(sec[b"RTSN"][2])[1]
        except Exception:
            lits = []
    edoc = sec.get(b"EDOC")
    if edoc is None:
        print("no EDOC"); return
    code = edoc[2]
    print(f"== {os.path.basename(path)}  EDOC {len(code)} bytes")
    for start, op, ops in OC.disassemble(code):
        if op == 0x4A and ops:
            fn = names[ops[0]] if ops[0] < len(names) else str(ops[0])
            print(f"{start:04X}: FUNCLABEL {fn}")
            continue
        # pretty operand hints
        hints = []
        for k, v in enumerate(ops):
            h = None
            if op in (0x2E, 0x33, 0x25, 0x26, 0x27, 0x28, 0x29, 0x35) and v < len(names):
                h = names[v]
            elif op == 0x2C and v < len(lits):
                h = repr(lits[v])
            elif op == 0x2A:
                h = str(v if v < 0x80000000 else v - 0x100000000)
            hints.append(h or hex(v))
        print(f"{start:04X}: {OC.name_of(op):14s} " + " ".join(hints))

if __name__ == "__main__":
    disasm(sys.argv[1])
