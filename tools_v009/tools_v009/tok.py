# -*- coding: utf-8 -*-
"""``skills.tok`` compress/decompress and ``NUMENTRIES`` maintenance.

The ``.tok`` text format is stored on disc as three pieces:

* ``<name>_strings.bin`` — a deduplicated dictionary of every word (3-byte
  little-endian offset table followed by the null-terminated string data).
* ``<name>_lines.bin`` — deduplicated token sequences (4-byte offset/wordcount
  table followed by 1- or 2-byte word indices).
* ``<name>.tok.brf`` — the body: a stream of 1- or 2-byte line indices.

``NUMENTRIES`` (and ``NUMCLASSDEFS``) headers are regenerated from the actual
create-record count rather than hardcoded line indices.
"""

import os
import struct
import operator


def _read_string(file, offset):
    file.seek(offset)
    string = b""
    while True:
        byte = file.read(1)
        if not byte:
            break
        value = byte[0]
        if value >= 128:
            string += bytes([value - 128])
            break
        string += byte
    return string.decode("utf-8")


def create_tok(stringsfilepath, linesfilepath, mainfilepath, outfilepath):
    """Decompress the three ``.tok`` parts into a text file."""
    stringsfile = open(stringsfilepath, "rb")

    binary_data = bytearray(stringsfile.read(3))
    tuple_of_data = struct.unpack("HB", binary_data)
    NrOfStrings = (tuple_of_data[0] + (tuple_of_data[1] << 16)) // 3

    string_offsets = []
    stringsfile.seek(0)
    for i in range(NrOfStrings):
        binary_data = bytearray(stringsfile.read(3))
        tuple_of_data = struct.unpack("HB", binary_data)
        string_offsets += [tuple_of_data[0] + (tuple_of_data[1] << 16)]

    strings = []
    for i in range(NrOfStrings):
        strings += [_read_string(stringsfile, string_offsets[i])]

    linesfile = open(linesfilepath, "rb")

    binary_data = bytearray(linesfile.read(3))
    tuple_of_data = struct.unpack("HB", binary_data)
    NrOfLines = (tuple_of_data[0] + (tuple_of_data[1] << 16)) // 4

    line_offsets = []
    line_nrs = []
    linesfile.seek(0)
    for i in range(NrOfLines):
        binary_data = bytearray(linesfile.read(3))
        tuple_of_data = struct.unpack("HB", binary_data)
        line_offsets += [tuple_of_data[0] + (tuple_of_data[1] << 16)]
        binary_data = bytearray(linesfile.read(1))
        line_nrs += [binary_data[0]]

    lines = []
    for i in range(NrOfLines):
        sentence = ""
        linesfile.seek(line_offsets[i])
        for j in range(line_nrs[i]):
            binary_data = bytearray(linesfile.read(1))
            byte = binary_data[0]
            if byte >= 128:
                binary_data = bytearray(linesfile.read(1))
                byte2 = binary_data[0]
                byte += (byte2 - 1) << 7
            sentence += strings[byte] + " "
        lines += [sentence]

    outfile = open(outfilepath, "wb")
    mainfile = open(mainfilepath, "rb")
    filesize = os.path.getsize(mainfilepath)
    i = 0
    while i < filesize:
        binary_data = bytearray(mainfile.read(1))
        byte = binary_data[0]
        i += 1
        if byte >= 128:
            binary_data = bytearray(mainfile.read(1))
            byte2 = binary_data[0]
            i += 1
            byte += ((byte2 - 1) << 7)
        outfile.write((lines[byte].strip() + "\n").encode("utf-8"))
    outfile.close()


