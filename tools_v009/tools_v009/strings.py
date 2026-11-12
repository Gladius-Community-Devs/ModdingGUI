# -*- coding: utf-8 -*-
"""``lookuptext_<lang>.txt`` <-> ``lookuptext_<lang>.bin``.

The text side is the human-editable form::

    ##8220
    // id^speak^listen^localized text
    1^Either^Either^Urlan is the future king ...
    2^Either^Either^Welcome to the shining jewel ...

The binary side is a flat string table::

    u16  entry_count            (highest id + 1)
    u16  0
    3x8  file_size
    3x8  end_of_address_table
    N x (3x8 offset + u8 0)     per-string offsets
    u8   0
    ...   null-terminated strings (windows-1252)

``\\r\\n`` in the text is stored as a real newline in the binary.
"""

import os
import re
import sys


class StringsError(Exception):
    """Raised when a ``lookuptext_<lang>.txt`` file cannot be converted to ``.bin``.

    Carries a user-facing message naming the offending line, so a bad edit
    surfaces as a clear error instead of a mangled binary.
    """


def _read_txt(txt_path):
    with open(txt_path, "r", encoding="ansi") as f:
        return f.readlines()


def _iter_entries(txt_path):
    """Yield ``(id, text)`` pairs from a lookuptext text file.

    Accepts both the verbose export (a ``##N`` header line, a ``//`` comment,
    and ``id^speak^listen^text`` entries) and the minimal ``id^text`` form with
    no header (the ``speak``/``listen`` fields are optional). The localized text
    is always the field after the last ``^``.
    """
    lines = _read_txt(txt_path)

    start = 2 if (lines and lines[0].lstrip().startswith("##")) else 0

    for idx, raw in enumerate(lines[start:], start=start):
        line = raw.rstrip("\r\n")
        if not line.strip():
            continue
        stripped = line.lstrip()
        if stripped.startswith("//") or stripped.startswith("#"):
            continue

        match = re.match(r"^(\d+)\^(.*)$", line)
        if not match:
            raise StringsError(
                "%s: line %d doesn't look like a lookuptext entry (expected "
                "\"id^text\"): %r. Did you insert a raw newline inside an entry?"
                % (txt_path, idx + 1, line[:80]))

        eid = int(match.group(1))
        text = match.group(2)
        if "^" in text:
            text = text.rsplit("^", 1)[1]
        yield eid, text


def _occupied_ids(bin_path):
    """Return the set of ids that hold non-empty text in an existing ``.bin``.

    Used to tell a genuine "the text for entry N has disappeared" apart from the
    unused holes the vanilla table has always had. Returns an empty set when the
    file is missing, empty or unreadable, so a warning is never raised from a
    baseline we do not trust.
    """
    try:
        with open(bin_path, "rb") as f:
            data = f.read()
    except OSError:
        return set()

    if len(data) < 12:
        return set()

    # The writer stores ``highest id + 1`` as the count but only writes one
    # offset per stored string, so the address table holds ``count - 1`` entries.
    entry_count = int.from_bytes(data[0:2], "little")
    count = entry_count - 1
    if count <= 0 or 12 + count * 4 > len(data):
        return set()

    offsets = [int.from_bytes(data[12 + i * 4:12 + i * 4 + 3], "little")
               for i in range(count)]

    occupied = set()
    for i, off in enumerate(offsets):
        end = offsets[i + 1] if i + 1 < count else len(data)
        if off >= len(data) or end <= off:
            continue
        if data[off:end].rstrip(b"\x00").strip():
            occupied.add(i + 1)
    return occupied


def txt_to_bin(txt_path, bin_path):
    """Convert a lookuptext text file into its binary form."""
    entries = {}
    for eid, text in _iter_entries(txt_path):
        if eid in entries:
            raise StringsError(
                "%s: duplicate id %d. Every lookuptext entry must have a unique "
                "id." % (txt_path, eid))
        entries[eid] = text

    if not entries:
        raise StringsError("%s: no lookuptext entries found." % txt_path)

    highestNumber = max(entries)

    linesFinal = []
    for i in range(highestNumber):
        text = entries.get(i + 1, "")
        linesFinal.append(text.replace(r"\r\n", "\n"))

    # Only report entries that actually LOST text. The vanilla table has ~64 holes
    # that have always been empty (they are empty in the shipped game too), so
    # warning about every hole would fire on an untouched project and teach people
    # to ignore the log. Compare against the .bin we are about to replace: a slot
    # that held text there and is empty now means a line was deleted, renumbered or
    # had its text cleared by accident. A missing baseline means no warning.
    before = _occupied_ids(bin_path)
    lost = sorted(i + 1 for i in range(highestNumber)
                  if not linesFinal[i] and (i + 1) in before)
    if lost:
        shown = ", ".join(str(x) for x in lost[:10])
        if len(lost) > 10:
            shown += ", ..."
        print("WARNING: %s: %d entr%s that had text in the existing %s %s now "
              "empty (e.g. %s). Check whether a line was deleted or renumbered."
              % (txt_path, len(lost), "y" if len(lost) == 1 else "ies",
                 os.path.basename(bin_path),
                 "is" if len(lost) == 1 else "are", shown), file=sys.stderr)

    fileSize = 13 + (highestNumber * 4)
    addressesEndOffset = fileSize - 1

    with open(bin_path, "wb") as f:
        f.write((highestNumber + 1).to_bytes(2, byteorder="little"))
        f.write(b"\x00\x00")

        i = 0
        while i < highestNumber:
            fileSize = fileSize + len(linesFinal[i]) + 1
            i += 1

        f.write(fileSize.to_bytes(3, byteorder="little"))
        f.write(b"\x00")

        f.write(addressesEndOffset.to_bytes(3, byteorder="little"))
        f.write(b"\x00")

        i = 0
        offset = addressesEndOffset + 1
        while i < highestNumber:
            f.write(offset.to_bytes(3, byteorder="little"))
            f.write(b"\x00")
            offset += len(linesFinal[i]) + 1
            i += 1
        f.write(b"\x00")

        i = 0
        while i < highestNumber:
            f.write(linesFinal[i].encode("ansi"))
            f.write(b"\x00")
            i += 1


def bin_to_txt(bin_path, txt_path):
    """Convert a lookuptext binary file back into its text form."""
    with open(bin_path, "rb") as f:
        data = f.read()

    entry_count = int.from_bytes(data[0:2], "little")
    addresses_end = int.from_bytes(data[8:11], "little")

    offsets = []
    pos = 12
    for i in range(entry_count - 1):
        offsets.append(int.from_bytes(data[pos:pos + 3], "little"))
        pos += 4

    strings = []
    for off in offsets:
        end = data.index(b"\x00", off)
        strings.append(data[off:end].decode("ansi").replace("\n", r"\r\n"))

    lines = ["##%d\n" % (len(strings)), "// id^speak^listen^localized text\n"]
    for i, s in enumerate(strings):
        if s:
            lines.append("%d^Either^Either^%s\n" % (i + 1, s))
        else:
            lines.append("%d^Either^Either^\n" % (i + 1))

    os.makedirs(os.path.dirname(txt_path), exist_ok=True) if os.path.dirname(txt_path) else None
    with open(txt_path, "w", encoding="ansi", newline="\r\n") as f:
        f.writelines(lines)
