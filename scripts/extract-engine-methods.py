#!/usr/bin/env python3
"""Extract the engine's script-method tables from bootfile.dol.

Every script method is added by ``FUN_800080a0(class_object, name_pointer)``. The decompiler shows the pattern plainly:

    iVar = Heap(0x18)                       // a method record
    *(char **)(iVar + 8)  = s_MethodName    // name
    *(code **)(iVar + 0xc) = FUN_handler    // handler
    FUN_800080a0(&DAT_class, s_MethodName)  // bind it to a class

and the machine code loads the two arguments in a small number of shapes:

    lis r3, hi ; addi r3, r3, lo            the class object, an absolute static (e.g. 0x803D7640)
    addi r4, r31, 0x34                      the name, an offset into a string block based in r31

So: find every call to the registrar, resolve r3 and r4 from the instructions in front of it (with the function preamble
when r31 carries a base), then group the calls by class object. That yields the method list of every engine class -
including every method no vanilla script ever calls. Interface labels are inferred by overlapping the method names with the
corpus-observed names, so they are best matches, not certainties.

No Ghidra needed: only lis / addi / ori / li / mr / bl are decoded, straight out of the DOL.
"""

import argparse
import json
import os
import struct
import sys
from collections import defaultdict

REGISTRAR = 0x800080A0
BLR = 0x4E800020


def load_dol(path):
    """Return {virtual base: bytes} for the DOL's text and data sections."""
    data = open(path, 'rb').read()

    # The DOL header holds a file offset, a load address and a size *per section*:
    #   0x00 + i*4  text offset     0x48 + i*4  text address   0x90 + i*4  text size
    #   0x1C + i*4  data offset     0x64 + i*4  data address   0xAC + i*4  data size
    # (advancing one cursor by size instead - the obvious-looking shortcut - maps the wrong bytes and makes every string
    # read resolve to code, which is how this was caught).
    text_offs = struct.unpack_from('>7I', data, 0x00)
    data_offs = struct.unpack_from('>11I', data, 0x1C)
    text_addrs = struct.unpack_from('>7I', data, 0x48)
    data_addrs = struct.unpack_from('>11I', data, 0x64)
    text_sizes = struct.unpack_from('>7I', data, 0x90)
    data_sizes = struct.unpack_from('>11I', data, 0xAC)

    sections = {}
    for i in range(7):
        if text_sizes[i]:
            sections[text_addrs[i]] = data[text_offs[i]:text_offs[i] + text_sizes[i]]
    for i in range(11):
        if data_sizes[i]:
            sections[data_addrs[i]] = data[data_offs[i]:data_offs[i] + data_sizes[i]]
    return sections


def section_for(sections, address):
    candidates = [b for b in sections if b <= address]
    if not candidates:
        return None, b""
    base = max(candidates)
    return base, sections[base]


def read_cstring(sections, address):
    base, blob = section_for(sections, address)
    if base is None:
        return None
    offset = address - base
    if offset < 0 or offset >= len(blob):
        return None
    end = blob.find(b'\x00', offset)
    if end < 0 or end - offset > 120:
        return None
    try:
        return blob[offset:end].decode('ascii')
    except UnicodeDecodeError:
        return None


def signed16(value):
    return value - 0x10000 if value & 0x8000 else value


def words_between(sections, start, end):
    """Instructions in [start, end), as (address, word)."""
    out = []
    for address in range(start, end - 3, 4):
        try:
            base, blob = section_for(sections, address)
        except ValueError:
            break
        offset = address - base
        if offset + 4 > len(blob):
            break
        out.append((address, struct.unpack_from('>I', blob, offset)[0]))
    return out


def decode_branch(word, pc):
    if (word >> 26) != 18:
        return None
    displacement = word & 0x03FFFFFC
    if displacement & 0x02000000:
        displacement -= 0x04000000
    return (displacement & 0xFFFFFFFF) if (word & 0x2) else (pc + displacement) & 0xFFFFFFFF


def writes_to(word, register):
    """True when the instruction's destination is ``register`` (only the forms a compiler uses for addresses)."""
    op = word >> 26
    if op in (14, 15, 24, 25, 32, 34, 40, 42, 31):
        return ((word >> 21) & 0x1F) == register
    return False


def resolve(sections, address, register, depth=0, window=0x100):
    """Address held by ``register`` at ``address``, or None.

    Handles lis/addi/ori/li directly and follows ``mr`` chains, plus the ``addi rX, rBase, lo`` shape by resolving the
    base (which is how the method names are reached: they live in a string block pointed at by r31).
    """
    if depth > 3:
        return None
    window = words_between(sections, max(0x80000000, address - window), address)

    hi = None
    low = None
    current = register
    for index in range(len(window) - 1, -1, -1):
        pc, word = window[index]
        op = word >> 26
        rt = (word >> 21) & 0x1F
        ra = (word >> 16) & 0x1F
        rb = (word >> 11) & 0x1F

        if word == BLR or (op == 37 and rt == 1):            # function boundary: stop here
            break
        if op == 15 and rt == current:                      # lis
            hi = (word & 0xFFFF) << 16
        elif op == 14 and rt == current and ra == current:  # addi rX, rX, lo
            low = signed16(word & 0xFFFF)
        elif op == 24 and rt == current and ra == current:  # ori rX, rX, lo
            low = word & 0xFFFF
        elif op == 14 and rt == current and ra == 0:        # li
            return signed16(word & 0xFFFF) & 0xFFFFFFFF
        elif op == 14 and rt == current and ra != current:  # addi rX, rBase, lo
            base = resolve(sections, pc, ra, depth + 1, window=0x1600)
            return None if base is None else (base + signed16(word & 0xFFFF)) & 0xFFFFFFFF
        elif op == 31 and ((word >> 1) & 0x3FF) == 444 and rt == current and ra == rb:   # mr rX, rY
            current = ra
            hi = None
            low = None
            continue
        elif writes_to(word, current):
            # a write we cannot model (a load, an arithmetic op): stop, but keep any half we already have
            break

        if hi is not None and low is not None:
            return (hi | (low & 0xFFFF) if low >= 0 else hi + low) & 0xFFFFFFFF

    if hi is not None:
        return (hi | (low or 0)) & 0xFFFFFFFF
    return None


