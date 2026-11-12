#!/usr/bin/env python3
"""scb_model.py - shared .scb parse helpers for the DogCode RE project.

Provides exact, tested section parsing (container level fully solved).
"""
import struct

DIR_TAGS = [b"RTSN", b"RTSS", b"BMYS", b"YSAS", b"EDOC", b"TRXE", b"ELIF", b"OFNI"]

def u32(b, o): return struct.unpack_from("<I", b, o)[0]
def s32(b, o): return struct.unpack_from("<i", b, o)[0]
def f32(b, o): return struct.unpack_from("<f", b, o)[0]

def parse(data):
    """Return (version, {tag: (offset, header_field, payload_bytes, body_tag)})."""
    assert data[:4] == b"SGOD"
    version = u32(data, 4)
    sections = {}
    for i, tag in enumerate(DIR_TAGS):
        off = u32(data, 12 + 8 * i)
        if off == 0xFFFFFFFF:
            continue
        body_tag = data[off:off + 4]
        field = u32(data, off + 4)
        # physical payload size per chunk type (validated corpus-wide)
        if tag in (b"RTSN", b"RTSS"):
            pay = data[off + 8: off + 8 + 4 + field]   # count dword + string area
        elif tag == b"TRXE":
            pay = data[off + 8: off + 8 + 36 * field]
        elif tag == b"ELIF":
            # field = number of names; payload = that many cstrings (unpadded)
            i = off + 8
            for _ in range(field):
                e = data.find(b"\x00", i)
                if e < 0:
                    e = len(data)
                i = e + 1
            pay = data[off + 8: i]
        else:
            pay = data[off + 8: off + 8 + field]
        sections[tag] = (off, field, pay, body_tag)
    return version, sections

def str_pool(payload):
    """Decode [u32 count][cstrings] payload; returns (count, [strings])."""
    count = u32(payload, 0)
    out = []
    i = 4
    for _ in range(count):
        e = payload.find(b"\x00", i)
        assert e >= 0
        out.append(payload[i:e].decode("latin1"))
        i = e + 1
    return count, out

def load(path):
    with open(path, "rb") as f:
        return parse(f.read())
