#!/usr/bin/env python3
"""
pttp_tga.py - view and convert Gladius `.tga` / `.ptx` texture pages.

Every file in `data/texture/` named `.tga` (and `.ptx`) is NOT a Targa file. It is a
`PTTP` texture page: a chunk container holding one or more CMPR (GameCube DXT1)
surfaces. That is why no image viewer will open them.

This is a single file with NO third-party dependencies - only the Python standard
library, so it needs no pip install and bundles cleanly into a one-file .exe.

    py pttp_tga.py                       # browse the tree (GUI) from the current dir
    py pttp_tga.py <dir-or-file>         # browse a specific folder or open a file
    py pttp_tga.py --info <path>         # print the surface table, decode nothing
    py pttp_tga.py --png <src> <outdir>  # batch-convert a file or tree to PNG

Format, as established in GladiusRecomp docs/formats/PTTP.md (ss6.2, ss6.5, ss6.8):

    chunk  +0x00  4   tag ("PTTP" "NMPT" "PFHD" "PADD" "PTDT" "END.")
    chunk  +0x04  u32 declared size of the chunk, including its 16-byte header

    record i sits at PFHD + 0x10 + i*32, and the fields are
    record +0x01  u8   format: 0x20 = CMPR colour, 0x22 = CMPR colour + alpha mask
    record +0x02  u16  mip level count (0 in every single-image .tga here)
    record +0x08  u16  width  - THE DRAWN WINDOW, not the stored width
    record +0x0A  u16  height - THE DRAWN WINDOW, not the stored height
    record +0x0C  u32  byte length of this surface's data
    record +0x10  u32  offset to that data, relative to the RECORD's own address
    record +0x14  u32  name offset, relative to the NMPT chunk's payload

Two rules that decide whether the picture is right or a staircase of noise:

  * the image is STORED at the next power of two in each axis, and the declared
    width/height is only the window the engine draws out of it. Decoding at the
    declared width shifts every 8x8 block row after the first - the "staircase".
  * CMPR is 4 bits per texel in 8x8 blocks of 32 bytes: four 4x4 DXT1 sub-blocks,
    left to right then top to bottom. A 0x22 surface is TWO such planes, and the
    second plane's GREEN channel is the alpha mask.
"""

import os
import struct
import sys
import zlib

APP = "PTTP texture viewer"
VERSION = "1.0"

TEXTURE_EXTS = (".tga", ".ptx")


