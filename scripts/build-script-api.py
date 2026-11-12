#!/usr/bin/env python3
"""Mine the game's scripting API out of the vanilla corpus and build a browsable reference page.

    python scripts/build-script-api.py [--bec <project _BEC>] [--out docs/api]

What it does
------------
There are no developer docs for the Gladius script language, so the API is recovered from what the shipped scripts
actually call:

  * interfaces   - ``shared x = InterfaceBattle()`` binds ``x`` to an interface, so ``x.Method(...)`` is a method of it,
                   with the argument counts and the call sites that use it
  * objects      - a method called on anything else is grouped under the method name, because the receiver's type needs
                   data flow to know (``gb.GetUnitByName(...)`` returns a unit, but that is inference, so it is not claimed)
  * globals      - the builtins the compiler knows about, plus calls that appear in scripts
  * constants    - SHOUTY identifiers, with where they are used
  * confirmations - the bootfile.dol string table is scanned once, so each name can be marked "also present as a literal
                   in the DOL", which is evidence the engine knows it too

Outputs ``docs/api/catalog.json`` (the data) and ``docs/api/index.html`` (a single self-contained page - no CDN, no
server, opens from disk).
"""

import argparse
import html
import json
import os
import re
import sys
from collections import Counter, defaultdict

IDENT = r'[A-Za-z_][A-Za-z0-9_]*'
CALL_INTERFACE = re.compile(r'(' + IDENT + r')\s*\(\s*\)')
BIND = re.compile(r'(' + IDENT + r')\s*=\s*(' + IDENT + r')\s*\(\s*\)')
MEMBER_CALL = re.compile(r'(' + IDENT + r')\s*\.\s*(' + IDENT + r')\s*\(')
PLAIN_CALL = re.compile(r'(?<![.\w])(' + IDENT + r')\s*\(')
MEMBER_READ = re.compile(r'(' + IDENT + r')\s*\.\s*(' + IDENT + r')\b(?!\s*\()')
SHOUTY = re.compile(r'\b([A-Z][A-Z0-9_]{2,})\b')

KEYWORDS = {
    'function', 'endfunction', 'if', 'endif', 'else', 'elseif', 'while', 'endwhile', 'for', 'next', 'to', 'step',
    'switch', 'case', 'default', 'endswitch', 'return', 'shared', 'global', 'break', 'continue', 'goto', 'label',
    'and', 'or', 'not', 'true', 'false', 'null', 'self', 'do', 'until', 'repeat',
}


def strip_comments_and_strings(text):
    """Blank out comments and string literals, keeping line/column geometry for the examples."""
    out = []
    i = 0
    n = len(text)
    while i < n:
        ch = text[i]
        if ch == '/' and i + 1 < n and text[i + 1] == '/':
            while i < n and text[i] != '\n':
                out.append(' ')
                i += 1
        elif ch == '"':
            out.append(' ')
            i += 1
            while i < n and text[i] != '"':
                if text[i] == '\n':
                    break
                out.append(' ')
                i += 1
            if i < n and text[i] == '"':
                out.append(' ')
                i += 1
        else:
            out.append(ch)
            i += 1
    return ''.join(out)


