# -*- coding: utf-8 -*-
"""GameCube ISO pack/unpack.

Port of the original ``ngciso-tool.py``: unpacks a GameCube disc image into its
component sections (DOL, RomFS, FST) and repacks a folder tree back into a
bootable ISO.  PS2 and Xbox images are handled by external tools in the
orchestrator batch files (7z/ImgBurn and extract-xiso respectively).
"""

import os
import struct
import operator


def align_adr(adr, align_val):
    if (adr & (align_val - 1)) != 0:
        adr = adr & (0x100000000 - align_val)
        adr += align_val
    return adr


def align_file_size_with_zeros(file, pos, alignment):
    target = (pos + alignment - 1) & (0x100000000 - alignment)
    amount = target - pos
    file.write(b"\0" * amount)


def _read_word(file, offset):
    file.seek(offset)
    data = file.read(4)
    word, = struct.unpack(">I", data)
    return word


def _read_byte(file, offset):
    file.seek(offset)
    data = file.read(1)
    word, = struct.unpack("<B", data)
    return word


def _read_string(file, offset, size):
    file.seek(offset)
    string = ""
    for i in range(size):
        byte = bytearray(file.read(1))
        if byte[0] != 0:
            string += chr(byte[0])
    return string


def _read_string0(file, offset, size):
    file.seek(offset)
    string = ""
    bytes_ = bytearray(file.read(size))
    for i in range(size):
        if bytes_[i] != 0:
            string += chr(bytes_[i])
        else:
            break
    return string


def _read_hex_string(file, offset, size):
    file.seek(offset)
    string = ""
    for i in range(size):
        byte = bytearray(file.read(1))
        string += ("%02x" % byte[0])
    return string


###############################################################################
# UNPACK
###############################################################################

class _RomSection:
    def __init__(self, name, address, file_id, size):
        self.name = name
        self.address = address
        self.file_id = file_id
        self.size = size


class _DirName:
    def __init__(self, name, start, end):
        self.name = name
        self.start = start
        self.end = end