def compress_tok(filename, filename2, filename3, inputfile):
    """Compress a text file into the three ``.tok`` parts."""
    dic_strings = {}
    dic_lines = {}
    list_file = []

    with open(inputfile) as fin:
        for line in fin:
            if line.strip() == "":
                continue
            if line.startswith("//"):
                continue

            newline = ""
            words = line.split()
            for word in words:
                word = word.strip().rstrip(",")
                if word == "//":
                    break
                if word in dic_strings:
                    dic_strings[word] += 1
                else:
                    dic_strings[word] = 1
                newline += word + " "
            if newline in dic_lines:
                dic_lines[newline] += 1
            else:
                dic_lines[newline] = 1
            list_file += [newline]

    if os.path.dirname(filename) != "":
        if not os.path.exists(os.path.dirname(filename)):
            os.makedirs(os.path.dirname(filename))
    output_rom = open(filename, "wb")

    nrofStrings = 0
    sorted_strings = sorted(dic_strings.items(), key=operator.itemgetter(1, 0), reverse=True)
    for key, val in sorted_strings:
        dic_strings[key] = nrofStrings
        nrofStrings += 1

    offset = nrofStrings * 3
    for key, val in sorted_strings:
        output_rom.write(struct.pack("<H", int(offset)))
        offset += len(key)
        output_rom.write(struct.pack("<B", int(0)))
    for key, val in sorted_strings:
        string = key[:-1]
        ba1 = bytearray(string.encode("UTF-8"))
        ba1.append(ord(key[len(key) - 1]) + 0x80)
        output_rom.write(ba1)

    if os.path.dirname(filename2) != "":
        if not os.path.exists(os.path.dirname(filename2)):
            os.makedirs(os.path.dirname(filename2))
    output_rom2 = open(filename2, "wb")

    nrofLines = 0
    linesInByts = []
    sorted_lines = sorted(dic_lines.items(), key=operator.itemgetter(1, 0), reverse=True)
    for key, val in sorted_lines:
        dic_lines[key] = nrofLines
        lineInBytes = []
        words = key.split()
        for word in words:
            byte1 = dic_strings[word] % 0x80
            if dic_strings[word] >= 0x80:
                byte1 += 0x80
                lineInBytes += [byte1]
                byte2 = dic_strings[word] >> 7
                lineInBytes += [byte2]
            else:
                lineInBytes += [byte1]
        nrofLines += 1
        linesInByts += [lineInBytes]

    offset = nrofLines * 4
    i = 0
    for key, val in sorted_lines:
        output_rom2.write(struct.pack("<H", int(offset)))
        words = key.split()
        line = linesInByts[i]
        offset += len(line)
        output_rom2.write(struct.pack("<B", int(0)))
        output_rom2.write(struct.pack("<B", int(len(words))))
        i += 1

    for bytes_ in linesInByts:
        output_rom2.write(bytearray(bytes_))

    if os.path.dirname(filename3) != "":
        if not os.path.exists(os.path.dirname(filename3)):
            os.makedirs(os.path.dirname(filename3))
    output_rom3 = open(filename3, "wb")

    data = []
    for line in list_file:
        byte2 = 0
        testvalue = dic_lines[line]
        byte1 = testvalue % 0x80
        if testvalue >= 128:
            byte1 += 0x80
            data += [byte1]
            byte2 = testvalue >> 7
            data += [byte2]
        else:
            data += [byte1]

    output_rom3.write(bytearray(data))


def _rewrite_numentries(path, key, prefix):
    """Regenerate the ``key:`` header in ``path`` from the count of ``prefix`` records.

    Preserves the file's original line endings and leaves the file completely
    untouched when the header already holds the correct count.
    """
    with open(path, "r", encoding="utf-8", newline="") as f:
        lines = f.readlines()

    count = 0
    for line in lines:
        if line.startswith(prefix):
            count += 1

    key_idx = None
    for i, line in enumerate(lines):
        if line.startswith(key + ":"):
            key_idx = i
            break

    if key_idx is not None and lines[key_idx].strip() == key + ": " + str(count):
        return

    eol = os.linesep
    for line in lines:
        if line.endswith("\r\n"):
            eol = "\r\n"
            break
        if line.endswith("\n"):
            eol = "\n"
            break

    new_key = key + ": " + str(count) + eol

    if key_idx is not None:
        lines[key_idx] = new_key
    else:
        lines.append(new_key)

    with open(path, "w", encoding="utf-8", newline="") as f:
        f.writelines(lines)


def update_tok_numentries(config_dir):
    """Update ``NUMENTRIES`` for ``skills.tok`` and ``items.tok`` in ``config_dir``."""
    _rewrite_numentries(os.path.join(config_dir, "skills.tok"), "NUMENTRIES", "SKILLCREATE")
    _rewrite_numentries(os.path.join(config_dir, "items.tok"), "NUMENTRIES", "ITEMCREATE")
