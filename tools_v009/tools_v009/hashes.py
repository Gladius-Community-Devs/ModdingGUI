# -*- coding: utf-8 -*-
"""Hash table loader.

Maps Gladius CRC-32 path hashes to human-readable paths.  The table lives in
``hashes.txt`` (one ``0xhash path`` entry per line) so that adding a resolution
is a one-line edit with a clean diff.

``filenameHashes`` maps ``int`` path hash -> ``str`` path.
``hashes`` maps ``str`` md5 hex -> ``str`` path (used by pak extraction; kept
empty unless populated).

The table file is located relative to this package (``<project>/hashes.txt``)
and, when frozen by PyInstaller, relative to the bundle/executable.
"""

import os
import sys

filenameHashes = {}
hashes = {}

_HASHES_FILENAME = "hashes.txt"


def _candidate_paths():
    here = os.path.dirname(os.path.abspath(__file__))
    # Both layouts are real: v008 shipped hashes.txt in the *parent* of the package, while a plain copy of the package
    # (and the ModdingGUI toolbelt) keeps it beside the modules. Without the "here" entry every path resolves to
    # unknown-<tag>-0x<...>, which is what a validation unpack showed before this was fixed.
    yield os.path.join(here, _HASHES_FILENAME)
    yield os.path.join(os.path.dirname(here), _HASHES_FILENAME)

    if getattr(sys, "frozen", False):
        exe_dir = os.path.dirname(sys.executable)
        yield os.path.join(exe_dir, _HASHES_FILENAME)
        meipass = getattr(sys, "_MEIPASS", None)
        if meipass:
            yield os.path.join(meipass, _HASHES_FILENAME)

    yield os.path.join(os.getcwd(), _HASHES_FILENAME)


def load(path=None):
    """(Re)load the hash table from ``path`` (or the first candidate that exists)."""
    global filenameHashes, hashes
    filenameHashes = {}
    hashes = {}

    if path is None:
        for candidate in _candidate_paths():
            if os.path.isfile(candidate):
                path = candidate
                break

    if path is None or not os.path.isfile(path):
        return

    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            parts = line.split(None, 1)
            if len(parts) != 2:
                continue
            try:
                key = int(parts[0], 16)
            except ValueError:
                continue
            filenameHashes[key] = parts[1]


def lookup(hashcode):
    """Return the resolved path for ``hashcode`` or ``None``."""
    return filenameHashes.get(hashcode)


load()