def base_register_of_prologue(sections, site):
    """Resolve the callee-saved base registers a function sets up, as {register: address}."""
    start = max(0x80000000, site - 0x1200)
    window = words_between(sections, start, site)
    bases = {}
    for index, (pc, word) in enumerate(window):
        op = word >> 26
        rt = (word >> 21) & 0x1F
        ra = (word >> 16) & 0x1F
        if rt in (29, 30, 31) and op in (14, 15, 24, 25):
            value = resolve(sections, pc, rt)
            if value is not None:
                bases[rt] = value
    return bases


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    parser.add_argument('--dol', default=os.path.join(root, 'bin', 'Debug', 'net8.0-windows', 'win-x64',
                                                      'demo_hero', 'demo_hero_ISO', 'bootfile.dol'))
    parser.add_argument('--catalog', default=os.path.join(root, 'docs', 'api', 'catalog.json'))
    parser.add_argument('--out', default=os.path.join(root, 'docs', 'api', 'engine-methods.json'))
    parser.add_argument('--debug-site', default=None, help='print how one call site resolves, e.g. 0x801e7568')
    args = parser.parse_args()

    sections = load_dol(args.dol)
    print(f'DOL sections: {len(sections)}')

    call_sites = []
    for base in sections:
        blob = sections[base]
        if base < 0x80000000 or base >= 0x80300000:
            continue
        for offset in range(0, len(blob) - 3, 4):
            word = struct.unpack_from('>I', blob, offset)[0]
            if (word >> 26) != 18:
                continue
            if decode_branch(word, base + offset) == REGISTRAR:
                call_sites.append(base + offset)
    call_sites.sort()
    print(f'calls to FUN_800080a0: {len(call_sites)}')

    if args.debug_site:
        site = int(args.debug_site, 16)
        print(f'debug {hex(site)}: r3={resolve(sections, site, 3)} r4={resolve(sections, site, 4)}')

    by_class = defaultdict(list)
    unresolved = 0
    for site in call_sites:
        class_object = resolve(sections, site, 3)
        name_pointer = resolve(sections, site, 4)
        name = read_cstring(sections, name_pointer) if name_pointer else None
        if class_object is None or not name or not name[:1].isalpha():
            unresolved += 1
            continue
        by_class[class_object].append({'name': name, 'site': f'0x{site:08x}'})

    print(f'class groups: {len(by_class)}, unresolved: {unresolved}')

    catalog = {}
    if os.path.isfile(args.catalog):
        catalog = json.load(open(args.catalog, encoding='utf-8'))
    observed = {}
    for iface in catalog.get('interfaces', []):
        names = {m['name'].lower() for m in iface['methods'] + iface['properties']}
        if names:
            observed[iface['name']] = names

    groups = []
    for class_object, methods in sorted(by_class.items(), key=lambda kv: -len(kv[1])):
        names = {m['name'].lower() for m in methods}
        best, overlap = None, 0
        for iface_name, iface_names in observed.items():
            shared = len(names & iface_names)
            if shared > overlap:
                best, overlap = iface_name, shared
        groups.append({
            'classObject': f'0x{class_object:08x}',
            'interface': best,
            'overlapWithCorpus': overlap,
            'methodCount': len(methods),
            'methods': [m['name'] for m in methods],
            'sites': [m['site'] for m in methods],
        })

    labelled = [g for g in groups if g['interface']]
    payload = {
        'note': ('Script method tables recovered from bootfile.dol: every call to the method registrar FUN_800080a0, '
                 'with r3 (class object) and r4 (name) resolved from the preceding instructions, grouped by class. '
                 'The interface label is a best match against the corpus-observed method names, not a certainty.'),
        'callSites': len(call_sites),
        'unresolved': unresolved,
        'groups': len(groups),
        'labelledGroups': len(labelled),
        'methodTotal': sum(g['methodCount'] for g in groups),
        'classes': groups,
    }
    with open(args.out, 'w', encoding='utf-8') as fh:
        json.dump(payload, fh, indent=1)

    print(f'\nwrote {args.out}')
    print(f'{"class object":14} {"interface":26} {"overlap":>7} {"methods":>7}')
    for group in groups[:22]:
        print(f'{group["classObject"]:14} {str(group["interface"] or "-"):26} {group["overlapWithCorpus"]:7} '
              f'{group["methodCount"]:7}')
    print(f'\nlabelled groups: {len(labelled)} ({sum(g["methodCount"] for g in labelled)} methods)')
    print(f'unlabelled groups: {len(groups) - len(labelled)} '
          f'({sum(g["methodCount"] for g in groups if not g["interface"])} methods)')


if __name__ == '__main__':
    sys.exit(main())
