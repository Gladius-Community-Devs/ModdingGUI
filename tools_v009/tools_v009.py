# -*- coding: utf-8 -*-
"""Gladius tools - unified command-line interface.

Every operation in the toolchain is available as a subcommand::

    python tools_v009.py bec unpack gladius.bec out_dir/ --platform GC
    python tools_v009.py bec pack   out_dir/ gladius.bec out_dir/filelist.txt --platform GC
    python tools_v009.py idx unpack data/
    python tools_v009.py idx repack data/
    python tools_v009.py tok compress skills.tok skills_strings.bin skills_lines.bin skills.tok.brf
    python tools_v009.py tok decompress skills_strings.bin skills_lines.bin skills.tok.brf skills.tok
    python tools_v009.py toknum data/config/
    python tools_v009.py strings data/config/
    python tools_v009.py iso unpack gladius.iso iso_dir/ filelist.txt
    python tools_v009.py iso pack   iso_dir/ fst.bin filelist.txt gladius.iso

The same CLI is what the orchestrator ``.bat`` files drive (via ``tools_v009.exe``
when available, otherwise ``python tools_v009.py``).
"""

import argparse
import os
import sys

from tools_v009 import bec, idx, tok, strings, iso

DEFAULT_LANGS = ["eng"]


def _add_platform(p):
    p.add_argument("--platform", default="GC", const="GC", nargs="?",
                   choices=["GC", "PS2", "XBOX"], help="target platform (default: GC)")


def _add_threads(p):
    p.add_argument("--threads", type=int, default=None, metavar="N",
                   help="worker thread count (default: CPU count)")


def cmd_bec_unpack(args):
    bec.unpackBecArchive(args.input, args.output, demobec=args.demobec,
                         platform=args.platform, num_threads=args.threads)


def cmd_bec_pack(args):
    bec.createBecArchive(args.input, args.output, args.filelist, demobec=args.demobec,
                         platform=args.platform, num_threads=args.threads)


def cmd_bec_add(args):
    bec.add_file_to_bec(args.input, args.hostfile, args.becpath, hash_value=args.hash)


def cmd_bec_refresh(args):
    bec.refresh_filelist(args.input)


def cmd_idx_unpack(args):
    idx.unpack(args.datafolder)


def cmd_idx_repack(args):
    idx.repack(args.datafolder)


def cmd_tok_compress(args):
    tok.compress_tok(args.strings, args.lines, args.brf, args.input)


def cmd_tok_decompress(args):
    tok.create_tok(args.strings, args.lines, args.brf, args.output)


def cmd_toknum(args):
    tok.update_tok_numentries(args.configdir)


def cmd_strings(args):
    config_dir = args.configdir
    if not config_dir.endswith(("/", "\\")):
        config_dir += "/"
    langs = args.lang or DEFAULT_LANGS
    for lang in langs:
        txt = config_dir + "lookuptext_" + lang + ".txt"
        if os.path.isfile(txt):
            strings.txt_to_bin(txt, config_dir + "lookuptext_" + lang + ".bin")


def cmd_iso_unpack(args):
    iso.unpack_iso(args.input, args.output, args.filelist)


def cmd_iso_pack(args):
    print(iso.pack_iso(args.input, args.output, args.fst, args.filelist))