def dol_strings(path):
    """Printable ASCII runs in the DOL, which is enough to confirm that a name appears as a literal."""
    if not path or not os.path.isfile(path):
        return set()
    data = open(path, 'rb').read()
    found = set()
    for match in re.finditer(rb'[ -~]{3,}', data):
        found.add(match.group().decode('ascii'))
        # a table entry may be a suffix of a longer literal
        piece = match.group()
        if b'\x00' in piece:
            for part in piece.split(b'\x00'):
                if len(part) >= 3:
                    found.add(part.decode('ascii'))
    return found


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    parser.add_argument('--bec', default=os.path.join(root, 'bin', 'Debug', 'net8.0-windows', 'win-x64',
                                                      'demo_hero', 'demo_hero_BEC'))
    parser.add_argument('--dol', default=None, help='bootfile.dol, used to confirm names (default: <project>_ISO)')
    parser.add_argument('--out', default=os.path.join(root, 'docs', 'api'))
    parser.add_argument('--max-examples', type=int, default=3)
    args = parser.parse_args()

    script_dir = os.path.join(args.bec, 'data', 'script')
    if not os.path.isdir(script_dir):
        raise SystemExit('no data/script under ' + args.bec)

    dol_path = args.dol
    if dol_path is None:
        iso = args.bec.replace('_BEC', '_ISO')
        candidate = os.path.join(iso, 'bootfile.dol')
        dol_path = candidate if os.path.isfile(candidate) else None

    # builtins the compiler treats as global functions
    builtins = set()
    try:
        sys.path.insert(0, os.path.join(root, 'tools_v009'))
        from tools_v009.dogcc import dogopcodes
        builtins = {name.lower() for name in getattr(dogopcodes, 'BUILTINS', [])}
    except Exception:
        pass

    strings = dol_strings(dol_path)
    print(f'DOL strings: {len(strings)}' + ('' if dol_path else ' (no bootfile.dol found)'))

    spellings = defaultdict(Counter)
    interfaces = defaultdict(lambda: {
        'name': '',
        'methods': defaultdict(lambda: {'name': '', 'arities': Counter(), 'calls': 0, 'files': set(), 'examples': []}),
        'properties': defaultdict(lambda: {'name': '', 'reads': 0, 'files': set(), 'examples': []}),
        'creators': 0,
        'files': set(),
    })
    untyped = defaultdict(lambda: {'name': '', 'arities': Counter(), 'calls': 0, 'files': set(), 'examples': []})
    globals_called = defaultdict(lambda: {'name': '', 'arities': Counter(), 'calls': 0, 'files': set(), 'examples': []})
    constants = defaultdict(lambda: {'name': '', 'uses': 0, 'files': set(), 'examples': []})

    scripts = sorted(f for f in os.listdir(script_dir) if f.lower().endswith('.scp'))
    for name in scripts:
        raw = open(os.path.join(script_dir, name), 'rb').read().decode('cp1252', errors='replace')
        code = strip_comments_and_strings(raw)
        raw_lines = raw.splitlines()

        # 1. which identifiers are interfaces, and which variables hold them
        bindings = {}
        for match in BIND.finditer(code):
            variable, creator = match.group(1), match.group(2)
            if not creator.lower().startswith('interface'):
                continue
            bindings[variable] = creator
            entry = interfaces[creator.lower()]
            spellings[creator.lower()][creator] += 1
            entry['name'] = creator
            entry['creators'] += 1
            entry['files'].add(name)

        # 2. member calls / reads, attributed to the interface when the receiver is a known binding
        for match in MEMBER_CALL.finditer(code):
            receiver, method = match.group(1), match.group(2)
            line = code.count('\n', 0, match.start()) + 1
            snippet = raw_lines[line - 1].strip() if 0 < line <= len(raw_lines) else ''
            if receiver in bindings:
                owner = interfaces[bindings[receiver].lower()]
                record = owner['methods'][method.lower()]
                record['name'] = method
                record['calls'] += 1
                record['files'].add(name)
                record['arities'][count_args(code, match.end())] += 1
                if len(record['examples']) < args.max_examples:
                    record['examples'].append({'file': name, 'line': line, 'code': snippet})
            else:
                record = untyped[method.lower()]
                record['name'] = method
                record['calls'] += 1
                record['files'].add(name)
                record['arities'][count_args(code, match.end())] += 1
                if len(record['examples']) < args.max_examples:
                    record['examples'].append({'file': name, 'line': line, 'code': snippet})

        for match in MEMBER_READ.finditer(code):
            receiver, prop = match.group(1), match.group(2)
            if receiver not in bindings:
                continue
            line = code.count('\n', 0, match.start()) + 1
            snippet = raw_lines[line - 1].strip() if 0 < line <= len(raw_lines) else ''
            owner = interfaces[bindings[receiver].lower()]
            record = owner['properties'][prop.lower()]
            record['name'] = prop
            record['reads'] += 1
            record['files'].add(name)
            if len(record['examples']) < args.max_examples:
                record['examples'].append({'file': name, 'line': line, 'code': snippet})

        # 3. global calls
        for match in PLAIN_CALL.finditer(code):
            func = match.group(1)
            low = func.lower()
            if low in KEYWORDS or low.startswith('interface') or low in bindings:
                continue
            line = code.count('\n', 0, match.start()) + 1
            snippet = raw_lines[line - 1].strip() if 0 < line <= len(raw_lines) else ''
            record = globals_called[low]
            record['name'] = func
            record['calls'] += 1
            record['files'].add(name)
            record['arities'][count_args(code, match.end())] += 1
            if len(record['examples']) < args.max_examples:
                record['examples'].append({'file': name, 'line': line, 'code': snippet})

        # 4. constants
        for match in SHOUTY.finditer(code):
            value = match.group(1)
            if value.lower() in KEYWORDS:
                continue
            line = code.count('\n', 0, match.start()) + 1
            snippet = raw_lines[line - 1].strip() if 0 < line <= len(raw_lines) else ''
            record = constants[value]
            record['name'] = value
            record['uses'] += 1
            record['files'].add(name)
            if len(record['examples']) < 2:
                record['examples'].append({'file': name, 'line': line, 'code': snippet})

    def finish(record, dol=True):
        record['files'] = sorted(record['files'])
        if 'arities' in record:
            record['arities'] = dict(sorted(record['arities'].items(), key=lambda kv: (-kv[1], kv[0])))
        if dol:
            record['inDol'] = record['name'] in strings
        return record

    catalog = {
        'generatedFrom': os.path.basename(args.bec),
        'scripts': len(scripts),
        'dolStrings': len(strings),
        'interfaces': [],
        'untypedMethods': [],
        'globals': [],
        'constants': [],
    }

    for key, owner in sorted(interfaces.items(), key=lambda kv: kv[1]['name'].lower()):
        methods = [finish(dict(record)) for record in owner['methods'].values()]
        methods.sort(key=lambda r: r['name'].lower())
        properties = [finish(dict(record), dol=False) for record in owner['properties'].values()]
        properties.sort(key=lambda r: r['name'].lower())
        for record in properties:
            record['inDol'] = record['name'] in strings
        catalog['interfaces'].append({
            'name': (spellings[key].most_common(1)[0][0] if spellings.get(key) else owner['name']),
            'creators': owner['creators'],
            'files': sorted(owner['files']),
            'methods': methods,
            'properties': properties,
        })

    catalog['untypedMethods'] = sorted(
        (finish(dict(record)) for record in untyped.values()), key=lambda r: r['name'].lower())
    catalog['globals'] = sorted(
        (finish(dict(record)) for record in globals_called.values()), key=lambda r: r['name'].lower())
    catalog['constants'] = sorted(
        (finish(dict(record), dol=False) for record in constants.values()), key=lambda r: r['name'].lower())
    for record in catalog['constants']:
        record['inDol'] = record['name'] in strings

    # ---- engine table merge ---------------------------------------------------------------------------------------
    # extract-engine-methods.py recovers the method tables straight from bootfile.dol (every call to the method
    # registrar, grouped by class). Merge it in so each interface shows what the engine offers, not only what scripts use.
    engine_path = os.path.join(args.out, 'engine-methods.json')
    if os.path.isfile(engine_path):
        engine = json.load(open(engine_path, encoding='utf-8'))
        observed_all = set()
        for iface in catalog['interfaces']:
            for record in iface['methods'] + iface['properties']:
                observed_all.add(record['name'].lower())
        for record in catalog['untypedMethods'] + catalog['globals']:
            observed_all.add(record['name'].lower())

        by_interface = {}
        for group in engine.get('classes', []):
            label = group.get('interface')
            if not label:
                continue
            entry = by_interface.setdefault(label, {'classObject': group['classObject'], 'methods': []})
            entry['methods'].extend(group['methods'])

        for iface in catalog['interfaces']:
            entry = by_interface.get(iface['name'])
            if not entry:
                continue
            names = []
            seen = set()
            for name in entry['methods']:
                if name.lower() in seen:
                    continue
                seen.add(name.lower())
                names.append({'name': name, 'observed': name.lower() in observed_all})
            iface['engineClass'] = entry['classObject']
            iface['engineMethods'] = names
            iface['engineOnly'] = [n['name'] for n in names if not n['observed']]

        catalog['engineOnly'] = sorted({n for iface in catalog['interfaces']
                                        for n in iface.get('engineOnly', [])})

    os.makedirs(args.out, exist_ok=True)
    with open(os.path.join(args.out, 'catalog.json'), 'w', encoding='utf-8') as fh:
        json.dump(catalog, fh, indent=1, sort_keys=True)

    with open(os.path.join(args.out, 'index.html'), 'w', encoding='utf-8') as fh:
        fh.write(render(catalog))

    method_count = sum(len(i['methods']) for i in catalog['interfaces'])
    print(f'interfaces: {len(catalog["interfaces"])}, interface methods: {method_count}, '
          f'untyped methods: {len(catalog["untypedMethods"])}, globals: {len(catalog["globals"])}, '
          f'constants: {len(catalog["constants"])}')
    print(f'wrote {os.path.join(args.out, "catalog.json")} and index.html')


def count_args(code, index):
    """Argument count at an opening parenthesis already consumed by the caller's match."""
    depth = 1
    count = 0
    seen = False
    i = index
    while i < len(code) and depth > 0:
        ch = code[i]
        if ch == '(':
            depth += 1
        elif ch == ')':
            depth -= 1
            if depth == 0:
                break
        elif depth == 1 and ch == ',':
            count += 1
        elif depth == 1 and not ch.isspace():
            seen = True
        i += 1
    if count == 0:
        return 0 if not seen else 1
    return count + 1


def render(catalog):
    data = json.dumps(catalog, separators=(',', ':'))
    template_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'api_page_template.html')
    template = open(template_path, encoding='utf-8').read()
    return template.replace('/*CATALOG*/null', data)


if __name__ == '__main__':
    main()
