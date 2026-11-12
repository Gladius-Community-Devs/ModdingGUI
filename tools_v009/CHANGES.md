# tools_v009 — v008 with the pack checksum fixed

`tools_v009` is `tools_v008` with **one** change, in `bec.py`:

```python
# v008: a per-byte CRC-32 loop in Python - the only real hot spot in a pack
def computeDataChecksum(data):
    hashVal = 0
    for byte in data:
        lookupKey = (hashVal ^ byte) & 0xff
        hashVal = CRCTable[lookupKey] ^ (hashVal >> 8)
    return hashVal

# v009: the same CRC without the usual pre/post inversion, in zlib's C implementation
def computeDataChecksum(data):
    return zlib.crc32(data, 0xFFFFFFFF) ^ 0xFFFFFFFF
```

That is exactly equivalent — verified below on empty, 1-byte, 11-byte, 256-byte, 1000-byte and text samples, and on a
real archive (byte-identical md5).

## Why

Profiling `tools_v008 bec pack` on the demo project (27,298 entries, 456 MB):

| Function | tottime | calls |
|---|---|---|
| **`bec.py:100 computeDataChecksum`** | **92.1 s** | 24,453 |
| `bec.py:441 createBecArchive` (everything else) | 4.4 s | 1 |
| `bec.py:87 computeFileHash` (path hashes) | 0.6 s | 27,298 |

So ~89% of a v008 pack was one pure-Python loop, at ~5 MB/s. That is why v008 felt *slower* than v007 even though v008
removed the per-file `flush()`: the flush penalty only shows on a cold disk, while the checksum cost is paid every time.
The thread count is not involved — `--threads 1` and the default were within noise, and the exe and the source take the
same time, so the PyInstaller bundle is not the cause either.

## Measured, same project, same machine, warm cache

| Tool | bec pack | Output |
|---|---|---|
| v007 (`bec-tool-all.py`, shipped in ModdingGUI) | 8.4 s | 431 MB |
| v008 (`tools_v008.exe` and `python tools_v008.py` agree) | 51–68 s | 430 MB |
| **v009 (`python tools_v009.py`)** | **4.8 s** | 430 MB, md5 `c6072bdf…` = v008's md5 |

v009 is **10× faster than v008** and **1.7× faster than v007**, with byte-identical output to v008.

## Verifying it yourself

```
python verify_vs_v008.py <project>            # packs with both and compares sha256 + size
```

The script returns 0 when the two archives are identical, and prints both hashes otherwise.

## Using it

The CLI is v008's, unchanged:

```
python tools_v009.py bec pack   <modded_BEC>/ out.bec <modded_BEC>/filelist.txt --platform GC [--threads N]
python tools_v009.py bec unpack in.bec out_dir/ --platform GC
python tools_v009.py idx repack <data>/        # after editing the 6 data/units/*.txt files
python tools_v009.py iso pack   <ISO>/ fst.bin filelist.txt out.iso
```

Everything else in `tools_v008/README.txt` still applies: `repack-filelist.txt` is the manifest, the six generated
`data/units/*.txt` files are skipped by the pack, deleted files are warned about and skipped, and `hashes.txt` is
human-editable.

## Still open

- `bec.py:87 computeFileHash` is a second per-byte loop, but only over path strings (0.6 s for 27k files). Leaf.
- The remaining 4.8 s is file reads through a thread pool plus the archive write; the next structural win is an
  incremental repack (reuse unchanged entries from the existing `.bec`), not a faster CRC.
- The v007-compatible drop-in scripts (`bec-tool-all.py`, `Gladiator_Units_IDX_Repack.py`, …) are **not** in the v008/v009
  package, so switching ModdingGUI's toolbelt over needs either those shims or step commands that call the v009 CLI.

## v009.2 - `maps` subcommand, so map PNGs need no Python

`tools_v009.exe maps png <texture dir> <out dir>` ports the decoder from the standalone `pttp_tga.py` (which also carries a
Tkinter viewer) into `maps.py`. The app used to run that script for the Grid Builder's arena-map overlay, which was the last
Python dependency in the unpack path.

Verified byte-identical to the original converter: 49 PNGs from `data/texture/gui/maps` (demo project), all matching
`pttp_tga.py --png` by MD5, through both `python tools_v009.py maps png` and `tools_v009.exe maps png`.

Also folded in here:

- `iso unpack` treats an absolute `filelist` argument as absolute. v007 and v008 prefixed it with the output folder
  unconditionally, producing `<outdir>/C:` and an `OSError` - every file was written and the step still exited 1.
- `hashes.txt` is also looked for beside the modules, not only in the package's parent. Without that, a package copy
  resolves every path to `unknown-<tag>-0x...` instead of `data/units/unitschools.idx`.