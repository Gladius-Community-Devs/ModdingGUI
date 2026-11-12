# -*- coding: utf-8 -*-
"""PTTP texture pages -> PNG, for the Grid Builder map overlay.

Ported from the standalone ``pttp_tga.py`` (which also carries a Tkinter viewer); only decoding and batch conversion are
kept here, so the bundled exe can produce the map PNGs without a Python install. The format notes are the original ones,
because they are the reason the decoder looks the way it does.

Every file in ``data/texture/`` named ``.tga`` (and ``.ptx``) is NOT a Targa file. It is a ``PTTP`` texture page: a chunk
container holding one or more CMPR (GameCube DXT1) surfaces. That is why no image viewer will open them.

    chunk  +0x00  4   tag ("PTTP" "NMPT" "PFHD" "PADD" "PTDT" "END.")
    chunk  +0x04  u32 declared size of the chunk, including its 16-byte header

    record i sits at PFHD + 0x10 + i*32, and the fields are
    record +0x01  u8   format: 0x20 = CMPR colour, 0x22 = CMPR colour + alpha mask
    record +0x02  u16  mip level count (0 in every single-image .tga here)
    record +0x08  u16  width  - THE DRAWN WINDOW, not the stored width
    record +0x0A  u16  height - THE DRAWN WINDOW, not the stored height
    record +0x0C  u32  byte length of this surface data
    record +0x10  u32  offset to that data, relative to the RECORD own address
    record +0x14  u32  name offset, relative to the NMPT chunk payload

Two rules that decide whether the picture is right or a staircase of noise:

  * the image is STORED at the next power of two in each axis, and the declared
    width/height is only the window the engine draws out of it. Decoding at the
    declared width shifts every 8x8 block row after the first - the "staircase".
  * CMPR is 4 bits per texel in 8x8 blocks of 32 bytes: four 4x4 DXT1 sub-blocks,
    left to right then top to bottom. A 0x22 surface is TWO such planes, and the
    second plane GREEN channel is the alpha mask.
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
def convert(src, outdir, crop=True, quiet=False):
    """Write a PNG per surface under ``outdir``; returns a summary dict for the CLI and the app log.

    Renamed from ``cmd_png``, and it returns counts instead of only printing them.
    """
    targets = list(iter_textures(src))
    if not targets:
        print("no .tga/.ptx files under %s" % src)
        return {"written": 0, "skipped": 0, "failed": 0, "pages": 0, "out": outdir}
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
    return {"written": ok, "skipped": skipped, "failed": failed, "pages": no_pixels_pages, "out": outdir}