def unpack_iso(filename, filedir, out_file_list):
    """Unpack a GameCube ISO into ``filedir`` and write ``out_file_list``."""
    if not filedir.endswith(("/", "\\")):
        filedir += "/"
    rom_map = []
    dir_names = []

    def add_rom_section(name, address, size, file_id=-1):
        if size > 0:
            rom_map.append(_RomSection(name, address, file_id, size))

    def get_dir_path(file_id):
        string = ""
        for dir_ in dir_names:
            if (file_id >= dir_.start) & (file_id < dir_.end):
                string += dir_.name + "/"
        return string

    def add_dir_name(name, start, end):
        dir_names.append(_DirName(name, start, end))

    def write_section_in_file(file, dirname, filename, addr, size):
        file.seek(addr)
        f_byte_array = file.read(size)
        filename2 = dirname + filename
        if not os.path.exists(os.path.dirname(filename2)):
            os.makedirs(os.path.dirname(filename2))
        with open(filename2, "wb") as f:
            f.write(f_byte_array)
        print("Write section in file " + filename2)
        return filename

    def get_file_size(file):
        file.seek(0, os.SEEK_END)
        return file.tell()

    def write_sections_out_of_iso(file):
        output = ""
        old_address = 0
        rom_map.sort(key=operator.attrgetter("address"))
        for item in rom_map:
            if old_address != item.address:
                write_section_in_file(file, filedir, "Unknown_" + hex(old_address) + ".bin",
                                      old_address, item.address - old_address)
                output += hex(old_address) + " " + "/Unknown_" + hex(old_address) + ".bin" + " " \
                    + hex(int(-1)) + " " + hex(item.address - old_address) + "\n"
            output += hex(item.address) + " " + str(item.name) + " " + hex(int(item.file_id)) \
                + " " + hex(item.size) + "\n"
            old_address = align_adr(item.address + item.size, 4)
        return output

    def get_output_rom_map_file_id():
        output = ""
        rom_map.sort(key=operator.attrgetter("file_id"))
        for item in rom_map:
            output += hex(int(item.file_id)) + " " + str(item.name) + " " \
                + hex(item.address) + " " + hex(item.size) + "\n"
        return output

    def romfs_diagnose(file, base_address):
        output = romfs_rootdir_diagnose(file, base_address)
        num_entries = _read_word(file, base_address + 0x8)
        for i in range(num_entries):
            file_offset = 0xc * i
            print("RomFS: " + str(i))
            if _read_byte(file, base_address + file_offset) == 1:
                romfs_dir_diagnose(file, base_address, file_offset, num_entries * 0xc)
            elif _read_byte(file, base_address + file_offset) == 0:
                output += romfs_file_diagnose(file, base_address, file_offset,
                                              num_entries * 0xc, get_dir_path(i))
        return output

    def romfs_rootdir_diagnose(file, base_address):
        offset_string = (_read_word(file, base_address + 0x0)) & 0xffffff
        parent_offset = _read_word(file, base_address + 0x4)
        num_entries = _read_word(file, base_address + 0x8)
        return "\nRootDir: " + hex(offset_string) + " - " + hex(parent_offset) \
            + " - " + hex(num_entries)

    def romfs_dir_diagnose(file, base_address, offset, string_table):
        offset_string = ((_read_word(file, base_address + offset + 0x0)) & 0xffffff) + string_table
        string = _read_string0(file, base_address + offset_string, 0x20)
        parent_offset = _read_word(file, base_address + offset + 0x4)
        next_offset = _read_word(file, base_address + offset + 0x8)
        if offset == 0:
            string = ""
        output = string
        print("dir name at: " + hex(base_address + offset_string))
        print("offset: " + hex(int(offset / 0xc)) + " (parent: " + hex(parent_offset)
              + "), (next: " + hex(next_offset) + "), " + output)
        add_dir_name(string, offset / 0xc, next_offset)
        return output

    def romfs_file_diagnose(file, base_address, offset, string_table, path):
        offset_string = ((_read_word(file, base_address + offset + 0x0)) & 0xffffff) + string_table
        string = _read_string0(file, base_address + offset_string, 0x20)
        file_offset = _read_word(file, base_address + offset + 0x4)
        file_length = _read_word(file, base_address + offset + 0x8)
        if string == "":
            string = "Unknown_" + hex(file_offset) + ".bin"
        print("file name at: " + hex(base_address + offset_string))
        add_rom_section(path + string, file_offset, file_length, offset / 0xc)
        write_section_in_file(file, (filedir + path).replace("//", "/"),
                              string.replace("//", "/"), file_offset, file_length)
        return ""

    def dolf_sdiagnose(file, base_address):
        header_output = "\n"
        text_pos = []
        data_pos = []
        text_mem = []
        data_mem = []
        text_size = []
        data_size = []
        tot_size = 0x100
        for i in range(6):
            text_pos += [_read_word(file, base_address + 0x0 + 0x4 * i)]
            text_mem += [_read_word(file, base_address + 0x48 + 0x4 * i)]
            text_size += [_read_word(file, base_address + 0x90 + 0x4 * i)]
            tot_size += text_size[i]
        for i in range(6):
            header_output += "Text " + hex(text_pos[i]) + ":   " + hex(text_mem[i]) \
                + " - " + hex(text_size[i]) + "\n"
        for i in range(10):
            data_pos += [_read_word(file, base_address + 0x1c + 0x4 * i)]
            data_mem += [_read_word(file, base_address + 0x64 + 0x4 * i)]
            data_size += [_read_word(file, base_address + 0xac + 0x4 * i)]
            tot_size += data_size[i]
        for i in range(10):
            header_output += "Data " + hex(data_pos[i]) + ":   " + hex(data_mem[i]) \
                + " - " + hex(data_size[i]) + "\n"
        header_output += "\n"
        for i in range(6):
            write_section_in_file(file, filedir + "code/", "Text_" + hex(text_mem[i]) + ".bin",
                                  base_address + text_pos[i], text_size[i])
        for i in range(10):
            write_section_in_file(file, filedir + "code/", "Data_" + hex(data_mem[i]) + ".bin",
                                  base_address + data_pos[i], data_size[i])
        tot_size = align_adr(tot_size, 0x100)
        write_section_in_file(file, filedir, "bootfile.dol", base_address, tot_size)
        add_rom_section("/bootfile.dol", base_address, tot_size)
        return header_output

    file = open(filename, "rb")

    header_output = filename + "\n"
    header_output += "FileSize:    " + hex(get_file_size(file)) + "\n"

    header_output += "\nGame Code:         " + str(_read_string(file, 0x0, 0x4))
    header_output += "\nMaker Code:        " + str(_read_string(file, 0x4, 0x2))
    header_output += "\nDVD Magic Word:    0x" + str(_read_hex_string(file, 0x1c, 0x4))
    header_output += "\nGame Name:         " + str(_read_string(file, 0x20, 0x3e0))
    write_section_in_file(file, filedir, "boot.bin", 0x0, 0x440)
    add_rom_section("/boot.bin", 0x0, 0x440)
    write_section_in_file(file, filedir, "bi2.bin", 0x440, 0x2000)
    add_rom_section("/bi2.bin", 0x440, 0x2000)

    apploader_entry = _read_word(file, 0x2440 + 0x10)
    header_output += "\nApploader Entrypoint: " + hex(apploader_entry)
    appsize = _read_word(file, 0x2440 + 0x14)
    appsize += _read_word(file, 0x2440 + 0x18)
    append = align_adr(0x2440 + appsize, 0x100)
    write_section_in_file(file, filedir, "appldr.bin", 0x2440, appsize)
    add_rom_section("/appldr.bin", 0x2440, append - 0x2440)

    fst_offset = _read_word(file, 0x424)
    fst_size = _read_word(file, 0x428)
    write_section_in_file(file, filedir, "fst.bin", fst_offset, fst_size)
    add_rom_section("/fst.bin", fst_offset, fst_size)

    header_output += dolf_sdiagnose(file, _read_word(file, 0x420))
    header_output += romfs_diagnose(file, _read_word(file, 0x424))

    header_output += "\n\n"
    file_list = write_sections_out_of_iso(file) + "\n"
    header_output += file_list
    header_output += get_output_rom_map_file_id()

    if out_file_list != "":
        # An absolute destination is used as given: v007 (and v008, which inherited it) prefixed the output folder
        # unconditionally, so a caller passing "C:\...\filelist.txt" got "<outdir>/C:" and an OSError - the unpack
        # wrote every file and still exited 1.
        if not os.path.isabs(out_file_list):
            out_file_list = filedir + out_file_list
        directory = os.path.dirname(out_file_list)
        if directory and not os.path.exists(directory):
            os.makedirs(directory)
        with open(out_file_list, "w") as f:
            f.write(file_list)

    headerfilename = filedir + "_Header.txt"
    if not os.path.exists(os.path.dirname(headerfilename)) and os.path.dirname(headerfilename):
        os.makedirs(os.path.dirname(headerfilename))
    with open(headerfilename, "w", encoding="utf-8") as f:
        f.write(header_output)

    file.close()
    return header_output


