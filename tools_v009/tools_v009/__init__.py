# -*- coding: utf-8 -*-
"""Gladius (GC 2003) modding toolchain.

Importable core modules for the BEC archive, unit IDX tables, ``.tok`` text,
lookup-string tables, GameCube ISO handling, and the filename-hash table.
"""

from tools_v009 import bec, idx, tok, strings, iso, hashes

__all__ = ["bec", "idx", "tok", "strings", "iso", "hashes"]

__version__ = "1.0.0"
