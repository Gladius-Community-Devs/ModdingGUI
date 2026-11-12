#!/usr/bin/env python3
"""dogwriter.py - assemble a DogCode .scb byte stream from semantic parts.

Mirrors the verified writer behaviour of DOGCodeCompiler.exe (and the 2003
engine compiler in protius): fixed order chunks, each chunk header {tag,
field}, payload framing with per-chunk-type field semantics:

  chunk      header field                          physical payload
  RTSN       len of string area (excl. count dword)  u32 count + literals\0...
  RTSS       len of string area (excl. count dword)  u32 count + names\0...
  BMYS       len in bytes (= 12*nsym)                 records (12 bytes each)
  YSAS       len in bytes (= 12*nshared)              records (12 bytes each)
  EDOC       len in bytes of serialized opcodes       opcode stream
  TRPX(TRXE) count of 36-byte export records          36 bytes * count
  ELIF       count of external script names           names\0...
  OFNI       (debug; never emitted here)

Directory: 'SGOD' + u32 version + 8 {tag, offset} entries (absent = 0xFFFFFFFF)
in fixed order RTSN RTSS BMYS YSAS EDOC TRXE ELIF OFNI + u32 0 terminator.
Each chunk start (and file end) is 4-byte aligned; body chunk for the 6th
entry is spelled TRPX while the directory spells TRXE.
"""
import struct

VERSION = 0x00010086
CHUNK_ORDER = [b"RTSN", b"RTSS", b"BMYS", b"YSAS", b"EDOC", b"TRXE", b"ELIF", b"OFNI"]
BODY_TAG = {b"TRXE": b"TRPX"}

def _u32le(v):
    return struct.pack("<I", v & 0xFFFFFFFF)

def emit_scb(*, rtsn_lits=(), rtss_names=(), bmys_payload=b"", ysas_payload=b"",
             edoc_code=b"", exports=0, elif_names=(), ofni_payload=None,
             trpx_payload=None, version=VERSION):
    """Build complete .scb bytes from semantic parts.

    rtsn_lits/rtss_names: ordered list of strings (escapes already decoded,
    stored raw + NUL). bmys_payload/ysas_payload: raw 12-byte-record bytes.
    edoc_code: serialized opcode bytes. exports: number of TRPX records
    (36 bytes each; pass trpx_payload to provide the actual record bytes).
    elif_names: external script base names (no path/extension).
    """
    # precompute chunk payloads and header 'field' values
    chunks = []          # (body_tag, field, physical_payload_bytes)
    def enc(s):
        return s.encode('cp1252')
    if rtsn_lits is not None:
        area = b"".join(enc(s) + b"\x00" for s in rtsn_lits)
        chunks.append((b"RTSN", len(area), struct.pack("<I", len(rtsn_lits)) + area))
    if rtss_names is not None:
        area = b"".join(enc(s) + b"\x00" for s in rtss_names)
        chunks.append((b"RTSS", len(area), struct.pack("<I", len(rtss_names)) + area))
    if bmys_payload is not None and bmys_payload:
        chunks.append((b"BMYS", len(bmys_payload), bmys_payload))
    if ysas_payload is not None and ysas_payload:
        chunks.append((b"YSAS", len(ysas_payload), ysas_payload))
    if edoc_code is not None and edoc_code:
        chunks.append((b"EDOC", len(edoc_code), edoc_code))
    if exports is not None and exports > 0:
        if trpx_payload is None:
            trpx_payload = b"\x00" * (36 * exports)
        chunks.append((b"TRPX", exports, trpx_payload))
    if elif_names is not None and elif_names:
        payload = b"".join(n.encode("cp1252") + b"\x00" for n in elif_names)
        chunks.append((b"ELIF", len(elif_names), payload))
    if ofni_payload is not None:
        chunks.append((b"OFNI", len(ofni_payload), ofni_payload))

    header_size = 8 + 8 * len(CHUNK_ORDER) + 4
    # lay out body
    body = bytearray()
    offsets = {}
    for body_tag, field, payload in chunks:
        while (header_size + len(body)) % 4 != 0:
            body += b"\x00"
        offsets[body_tag] = header_size + len(body)
        body += BODY_TAG.get(body_tag, body_tag)
        body += _u32le(field)
        body += payload
    while (header_size + len(body)) % 4 != 0:
        body += b"\x00"

    out = bytearray()
    out += b"SGOD" + _u32le(version)
    for tag in CHUNK_ORDER:
        body_tag = BODY_TAG.get(tag, tag)
        out += tag
        out += _u32le(offsets[body_tag] if body_tag in offsets else 0xFFFFFFFF)
    out += _u32le(0)
    out += body
    return bytes(out)


def write(path, data):
    with open(path, "wb") as f:
        f.write(data)