###############################################################################
# PACK
###############################################################################

class _FstDir:
    def __init__(self, name, flag, file_id=0, file_size=0):
        self.name = name
        self.lower = name.lower()
        self.sub_dirs = []
        self.files = []
        self.flag = flag
        self.file_id = file_id
        self.file_size = file_size
        self.offset = 0

    def get_sub_dir(self, name):
        for dir_ in self.sub_dirs:
            if dir_.name == name:
                return dir_
        return None

    def add_sub_dir(self, name):
        sub_dir = self.get_sub_dir(name)
        if sub_dir is None:
            self.sub_dirs.append(_FstDir(name, 1))
        self.sub_dirs = sorted(self.sub_dirs, key=lambda d: d.lower.replace("_", "}"))
        return self.get_sub_dir(name)

    def add_file(self, name, file_id, file_size):
        sub_dir = self.get_sub_dir(name)
        if sub_dir is None:
            self.sub_dirs.append(_FstDir(name, 0, file_id, file_size))
        self.sub_dirs = sorted(self.sub_dirs, key=lambda d: d.lower.replace("_", "}"))
        return self.get_sub_dir(name)

    def print_dir(self, offset):
        string = offset + self.name + " " + hex(self.get_nr_of_entries()) + " " \
            + hex(self.file_id) + " " + hex(self.offset) + "\n"
        for i in self.sub_dirs:
            string += i.print_dir(offset + self.name + "/")
        for i in self.files:
            string += offset + self.name + "/" + i + "\n"
        return string

    def create_fst(self, string_table_length, parent_id, own_id):
        if self.file_id == -0x1:
            return ([], "")

        word2 = 0
        word3 = 0
        string = ""
        if self.name != "":
            string += self.name + "\0"
            if self.flag == 0:  # file
                word3 = self.file_size
                word2 = self.offset
            else:  # directory
                word3 = own_id + self.get_nr_of_entries() + 1
                word2 = parent_id
        else:  # root
            word3 = self.get_nr_of_entries() + 1

        fst = [self.flag << 24 | string_table_length, word2, word3]

        n = 0
        for i in self.sub_dirs:
            res = i.create_fst(string_table_length + len(string), own_id, own_id + n + 1)
            fst += res[0]
            string += res[1]
            if res[1] != "":
                n += 1 + i.get_nr_of_entries()

        return (fst, string)

    def get_nr_of_entries(self):
        nr = 0
        for i in self.sub_dirs:
            if i.file_id != -1:
                nr += 1
            nr += i.get_nr_of_entries()
        return nr

    def set_offset(self, offset):
        self.offset = offset
        return offset + self.file_size

    def get_length_of_strings(self):
        length = len(self.name) + 1
        for i in self.sub_dirs:
            if i.file_id != -1:
                length += i.get_length_of_strings()
        return length

    def set_file_size(self, file_size):
        self.file_size = file_size