def build_parser():
    parser = argparse.ArgumentParser(prog="tools_v009", description=__doc__.splitlines()[0])
    sub = parser.add_subparsers(dest="command", required=True)

    p = sub.add_parser("bec", help="BEC archive operations")
    bec_sub = p.add_subparsers(dest="bec_command", required=True)
    p_u = bec_sub.add_parser("unpack", help="unpack a .bec archive")
    p_u.add_argument("input")
    p_u.add_argument("output")
    p_u.add_argument("--demobec", action="store_true")
    _add_platform(p_u)
    _add_threads(p_u)
    p_u.set_defaults(func=cmd_bec_unpack)

    p_p = bec_sub.add_parser("pack", help="pack a folder into a .bec archive")
    p_p.add_argument("input")
    p_p.add_argument("output")
    p_p.add_argument("filelist")
    p_p.add_argument("--demobec", action="store_true")
    _add_platform(p_p)
    _add_threads(p_p)
    p_p.set_defaults(func=cmd_bec_pack)

    p_a = bec_sub.add_parser("add", help="copy a file into a BEC folder and record it in the mod manifest")
    p_a.add_argument("input", help="BEC folder (the unpacked mod folder)")
    p_a.add_argument("hostfile", help="the file on disk to copy in")
    p_a.add_argument("becpath", help="path inside the BEC archive, e.g. data/mymod/mymod.bin")
    p_a.add_argument("--hash", type=lambda v: int(v, 0), default=None,
                     help="explicit CRC-32 path hash (default: computed from becpath)")
    p_a.set_defaults(func=cmd_bec_add)

    p_r = bec_sub.add_parser("refresh-filelist", help="recompute the mod manifest without packing")
    p_r.add_argument("input", help="BEC folder (the unpacked mod folder)")
    p_r.set_defaults(func=cmd_bec_refresh)

    p = sub.add_parser("idx", help="unit .idx table operations")
    idx_sub = p.add_subparsers(dest="idx_command", required=True)
    p_u = idx_sub.add_parser("unpack", help="unpack .idx files into .txt")
    p_u.add_argument("datafolder")
    p_u.set_defaults(func=cmd_idx_unpack)
    p_p = idx_sub.add_parser("repack", help="repack .txt files into .idx")
    p_p.add_argument("datafolder")
    p_p.set_defaults(func=cmd_idx_repack)

    p = sub.add_parser("tok", help="skills.tok operations")
    tok_sub = p.add_subparsers(dest="tok_command", required=True)
    p_c = tok_sub.add_parser("compress", help="compress .tok text into the 3 binary parts")
    p_c.add_argument("input")
    p_c.add_argument("strings")
    p_c.add_argument("lines")
    p_c.add_argument("brf")
    p_c.set_defaults(func=cmd_tok_compress)
    p_x = tok_sub.add_parser("decompress", help="decompress the 3 binary parts back into .tok text")
    p_x.add_argument("strings")
    p_x.add_argument("lines")
    p_x.add_argument("brf")
    p_x.add_argument("output")
    p_x.set_defaults(func=cmd_tok_decompress)

    p = sub.add_parser("toknum", help="update NUMENTRIES in skills.tok/items.tok")
    p.add_argument("configdir")
    p.set_defaults(func=cmd_toknum)

    p = sub.add_parser("strings", help="rebuild lookuptext_<lang>.bin from .txt")
    p.add_argument("configdir")
    p.add_argument("lang", nargs="*", help="languages to rebuild (default: eng)")
    p.set_defaults(func=cmd_strings)

    p = sub.add_parser("iso", help="GameCube ISO operations")
    iso_sub = p.add_subparsers(dest="iso_command", required=True)
    p_u = iso_sub.add_parser("unpack", help="unpack a GameCube ISO")
    p_u.add_argument("input")
    p_u.add_argument("output")
    p_u.add_argument("filelist")
    p_u.set_defaults(func=cmd_iso_unpack)
    p_p = iso_sub.add_parser("pack", help="pack a folder tree into a GameCube ISO")
    p_p.add_argument("input")
    p_p.add_argument("fst")
    p_p.add_argument("filelist")
    p_p.add_argument("output")
    p_p.set_defaults(func=cmd_iso_pack)

    return parser


def main(argv=None):
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        args.func(args)
    except idx.IDXDataError as exc:
        print("ERROR: " + str(exc), file=sys.stderr)
        print("The .idx repack did not write any files.", file=sys.stderr)
        return 1
    except strings.StringsError as exc:
        print("ERROR: " + str(exc), file=sys.stderr)
        print("No .bin file was written.", file=sys.stderr)
        return 1
    except (FileNotFoundError, PermissionError) as exc:
        print("ERROR: " + str(exc), file=sys.stderr)
        return 1
    except (ValueError, IndexError, KeyError, TypeError) as exc:
        print("ERROR: invalid data: " + str(exc), file=sys.stderr)
        return 1
    except Exception as exc:
        print("ERROR: unexpected failure (%s): %s" % (type(exc).__name__, exc),
              file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