# --------------------------------------------------------------------------
# RGB565 -> RGB.  The exact expansion used by GladiusRecomp's GxTexture.cs, so
# the output matches the project's reference decoder texel for texel.
# --------------------------------------------------------------------------
def _build_565():
    return [bytes(((v >> 11 & 0x1F) * 255 // 31,
                   (v >> 5 & 0x3F) * 255 // 63,
                   (v & 0x1F) * 255 // 31)) for v in range(65536)]


_C565 = None


def c565():
    global _C565
    if _C565 is None:
        _C565 = _build_565()
    return _C565


def _u32(b, o):
    return b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24)


def _u16(b, o):
    return b[o] | (b[o + 1] << 8)


def stored_extent(declared):
    """The next power of two >= declared (0 for a non-positive input)."""
    if declared <= 0:
        return 0
    e = 1
    while e < declared:
        e <<= 1
    return e


class Surface:
    """One PFHD record: a single image inside a page."""

    __slots__ = ("index", "fmt", "levels", "width", "height", "data_len",
                 "data_off", "raw_doff", "rec_addr", "name", "buf", "page")

    def __init__(self, **kw):
        for k, v in kw.items():
            setattr(self, k, v)

    @property
    def stored_w(self):
        return stored_extent(self.width)

    @property
    def stored_h(self):
        return stored_extent(self.height)

    @property
    def kind(self):
        """Which pixel format this record uses.

        `0x20`/`0x22` are the GameCube ids (GX CMPR: 8x8 blocks of 32 bytes,
        big-endian). `0x41`/`0x43` are the Xbox ids (DXT1 / DXT5: 4x4 blocks of
        8 / 16 bytes, little-endian). Both are 4 bpp / 8 bpp respectively, which
        is why the payload sizes alone cannot tell them apart.
        """
        return {0x20: "cmpr", 0x22: "cmpr+a", 0x41: "dxt1", 0x43: "dxt5"}.get(
            self.fmt, "?")

    @property
    def planes(self):
        # Only the GameCube 0x22 keeps its mask in a second plane. Xbox DXT5
        # carries alpha inside each block, so it is a single plane.
        return 2 if self.fmt == 0x22 else 1

    @property
    def level0_bytes(self):
        """Bytes one level-0 image needs at the stored size."""
        sw = max(self.stored_w, 1)
        sh = max(self.stored_h, 1)
        if self.fmt in (0x20, 0x22):
            return max(1, sw // 8) * max(1, sh // 8) * 32
        if self.fmt == 0x41:
            return max(1, sw // 4) * max(1, sh // 4) * 8
        if self.fmt == 0x43:
            return max(1, sw // 4) * max(1, sh // 4) * 16
        return 0

    @property
    def has_image(self):
        return (self.data_off is not None and self.level0_bytes > 0
                and self.data_len >= self.level0_bytes)

    @property
    def stub(self):
        """A record that declares a surface but stores no pixels.

        Common on both platforms and NOT a decode failure: the page names the
        surface but its image lives elsewhere. There are two markers - a 32-byte
        record (GameCube) and a `0xFFFFFFFF` data offset (Xbox) - and neither
        pointer may be followed: the GameCube stub's points at the NEXT data
        record, so decoding through it yields a crop of a DIFFERENT image.
        """
        return not self.has_image

    def describe(self, show_index=True):
        return ("%s%s  %dx%d (stored %dx%d)  fmt=0x%02X (%s)  %d bytes  "
                "planes=%d  levels=%d%s" % (
                    "[%d] " % self.index if show_index else "",
                    self.name or "?", self.width, self.height,
                    self.stored_w, self.stored_h, self.fmt, self.kind.upper(),
                    self.data_len, self.planes, self.levels,
                    "  [NO PIXELS: declared only]" if self.stub else ""))


def parse_page(data):
    """Return the list of Surfaces in a PTTP page, or raise ValueError."""
    buf = data
    if buf[:1] == b"\x78":                      # some pages are zlib-wrapped
        try:
            buf = zlib.decompress(buf)
        except zlib.error:
            pass
    if buf[:4] != b"PTTP":
        raise ValueError("not a PTTP page (starts %r)" % buf[:4])

    chunks = []
    off = 0
    n = len(buf)
    while off + 16 <= n:
        tag = buf[off:off + 4]
        size = _u32(buf, off + 4)
        chunks.append((tag, off, size))
        if tag == b"END." or size < 16:
            break
        off += size

    pfhd = next((c for c in chunks if c[0] == b"PFHD"), None)
    if pfhd is None:
        raise ValueError("no PFHD chunk - this page carries no images")
    nmpt = next((c for c in chunks if c[0] == b"NMPT"), None)
    nmpt_payload = nmpt[1] + 16 if nmpt else 0
    ptdt = next((c for c in chunks if c[0] == b"PTDT"), None)

    _, poff, psize = pfhd
    count = _u32(buf, poff + 0x0C)
    # The record array always fits inside the chunk (PTTP.md 6.3), but a
    # malformed count must not walk off the end silently.
    if count < 0 or count * 32 > max(0, psize - 16):
        count = max(0, (psize - 16) // 32)

    out = []
    for i in range(count):
        rec = poff + 0x10 + i * 32
        if rec + 32 > n:
            break
        fmt = buf[rec + 0x01]
        levels = _u16(buf, rec + 0x02)
        w = _u16(buf, rec + 0x08)
        h = _u16(buf, rec + 0x0A)
        dlen = _u32(buf, rec + 0x0C)
        doff = _u32(buf, rec + 0x10)
        nameoff = _u32(buf, rec + 0x14)

        name = ""
        if nmpt:
            p = nmpt_payload + nameoff
            if 0 <= p < n:
                end = buf.find(b"\x00", p)
                if end < 0:
                    end = min(n, p + 64)
                try:
                    name = buf[p:end].decode("latin-1")
                except Exception:
                    name = ""

        out.append(Surface(index=i, fmt=fmt, levels=levels, width=w, height=h,
                           data_len=dlen, data_off=None, raw_doff=doff,
                           rec_addr=rec, name=name, buf=buf, page=None))

    _resolve_data_offsets(buf, out, ptdt)
    return out


def _resolve_data_offsets(buf, surfs, ptdt):
    """Turn each record's offset word into an absolute file offset.

    The field is RECORD-relative - `recordAddress + u32@+0x10` - which is the
    documented GameCube rule (`PTTP.md` 6.2). Measured on the Xbox build it holds
    there too: over all 3,569 Xbox pages that carry an image, this reading puts
    every surface's data inside `PTDT` and at exactly the running cursor (the
    preceding surfaces' lengths summed), with 0 exceptions.

    A word of `0xFFFFFFFF` means the record stores NO data at all - it declares a
    name only. The Xbox build uses that marker where the GameCube uses a 32-byte
    record. Neither is an alias to follow: the GameCube stub's pointer aims at the
    NEXT data record, so decoding through it yields a crop of a DIFFERENT image.
    """
    for s in surfs:
        s.data_off = (None if s.raw_doff == 0xFFFFFFFF
                      else s.rec_addr + s.raw_doff)


# --------------------------------------------------------------------------
# CMPR (GameCube DXT1) decode
# --------------------------------------------------------------------------
def decode_cmpr(buf, off, w, h):
    """Decode one CMPR plane of w x h texels (both must be multiples of 8)."""
    pal565 = c565()

    def idx4(c0, c1):
        r0, g0, b0 = pal565[c0]
        r1, g1, b1 = pal565[c1]
        if c0 > c1:
            return (bytes((r0, g0, b0, 255)),
                    bytes((r1, g1, b1, 255)),
                    bytes(((2 * r0 + r1) // 3, (2 * g0 + g1) // 3,
                           (2 * b0 + b1) // 3, 255)),
                    bytes(((r0 + 2 * r1) // 3, (g0 + 2 * g1) // 3,
                           (b0 + 2 * b1) // 3, 255)))
        return (bytes((r0, g0, b0, 255)),
                bytes((r1, g1, b1, 255)),
                bytes(((r0 + r1) // 2, (g0 + g1) // 2, (b0 + b1) // 2, 255)),
                b"\x00\x00\x00\x00")            # DXT1 punch-through

    rgba = bytearray(w * h * 4)
    bw, bh = w >> 3, h >> 3
    for by in range(bh):
        for bx in range(bw):
            base = off + ((by * bw) + bx) * 32
            for sub in range(4):
                so = base + sub * 8
                c0 = (buf[so] << 8) | buf[so + 1]
                c1 = (buf[so + 2] << 8) | buf[so + 3]
                bits = ((buf[so + 4] << 24) | (buf[so + 5] << 16) |
                        (buf[so + 6] << 8) | buf[so + 7])
                pal = idx4(c0, c1)
                sx = (bx << 3) + ((sub & 1) << 2)
                sy = (by << 3) + ((sub >> 1) << 2)
                for ty in range(4):
                    row = ((sy + ty) * w + sx) * 4
                    shift_row = 30 - (ty << 3)
                    for tx in range(4):
                        i = (bits >> (shift_row - (tx << 1))) & 3
                        o = row + (tx << 2)
                        rgba[o:o + 4] = pal[i]
    return rgba


# --------------------------------------------------------------------------
# DXT1 / DXT5 (the Xbox ids 0x41 / 0x43). 4x4 blocks, LITTLE-endian, and the
# index field is read LSB-first - the opposite of the GameCube CMPR layout above.
# Verified against the Xbox build's own `3d_loading51264.tga`, which decodes to
# the gold "LOADING" word, and `loadingscreen_01.tga`, which decodes to the
# GLADIUS title art.
# --------------------------------------------------------------------------
def _dxt1_colour(buf, off, out, w, h, rx, ry):
    """Paint one 8-byte DXT1 colour block at (rx, ry)."""
    pal565 = c565()
    c0, c1 = struct.unpack_from("<HH", buf, off)
    bits = struct.unpack_from("<I", buf, off + 4)[0]
    r0, g0, b0 = pal565[c0]
    r1, g1, b1 = pal565[c1]
    if c0 > c1:
        pal = (bytes((r0, g0, b0, 255)), bytes((r1, g1, b1, 255)),
               bytes(((2 * r0 + r1) // 3, (2 * g0 + g1) // 3,
                      (2 * b0 + b1) // 3, 255)),
               bytes(((r0 + 2 * r1) // 3, (g0 + 2 * g1) // 3,
                      (b0 + 2 * b1) // 3, 255)))
    else:
        pal = (bytes((r0, g0, b0, 255)), bytes((r1, g1, b1, 255)),
               bytes(((r0 + r1) // 2, (g0 + g1) // 2, (b0 + b1) // 2, 255)),
               b"\x00\x00\x00\x00")
    for ty in range(4):
        y = ry + ty
        if y >= h:
            break
        row = (y * w) * 4
        for tx in range(4):
            x = rx + tx
            if x >= w:
                break
            i = (bits >> ((ty << 3) + (tx << 1))) & 3
            o = row + (x << 2)
            out[o:o + 4] = pal[i]


def decode_dxt1(buf, off, w, h):
    rgba = bytearray(w * h * 4)
    bw = max(1, (w + 3) // 4)
    for by in range(max(1, (h + 3) // 4)):
        for bx in range(bw):
            _dxt1_colour(buf, off + ((by * bw) + bx) * 8, rgba, w, h,
                         bx << 2, by << 2)
    return rgba


def decode_dxt5(buf, off, w, h):
    rgba = bytearray(w * h * 4)
    bw = max(1, (w + 3) // 4)
    for by in range(max(1, (h + 3) // 4)):
        for bx in range(bw):
            o = off + ((by * bw) + bx) * 16
            a0 = buf[o]
            a1 = buf[o + 1]
            abits = int.from_bytes(bytes(buf[o + 2:o + 8]), "little")
            if a0 > a1:
                alphas = [a0, a1] + [((6 - i) * a0 + (1 + i) * a1) // 7
                                     for i in range(6)]
            else:
                alphas = [a0, a1] + [((4 - i) * a0 + (1 + i) * a1) // 5
                                     for i in range(4)] + [0, 255]
            _dxt1_colour(buf, o + 8, rgba, w, h, bx << 2, by << 2)
            rx, ry = bx << 2, by << 2
            for ty in range(4):
                y = ry + ty
                if y >= h:
                    break
                row = (y * w) * 4
                for tx in range(4):
                    x = rx + tx
                    if x >= w:
                        break
                    i = (abits >> ((ty * 12) + (tx * 3))) & 7
                    rgba[row + (x << 2) + 3] = alphas[i]
    return rgba


def decode_surface(surf, crop=True):
    """
    Decode a surface to (rgba bytearray, width, height).

    The image comes out at the STORED (power-of-two) size; `crop` trims it to the
    declared drawn window, which is what the engine actually shows.
    """
    if not surf.has_image:
        raise ValueError("record %d carries no pixels of its own "
                         "(declared name only)" % surf.index)
    buf = surf.buf
    sw, sh = surf.stored_w, surf.stored_h
    need = surf.level0_bytes

    end = surf.data_off + need
    if end > len(buf):
        raise ValueError("surface data runs past the end of the file")
    if surf.kind.startswith("cmpr"):
        rgba = decode_cmpr(buf, surf.data_off, sw, sh)
    elif surf.kind == "dxt1":
        rgba = decode_dxt1(buf, surf.data_off, sw, sh)
    elif surf.kind == "dxt5":
        rgba = decode_dxt5(buf, surf.data_off, sw, sh)
    else:
        raise ValueError("unknown pixel format 0x%02X" % surf.fmt)

    if surf.planes >= 2:
        # The two planes are consecutive runs of DataLength/2 bytes; the alpha
        # mask lives in the SECOND plane's green channel.
        aoff = surf.data_off + surf.data_len // 2
        if aoff + need <= len(buf):
            mask = decode_cmpr(buf, aoff, sw, sh)
            for i in range(3, (sw * sh) * 4, 4):
                rgba[i] = mask[i - 2]           # i-2 == green of the same texel

    if crop:
        cw = min(surf.width or sw, sw)
        ch = min(surf.height or sh, sh)
        if (cw, ch) != (sw, sh):
            rgba = crop_rgba(rgba, sw, sh, cw, ch)
            sw, sh = cw, ch
    return rgba, sw, sh


def crop_rgba(rgba, w, h, cw, ch):
    out = bytearray(cw * ch * 4)
    for y in range(ch):
        s = (y * w) * 4
        d = (y * cw) * 4
        out[d:d + cw * 4] = rgba[s:s + cw * 4]
    return out


# --------------------------------------------------------------------------
# PNG writing (stdlib only: zlib + struct)
# --------------------------------------------------------------------------
def png_bytes(rgba, w, h):
    raw = bytearray()
    stride = w * 4
    for y in range(h):
        raw.append(0)                           # filter: None
        raw += rgba[y * stride:(y + 1) * stride]

    def chunk(tag, payload):
        return (struct.pack(">I", len(payload)) + tag + payload +
                struct.pack(">I", zlib.crc32(tag + payload) & 0xFFFFFFFF))

    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
            + chunk(b"IEND", b""))


def save_png(path, rgba, w, h):
    d = os.path.dirname(path)
    if d:
        os.makedirs(d, exist_ok=True)
    with open(path, "wb") as f:
        f.write(png_bytes(rgba, w, h))


# --------------------------------------------------------------------------
# Loading helper
# --------------------------------------------------------------------------
def load_page(path):
    with open(path, "rb") as f:
        return parse_page(f.read())


def is_texture(path):
    return path.lower().endswith(TEXTURE_EXTS)


def iter_textures(root):
    if os.path.isfile(root):
        yield root
        return
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames.sort()
        for fn in sorted(filenames):
            if is_texture(fn):
                yield os.path.join(dirpath, fn)


# --------------------------------------------------------------------------
# CLI: --info
# --------------------------------------------------------------------------
def cmd_info(path):
    targets = list(iter_textures(path))
    if not targets:
        print("no .tga/.ptx files under %s" % path)
        return 1
    pages = surfs = images = bad = 0
    for p in targets:
        try:
            ss = load_page(p)
        except Exception as e:
            bad += 1
            print("FAIL %s : %s" % (p, e))
            continue
        pages += 1
        surfs += len(ss)
        images += sum(1 for s in ss if s.has_image)
        if os.path.isfile(path) or len(targets) < 40:
            print(p)
            for s in ss:
                print("   " + s.describe())
    print("\npages=%d surfaces=%d with-image=%d failed=%d"
          % (pages, surfs, images, bad))
    return 0


# --------------------------------------------------------------------------
# CLI: --png
# --------------------------------------------------------------------------
def cmd_png(src, outdir, crop=True, quiet=False):
    targets = list(iter_textures(src))
    if not targets:
        print("no .tga/.ptx files under %s" % src)
        return 1
    base = src if os.path.isdir(src) else os.path.dirname(os.path.abspath(src))
    ok = skipped = failed = 0
    no_pixels_pages = 0
    for p in targets:
        rel = os.path.relpath(p, base) if os.path.isdir(src) else os.path.basename(p)
        stem = os.path.splitext(rel)[0]
        try:
            surfs = load_page(p)
        except Exception as e:
            failed += 1
            print("FAIL  %s : %s" % (rel, e))
            continue
        if not any(s.has_image for s in surfs):
            no_pixels_pages += 1
        for s in surfs:
            if not s.has_image:
                skipped += 1
                continue
            try:
                rgba, w, h = decode_surface(s, crop=crop)
            except Exception as e:
                failed += 1
                print("FAIL  %s[%d] : %s" % (rel, s.index, e))
                continue
            if len(surfs) == 1:
                name = stem + ".png"
            else:
                safe = "".join(ch if ch.isalnum() or ch in "-_." else "_"
                               for ch in (s.name or ("surf%d" % s.index)))
                name = "%s__%02d_%s.png" % (stem, s.index, safe)
            save_png(os.path.join(outdir, name), rgba, w, h)
            ok += 1
            if not quiet:
                print("%s  ->  %dx%d" % (name, w, h))
    print("\nwritten=%d  no-pixel records skipped=%d  failed=%d  pages with no"
          " pixels at all=%d  out=%s"
          % (ok, skipped, failed, no_pixels_pages, outdir))
    return 0 if failed == 0 else 2


# --------------------------------------------------------------------------
# GUI
# --------------------------------------------------------------------------
def _nearest(rgba, w, h, nw, nh):
    """Nearest-neighbour scale - right for pixel art, and no dependencies."""
    if (nw, nh) == (w, h):
        return rgba
    out = bytearray(nw * nh * 4)
    xs = [min(w - 1, x * w // nw) for x in range(nw)]
    for y in range(nh):
        sy = min(h - 1, y * h // nh)
        srow = sy * w
        drow = y * nw
        for x in range(nw):
            s = (srow + xs[x]) << 2
            d = (drow + x) << 2
            out[d:d + 4] = rgba[s:s + 4]
    return out


def _checker(rgba, w, h, cell=8):
    """Composite over a checkerboard so transparency is visible.

    Row-oriented with a fast path for the fully-opaque and fully-transparent rows
    that dominate real textures - a per-pixel loop over the whole image is far too
    slow to run on every re-render.
    """
    out = bytearray(rgba)
    stride = w * 4
    dark, lite = (58, 58, 62), (86, 86, 92)
    light_row = bytearray(stride)
    dark_row = bytearray(stride)
    for x in range(w):
        bg = lite if ((x // cell) & 1) else dark
        light_row[x * 4:x * 4 + 4] = bytes((bg[0], bg[1], bg[2], 255))
        bg = dark if ((x // cell) & 1) else lite
        dark_row[x * 4:x * 4 + 4] = bytes((bg[0], bg[1], bg[2], 255))
    for y in range(h):
        base = y * stride
        pattern = light_row if not ((y // cell) & 1) else dark_row
        alphas = out[base + 3:base + stride:4]
        if alphas.count(255) == w:
            continue
        if alphas.count(0) == w:
            out[base:base + stride] = pattern
            continue
        for x in range(w):
            o = base + (x << 2)
            a = out[o + 3]
            if a == 255:
                continue
            p = pattern[x * 4:x * 4 + 3]
            if a == 0:
                out[o] = p[0]
                out[o + 1] = p[1]
                out[o + 2] = p[2]
            else:
                ia = 255 - a
                out[o] = (out[o] * a + p[0] * ia) // 255
                out[o + 1] = (out[o + 1] * a + p[1] * ia) // 255
                out[o + 2] = (out[o + 2] * a + p[2] * ia) // 255
            out[o + 3] = 255
    return out


def run_gui(start, crop_default=True):
    import tempfile
    import tkinter as tk
    from tkinter import ttk, filedialog, messagebox

    if start and os.path.isfile(start):
        start_dir = os.path.dirname(os.path.abspath(start))
        want = os.path.abspath(start)
    else:
        start_dir = os.path.abspath(start or ".")
        want = None

    root = tk.Tk()
    root.title("%s %s" % (APP, VERSION))
    root.geometry("1180x760")
    root.minsize(820, 520)

    state = {
        "crop": tk.BooleanVar(value=crop_default),
        "checker": tk.BooleanVar(value=True),
        "zoom": 1.0,
        "fit": True,
        "pan_x": 0.0,
        "pan_y": 0.0,
        "img": None,
        "imgwh": (0, 0),
        "photo": None,
        "surface": None,
        "surfs": [],
        "index": 0,
    }
    tmpdir = None

    def make_photo(png):
        """Hand the PNG to Tk in memory. Only if Tk refuses (older builds) fall
        back to a real file, so a read-only TEMP cannot break the viewer."""
        try:
            return tk.PhotoImage(data=png)
        except Exception:
            nonlocal tmpdir
            if tmpdir is None:
                tmpdir = tempfile.mkdtemp(prefix="pttp_")
            path = os.path.join(tmpdir, "preview.png")
            with open(path, "wb") as f:
                f.write(png)
            return tk.PhotoImage(file=path)

    top = ttk.Frame(root, padding=(6, 6, 6, 0))
    top.pack(fill="x")

    ttk.Label(top, text="Folder:").pack(side="left")
    path_var = tk.StringVar(value=start_dir)
    path_entry = ttk.Entry(top, textvariable=path_var)
    path_entry.pack(side="left", fill="x", expand=True, padx=(4, 4))
    ttk.Button(top, text="Browse...", width=10,
               command=lambda: browse()).pack(side="left")

    opts = ttk.Frame(root, padding=(6, 4, 6, 0))
    opts.pack(fill="x")
    ttk.Checkbutton(opts, text="Crop to drawn window",
                    variable=state["crop"],
                    command=lambda: render_preview(True)).pack(side="left")
    ttk.Checkbutton(opts, text="Checkerboard",
                    variable=state["checker"],
                    command=lambda: render_preview(True)).pack(side="left", padx=(12, 0))
    ttk.Button(opts, text="Fit", width=6,
               command=lambda: fit_view()).pack(side="left", padx=(16, 0))
    ttk.Button(opts, text="1:1", width=6,
               command=lambda: one_to_one()).pack(side="left", padx=(4, 0))
    ttk.Button(opts, text="Save PNG...", width=12,
               command=lambda: save_current()).pack(side="right")
    ttk.Button(opts, text="Convert folder...", width=17,
               command=lambda: convert_folder()).pack(side="right", padx=(0, 6))

    # The status line is packed BEFORE the expanding body: packing order decides
    # who gets the space first, and a bottom-packed label after an `expand=True`
    # pane is squeezed to zero height.
    status = ttk.Label(root, anchor="w", padding=(8, 4), relief="sunken",
                       text="Ready")
    status.pack(fill="x", side="bottom")

    body = ttk.Panedwindow(root, orient="horizontal")
    body.pack(fill="both", expand=True, padx=6, pady=6)

    left = ttk.Frame(body)
    tree = ttk.Treeview(left, show="tree", selectmode="browse")
    tsb = ttk.Scrollbar(left, orient="vertical", command=tree.yview)
    tree.configure(yscrollcommand=tsb.set)
    tsb.pack(side="right", fill="y")
    tree.pack(fill="both", expand=True)
    body.add(left, weight=1)

    mid = ttk.Frame(body)
    files = ttk.Treeview(mid, columns=("dims", "fmt"), show="tree headings",
                         selectmode="browse")
    files.heading("#0", text="File")
    files.heading("dims", text="Size")
    files.heading("fmt", text="Format")
    files.column("#0", width=250, anchor="w")
    files.column("dims", width=90, anchor="w", stretch=False)
    files.column("fmt", width=90, anchor="w", stretch=False)
    fsb = ttk.Scrollbar(mid, orient="vertical", command=files.yview)
    files.configure(yscrollcommand=fsb.set)
    fsb.pack(side="right", fill="y")
    files.pack(fill="both", expand=True)
    body.add(mid, weight=2)

    right = ttk.Frame(body)

    # Record switcher. A character page holds several records - a shared eyeball
    # mask, the skin, the armour - and without this bar the only way to reach the
    # rest was an undocumented PageDown.
    recbar = ttk.Frame(right)
    recbar.pack(fill="x", side="top", pady=(0, 4))
    ttk.Button(recbar, text="\u25c0", width=3,
               command=lambda: step_all(-1)).pack(side="left")
    ttk.Button(recbar, text="\u25b6", width=3,
               command=lambda: step_all(1)).pack(side="left", padx=(2, 6))
    rec_combo = ttk.Combobox(recbar, state="readonly", values=())
    rec_combo.pack(side="left", fill="x", expand=True)
    rec_label = ttk.Label(recbar, text="", padding=(8, 0))
    rec_label.pack(side="left")

    canvas = tk.Canvas(right, background="#202024", highlightthickness=0)
    canvas.pack(fill="both", expand=True)
    body.add(right, weight=4)

    # ---------------- folder tree ----------------
    _dir_cache = {}

    def subdirs_of(p):
        key = p.lower()
        if key in _dir_cache:
            return _dir_cache[key]
        try:
            items = sorted(d for d in os.listdir(p)
                           if not d.startswith(".") and
                           os.path.isdir(os.path.join(p, d)))
        except OSError:
            items = []
        _dir_cache[key] = items
        return items

    def add_dir_node(parent, path):
        node = tree.insert(parent, "end", text=os.path.basename(path) or path,
                           open=False, values=(path,))
        if subdirs_of(path):
            tree.insert(node, "end", text="...")            # lazy placeholder
        return node

    def on_tree_open(ev):
        node = tree.focus()
        kids = tree.get_children(node)
        if len(kids) == 1 and tree.item(kids[0], "text") == "...":
            tree.delete(kids[0])
            full = tree.item(node, "values")[0]
            for d in subdirs_of(full):
                add_dir_node(node, os.path.join(full, d))

    def on_tree_select(ev):
        sel = tree.focus()
        if not sel:
            return
        vals = tree.item(sel, "values")
        if vals:
            path_var.set(vals[0])
            load_folder(vals[0])

    def reveal(path):
        """Expand the tree down to `path` and select it."""
        path = os.path.abspath(path)
        parts = []
        cur = path
        while True:
            parent = os.path.dirname(cur)
            if parent == cur:
                break
            parts.append(cur)
            cur = parent
        parts.reverse()
        parent = ""
        for i, p in enumerate(parts):
            last = i == len(parts) - 1
            hit = None
            for k in tree.get_children(parent):
                if os.path.normcase(tree.item(k, "values")[0]) == os.path.normcase(p):
                    hit = k
                    break
            if hit is None:
                if not subdirs_of(p) and not last:
                    return
                hit = tree.insert(parent, "end", text=os.path.basename(p) or p,
                                  open=False, values=(p,))
                if subdirs_of(p):
                    tree.insert(hit, "end", text="...")
            if last:
                on_tree_open_forced(hit, p)
                tree.selection_set(hit)
                tree.focus(hit)
                tree.see(hit)
                return
            on_tree_open_forced(hit, p)
            parent = hit

    def on_tree_open_forced(node, full):
        kids = tree.get_children(node)
        if len(kids) == 1 and tree.item(kids[0], "text") == "...":
            tree.delete(kids[0])
            for d in subdirs_of(full):
                add_dir_node(node, os.path.join(full, d))
        tree.item(node, open=True)

    tree.bind("<<TreeviewOpen>>", on_tree_open)
    tree.bind("<<TreeviewSelect>>", on_tree_select)

    # ---------------- file list ----------------
    def load_folder(folder):
        files.delete(*files.get_children())
        try:
            names = sorted(os.listdir(folder))
        except OSError as e:
            set_status("cannot read folder: %s" % e)
            return
        n = 0
        for fn in names:
            full = os.path.join(folder, fn)
            if not os.path.isfile(full) or not is_texture(fn):
                continue
            dims = ""
            fmt = ""
            try:
                ss = load_page(full)
                # A page's records are a mix of surfaces that carry pixels and
                # records that only declare a name (a 32-byte record on GameCube, a
                # 0xFFFFFFFF data offset on Xbox), so show the count of viewable
                # ones rather than the record count.
                viewable = [s for s in ss if s.has_image]
                if viewable:
                    s = viewable[0]
                    dims = "%dx%d" % (s.width, s.height)
                    fmt = s.kind.upper()
                    if len(ss) > 1:
                        fmt += "  %d/%d img" % (len(viewable), len(ss))
                elif ss:
                    dims = "%dx%d" % (ss[0].width, ss[0].height)
                    fmt = "no pixels"
                else:
                    fmt = "empty page"
            except Exception as e:
                fmt = "err: %s" % str(e)[:24]
            files.insert("", "end", text=fn, values=(dims, fmt), iid=full)
            n += 1
        set_status("%d texture page(s) in %s" % (n, folder))
        kids = files.get_children()
        if kids:
            files.selection_set(kids[0])
            files.focus(kids[0])

    def viewable_indices():
        return [s.index for s in state["surfs"] if s.has_image]

    def default_record_index(ss, page_path):
        """Pick the record worth showing first.

        Character pages lead with shared masks - `eyeBlue.tga`, `teeth.tga`,
        `skygold_R.rm.tga` - which are the SAME textures in every character page, so
        "first record with pixels" opens an eyeball for nearly every character. Prefer
        a record whose name carries this page's own stem, and among those the largest
        payload (the armour keeps colour AND alpha, so it is the biggest and the most
        distinctive).
        """
        if not ss:
            return None
        view = [s for s in ss if s.has_image]
        if not view:
            return ss[0].index
        stem = os.path.splitext(os.path.basename(page_path))[0].lower()
        tokens = [stem]
        if "_var" in stem:
            tokens.append(stem.split("_var")[0])
        own = [s for s in view
               if any(t and t in (s.name or "").lower() for t in tokens)]
        pool = own or view
        best = max(pool, key=lambda s: (s.data_len, -s.index))
        return best.index

    def refresh_record_bar():
        ss = state["surfs"]
        if not ss:
            rec_combo.configure(values=())
            rec_combo.set("")
            rec_label.configure(text="no records")
            return
        vals = []
        for s in ss:
            vals.append("%d. %-46s %dx%d%s%s" % (
                s.index + 1, (s.name or "(unnamed)")[:46], s.width, s.height,
                "  +alpha" if s.planes >= 2 else "",
                "  [no pixels]" if s.stub else ""))
        rec_combo.configure(values=vals)
        i = state.get("index", 0)
        rec_combo.set(vals[i] if 0 <= i < len(vals) else "")
        n_view = len(viewable_indices())
        rec_label.configure(text=("%d of %d viewable" % (n_view, len(ss))
                                  if n_view != len(ss) else "%d records" % len(ss)))

    def step_all(d):
        ss = state["surfs"]
        if not ss:
            return
        i = max(0, min(len(ss) - 1, state.get("index", 0) + d))
        show_surface(i)

    def on_record_pick(ev=None):
        i = rec_combo.current()
        if i >= 0:
            show_surface(i)

    rec_combo.bind("<<ComboboxSelected>>", on_record_pick)

    def on_file_select(ev=None):
        sel = files.focus()
        if not sel:
            return
        try:
            state["surfs"] = load_page(sel)
        except Exception as e:
            state["surfs"] = []
            state["surface"] = None
            state["img"] = None
            canvas.delete("all")
            canvas.create_text(12, 12, anchor="nw", fill="#ff8080",
                               font=("Consolas", 10),
                               text="Cannot read this page:\n%s" % e)
            set_status("error: %s" % e)
            return
        state["page_path"] = sel
        # Open on the record worth seeing, not merely the first one with pixels:
        # character pages lead with a shared eyeball mask.
        pick = default_record_index(state["surfs"], sel)
        show_surface(pick if pick is not None else 0)

    files.bind("<<TreeviewSelect>>", on_file_select)

    def draw_panel():
        """Redraw the information panel at the canvas's CURRENT width.

        The first draw happens before the canvas has been laid out, so the wrap
        width must be recomputed on resize rather than captured once.
        """
        canvas.delete("all")
        txt = state.get("panel_text")
        if not txt:
            return
        canvas.create_text(16, 16, anchor="nw", fill="#c8c8d0",
                           font=("Consolas", 10),
                           width=max(240, canvas.winfo_width() - 32), text=txt)

    def show_declaration_panel(ss, path, record=None):
        """A record that declares a name but stores no pixels is a real, common case
        (2,797 of 8,654 records, and 302 pages where that is ALL of them) - explain
        it instead of showing it as an error."""
        state["img"] = None
        state["surface"] = None
        others = [x for x in (state.get("surfs") or []) if x.has_image]
        if others and record is not None:
            body = ("Record %d (%s) stores no pixels of its own.\n\n"
                    "It is a 32-byte stub that only declares the name. %d record%s "
                    "on this page do carry pixels - pick one from the switcher above, "
                    "or press PageUp/PageDown to step between them.\n\n"
                    "Declared names: %s"
                    % (record.index, record.name or "(unnamed)", len(others),
                       "" if len(others) == 1 else "s",
                       ", ".join(x.name or "(unnamed)" for x in ss)))
            set_status("record %d (%s) has no pixel data - %d of %d records are viewable"
                       % (record.index, record.name or "?",
                          len(others), len(state["surfs"])))
        elif ss:
            body = ("This page declares %d surface%s but carries NO pixel data at all.\n\n"
                    "It is a declaration/reference page: the images live in another "
                    "page (a character page names its eye/teeth/grid masks this way)."
                    "\n\nDeclared: %s"
                    % (len(ss), "" if len(ss) == 1 else "s",
                       ", ".join(x.name or "(unnamed)" for x in ss)))
            set_status("no pixel data in this page - %s" % os.path.basename(path))
        else:
            try:
                size = os.path.getsize(path)
            except OSError:
                size = 0
            body = ("This PTTP page holds no surfaces at all (an empty %d-byte page)."
                    % size)
            set_status("empty page (no surfaces) - %s" % os.path.basename(path))
        state["panel_text"] = body
        draw_panel()

    def show_surface(i, skip_stubs=False):
        ss = state["surfs"]
        if not ss:
            state["index"] = 0
            refresh_record_bar()
            show_declaration_panel([], state.get("page_path", ""))
            return
        i = max(0, min(i, len(ss) - 1))
        if skip_stubs and not ss[i].has_image:
            view = viewable_indices()
            nxt = [j for j in view if j > i]
            prv = [j for j in view if j < i]
            if nxt:
                i = nxt[0]
            elif prv:
                i = prv[-1]
        state["index"] = i
        s = ss[i]
        state["surface"] = s
        refresh_record_bar()
        if s.stub:
            # Reachable by direct selection only; skip_stubs handles navigation.
            show_declaration_panel([x for x in ss if x.stub],
                                   state.get("page_path", ""), record=s)
            return
        try:
            rgba, w, h = decode_surface(s, crop=state["crop"].get())
        except Exception as e:
            state["img"] = None
            state["panel_text"] = None
            canvas.delete("all")
            canvas.create_text(12, 12, anchor="nw", fill="#ff8080",
                               font=("Consolas", 10),
                               text="Cannot decode record %d:\n%s" % (i, e))
            set_status("error: %s" % e)
            return
        state["img"] = (rgba, w, h)
        state["imgwh"] = (w, h)
        state["panel_text"] = None
        n_view = len(viewable_indices())
        if len(ss) > 1:
            info = "record %d of %d%s | %s" % (
                i + 1, len(ss),
                "" if n_view == len(ss) else " (%d with pixels)" % n_view,
                s.describe(show_index=False))
        else:
            info = s.describe()
        set_status("%s   [%s]" % (info, os.path.basename(state.get("page_path", ""))))
        fit_view()

    # ---------------- preview ----------------
    def fit_view():
        state["fit"] = True
        state["pan_x"] = state["pan_y"] = 0.0
        render_preview(True)

    def one_to_one():
        state["fit"] = False
        state["zoom"] = 1.0
        state["pan_x"] = state["pan_y"] = 0.0
        render_preview(True)

    def render_preview(reload=True):
        img = state["img"]
        if img is None:
            draw_panel()            # keep the explanation wrapped to the new width
            return
        rgba, w, h = img
        cw = max(64, canvas.winfo_width())
        chh = max(64, canvas.winfo_height())
        if state["fit"]:
            scale = min(cw / float(w), chh / float(h), 8.0)
        else:
            scale = state["zoom"]
        scale = max(scale, 0.01)
        MAXPX = 1_600_000
        if w * scale * h * scale > MAXPX:   # keep a re-render interactive
            scale = (MAXPX / float(w * h)) ** 0.5
        nw = max(1, int(round(w * scale)))
        nh = max(1, int(round(h * scale)))
        small = _nearest(rgba, w, h, nw, nh)
        if state["checker"].get():
            small = _checker(small, nw, nh)
        photo = make_photo(png_bytes(small, nw, nh))
        state["photo"] = photo
        canvas.delete("all")
        x = int(cw / 2 - nw / 2 + state["pan_x"])
        y = int(chh / 2 - nh / 2 + state["pan_y"])
        canvas.create_image(x, y, image=photo, anchor="nw")
        canvas.create_rectangle(x - 1, y - 1, x + nw, y + nh,
                                outline="#55555c")

    def on_resize(ev):
        render_preview(False)

    def on_wheel(ev):
        if state["img"] is None:
            return
        state["fit"] = False
        factor = 1.25 if ev.delta > 0 else 0.8
        state["zoom"] = max(0.05, min(32.0, state["zoom"] * factor))
        render_preview(True)

    def on_drag(ev):
        if state["img"] is None:
            return
        state["fit"] = False
        dx = ev.x - state.get("_lx", ev.x)
        dy = ev.y - state.get("_ly", ev.y)
        state["_lx"], state["_ly"] = ev.x, ev.y
        state["pan_x"] += dx
        state["pan_y"] += dy
        canvas.move("all", dx, dy)          # pan without re-decoding

    canvas.bind("<Configure>", on_resize)
    canvas.bind("<MouseWheel>", on_wheel)
    canvas.bind("<ButtonPress-1>", lambda e: state.update(_lx=e.x, _ly=e.y))
    canvas.bind("<B1-Motion>", on_drag)

    # ---------------- actions ----------------
    def set_status(t):
        status.configure(text=t)

    def browse():
        d = filedialog.askdirectory(initialdir=path_var.get() or start_dir,
                                    title="Choose a texture folder")
        if d:
            path_var.set(d)
            load_folder(d)
            try:
                reveal(d)
            except Exception:
                pass

    def save_current():
        s = state["surface"]
        if s is None or state["img"] is None:
            return
        default = os.path.splitext(os.path.basename(state.get("page_path", "image")))[0]
        ss = state["surfs"]
        if len(ss) > 1:
            default += "__%02d" % s.index
        out = filedialog.asksaveasfilename(
            defaultextension=".png", initialfile=default + ".png",
            filetypes=[("PNG image", "*.png"), ("All files", "*.*")],
            title="Save decoded texture as PNG")
        if not out:
            return
        try:
            rgba, w, h = decode_surface(s, crop=state["crop"].get())
            save_png(out, rgba, w, h)
            set_status("saved %s (%dx%d)" % (out, w, h))
        except Exception as e:
            messagebox.showerror(APP, "Could not save:\n%s" % e)

    def convert_folder():
        d = filedialog.askdirectory(initialdir=path_var.get() or start_dir,
                                    title="Choose the folder to convert")
        if not d:
            return
        out = filedialog.askdirectory(title="Where to write the PNGs")
        if not out:
            return
        set_status("converting %s ..." % d)
        root.update()
        try:
            rc = cmd_png(d, out, crop=state["crop"].get(), quiet=True)
        except Exception as e:
            messagebox.showerror(APP, "Conversion failed:\n%s" % e)
            return
        set_status("done - %s" % out)
        messagebox.showinfo(APP, "Conversion finished.\n\nOutput: %s" % out)

    def refresh():
        load_folder(path_var.get())

    path_entry.bind("<Return>", lambda e: refresh())
    root.bind("<F5>", lambda e: refresh())
    root.bind("<Escape>", lambda e: root.destroy())

    # keyboard: page through the records that actually have pixels
    def next_record(d):
        view = viewable_indices()
        if len(view) < 2:
            return
        cur = state.get("index", view[0])
        try:
            pos = view.index(cur)
        except ValueError:
            pos = 0
        pos = max(0, min(len(view) - 1, pos + d))
        show_surface(view[pos])

    root.bind("<Next>", lambda e: next_record(1))
    root.bind("<Prior>", lambda e: next_record(-1))

    def step_file(d):
        kids = list(files.get_children())
        cur = files.focus()
        if not kids:
            return "break"
        i = kids.index(cur) if cur in kids else 0
        i = max(0, min(len(kids) - 1, i + d))
        files.selection_set(kids[i])
        files.focus(kids[i])
        files.see(kids[i])
        return "break"

    files.bind("<Down>", lambda e: step_file(1))
    files.bind("<Up>", lambda e: step_file(-1))

    # ---------------- boot ----------------
    try:
        reveal(start_dir)
    except Exception:
        pass
    load_folder(start_dir)
    if want:
        if files.exists(want):
            files.selection_set(want)
            files.focus(want)
            files.see(want)
            on_file_select()
    else:
        on_file_select()

    root.after(60, lambda: render_preview(True))
    root.mainloop()
    if tmpdir:
        try:
            for fn in os.listdir(tmpdir):
                os.remove(os.path.join(tmpdir, fn))
            os.rmdir(tmpdir)
        except OSError:
            pass
    return 0


# --------------------------------------------------------------------------
def usage():
    print(__doc__.strip())
    print()
    print("usage:")
    print("  py %s [folder-or-file]        browse (GUI)" % os.path.basename(sys.argv[0]))
    print("  py %s --info <folder-or-file>  print the surface table" % os.path.basename(sys.argv[0]))
    print("  py %s --png <folder-or-file> <outdir> [--stored]"
          % os.path.basename(sys.argv[0]))
    print()
    print("  --stored   keep the full power-of-two storage size instead of")
    print("             cropping to the window the engine draws")


def main(argv):
    args = argv[1:]
    if args and args[0] in ("-h", "--help", "/?"):
        usage()
        return 0
    if args and args[0] == "--info":
        if len(args) < 2:
            usage()
            return 1
        return cmd_info(args[1])
    if args and args[0] == "--png":
        rest = [a for a in args[1:] if not a.startswith("--")]
        crop = "--stored" not in args
        if len(rest) < 2:
            usage()
            return 1
        return cmd_png(rest[0], rest[1], crop=crop)
    if args and args[0].startswith("--"):
        usage()
        return 1
    return run_gui(args[0] if args else ".")


if __name__ == "__main__":
    sys.exit(main(sys.argv))