def pack_iso(dir, filename, fst_filename, fstmap):
    """Repack a folder tree into a GameCube ISO."""
    if not dir.endswith(("/", "\\")):
        dir += "/"
    root_dir = _FstDir("", 1)
    rom_map = []

    def add_rom_section(name, address, size, file_id=-1):
        if size > 0:
            rom_map.append(_RomSection(name, address, file_id, size))

    def add_file_to_fst(name, file_id, file_size):
        cur_dir = root_dir
        name = name[1:]
        while name != "":
            pos = name.find("/")
            if pos != -1:
                cur_dir = cur_dir.add_sub_dir(name[0:pos])
                name = name[pos + 1:]
            else:
                cur_dir.add_file(name, file_id, file_size)
                name = ""

    def set_offset_of_file(name, offset):
        cur_dir = root_dir
        name = name[1:]
        while name != "":
            pos = name.find("/")
            if pos != -1:
                cur_dir = cur_dir.get_sub_dir(name[0:pos])
                name = name[pos + 1:]
            else:
                cur_dir = cur_dir.get_sub_dir(name)
                return cur_dir.set_offset(offset)
        return 0

    def set_file_size(name, file_size):
        cur_dir = root_dir
        name = name[1:]
        while name != "":
            pos = name.find("/")
            if pos != -1:
                cur_dir = cur_dir.get_sub_dir(name[0:pos])
                name = name[pos + 1:]
            else:
                cur_dir = cur_dir.get_sub_dir(name)
                cur_dir.set_file_size(file_size)
                return 1
        return 0

    def calc_size_of_fst():
        size = root_dir.get_nr_of_entries() * 0xc + 0xc
        size += root_dir.get_length_of_strings()
        return size - 1

    with open(fstmap) as fin:
        for line in fin:
            words = line.split()
            if len(words) == 4:
                filepath = dir + words[1]
                file_size = os.path.getsize(filepath)
                add_rom_section(words[1], int(words[0], 16), file_size, int(words[2], 16))
                add_file_to_fst(words[1], int(words[2], 16), file_size)

    set_file_size("/fst.bin", align_adr(calc_size_of_fst(), 4))
    offset = 0
    with open(fstmap) as fin:
        for line in fin:
            words = line.split()
            if len(words) == 4:
                offset = set_offset_of_file(words[1], offset)
                if (words[1] == "/appldr.bin") or (words[1] == "/bootfile.dol"):
                    offset = align_adr(offset, 0x100)
                offset = align_adr(offset, 4)

    output = ""
    output += get_output_rom_map_file_id(rom_map) + "\n"
    output += root_dir.print_dir("")
    new_fst = root_dir.create_fst(0, 0, 0)

    if os.path.dirname(fst_filename) != "":
        if not os.path.exists(os.path.dirname(fst_filename)):
            os.makedirs(os.path.dirname(fst_filename))
    with open(fst_filename, "wb") as output_fst:
        for i in new_fst[0]:
            byte1 = i & 0xff
            byte2 = (i >> 8) & 0xff
            byte3 = (i >> 16) & 0xff
            byte4 = (i >> 24) & 0xff
            output_fst.write(struct.pack("BBBB", byte4, byte3, byte2, byte1))
        output_fst.write(bytearray(new_fst[1], "utf8"))

    if os.path.dirname(filename) != "":
        if not os.path.exists(os.path.dirname(filename)):
            os.makedirs(os.path.dirname(filename))
    output_rom = open(filename, "wb")

    bootfile_offset = 0
    fst_offset = 0
    with open(fstmap) as fin:
        for line in fin:
            words = line.split()
            if len(words) == 4:
                filepath = dir + words[1]
                print("writing " + dir + words[1])
                filepart = bytearray(open(filepath, "rb").read())

                if words[1] == "/bootfile.dol":
                    bootfile_offset = output_rom.tell()
                    print("bootfile_offset: " + hex(bootfile_offset))
                elif words[1] == "/fst.bin":
                    fst_offset = output_rom.tell()
                    print("fst_offset: " + hex(fst_offset))

                output_rom.write(filepart)

                if (words[1] == "/appldr.bin") or (words[1] == "/bootfile.dol"):
                    align_file_size_with_zeros(output_rom, output_rom.tell(), 0x100)
                else:
                    align_file_size_with_zeros(output_rom, output_rom.tell(), 0x4)

    output_rom.seek(0x420)
    output_rom.write(struct.pack(">II", int(bootfile_offset), int(fst_offset)))
    output_rom.close()

    return output


def get_output_rom_map_file_id(rom_map):
    output = ""
    rom_map.sort(key=operator.attrgetter("file_id"))
    for item in rom_map:
        output += hex(int(item.file_id)) + " " + str(item.name) + " " \
            + hex(item.address) + " " + hex(item.size) + "\n"
    return output
