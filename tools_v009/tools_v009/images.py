"""Write a modern image into the texture format the game actually reads.

Every file in ``data/texture/`` named ``.tga`` is a ``PTTP`` texture page, not a Targa file, and the arena league cards are
``fmt = 0x22``: GameCube ``CMPR`` (DXT1) with a second, equal-sized plane whose green channel is the alpha mask. So importing a
picture means *encoding* DXT1 twice and patching both planes into a page that already has the right chunk tables.

That is what this module does. It is deliberately small and free of image libraries: callers hand it raw RGBA, and the shell
(apps) is where a PNG or JPEG gets decoded. Patching a vanilla page as the template keeps every chunk header, the PFHD record
and the page's own name intact - only the 65,536 bytes of pixels change, so the file keeps its size and structure and the game
walks exactly the tables it shipped with.
"""

import struct


def _rgb565(r, g, b):
    return ((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3)


def _unpack565(c):
    r = (c >> 11) & 0x1F
    g = (c >> 5) & 0x3F
    b = c & 0x1F
    return ((r << 3) | (r >> 2), (g << 2) | (g >> 4), (b << 3) | (b >> 2))


def _luma(p):
    # Rec.601 integer weights, enough to pick the extreme colours of a block
    return (p[0] * 299 + p[1] * 587 + p[2] * 114) // 1000


def _encode_block(px):
    """DXT1 for one 4x4 block given as 16 (r, g, b) tuples, in row-major order.

    Two endpoints from the block's darkest and brightest texel, four interpolated colours, 2-bit indices. Endpoints are ordered
    so ``c0 > c1``, which selects the four-colour mode: three-colour mode would leave one index meaning transparent, and the
    alpha lives in the second plane here.
    """
    lo = min(px, key=_luma)
    hi = max(px, key=_luma)
    c0 = _rgb565(*hi)
    c1 = _rgb565(*lo)
    if c0 == c1:
        # a flat block: one endpoint repeated, all indices zero
        return struct.pack(">HHI", c0, c0, 0)
    if c0 < c1:
        c0, c1 = c1, c0

    (r0, g0, b0) = _unpack565(c0)
    (r1, g1, b1) = _unpack565(c1)
    palette = [
        (r0, g0, b0),
        (r1, g1, b1),
        ((2 * r0 + r1) // 3, (2 * g0 + g1) // 3, (2 * b0 + b1) // 3),
        ((r0 + 2 * r1) // 3, (g0 + 2 * g1) // 3, (b0 + 2 * b1) // 3),
    ]

    bits = 0
    for i, p in enumerate(px):
        best = 0
        bestd = 1 << 30
        for k, q in enumerate(palette):
            d = (p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 + (p[2] - q[2]) ** 2
            if d < bestd:
                bestd = d
                best = k
        bits |= best << (2 * i)

    return struct.pack(">HHI", c0, c1, bits)


def to_cmpr(rgba, width, height):
    """Encode RGBA bytes as a GameCube CMPR plane (8x8 blocks of four 4x4 sub-blocks).

    The blocks are big-endian: maps.py documents "8x8 blocks of 32 bytes, big-endian" for the GameCube ids, and encoding them
    little-endian produces colours scrambled through the wrong byte order.
    """
    stride = width * 4
    out = bytearray()
    for by in range(0, height, 8):
        for bx in range(0, width, 8):
            for sub in range(4):
                ox = bx + (sub % 2) * 4
                oy = by + (sub // 2) * 4

                px = []
                for y in range(oy, oy + 4):
                    for x in range(ox, ox + 4):
                        i = y * stride + x * 4
                        px.append((rgba[i], rgba[i + 1], rgba[i + 2]))
                out += _encode_block(px)
    return bytes(out)


def alpha_plane(rgba, width, height):
    """The mask plane: a CMPR image whose green channel carries alpha, since that is where the engine reads it."""
    grey = bytearray(len(rgba))
    for i in range(0, len(rgba), 4):
        a = rgba[i + 3]
        grey[i] = a
        grey[i + 1] = a
        grey[i + 2] = a
        grey[i + 3] = 255
    return to_cmpr(grey, width, height)


def card_from_rgba(rgba, width, height, template):
    """Patch a full 256x256 RGBA image into a vanilla league card page.

    ``template`` must be an existing page of the same geometry (the vanilla cards are all 256x256 ``fmt 0x22``). The two
    encoded planes replace the page's pixel bytes in place, so the chunk tables and the PFHD record are untouched.
    """
    from . import maps

    surface = maps.parse_page(template)[0]
    if surface.fmt != 0x22:
        raise ValueError("template is fmt 0x%02X; this writer only produces 0x22 (CMPR + alpha)" % surface.fmt)
    if (surface.stored_w, surface.stored_h) != (width, height):
        raise ValueError("template is %dx%d but the image is %dx%d"
                         % (surface.stored_w, surface.stored_h, width, height))

    colour = to_cmpr(rgba, width, height)
    mask = alpha_plane(rgba, width, height)
    payload = colour + mask
    need = surface.data_len
    if len(payload) != need:
        raise ValueError("encoded %d bytes but the page holds %d" % (len(payload), need))

    patched = bytearray(template)
    patched[surface.data_off:surface.data_off + need] = payload
    return bytes(patched)
