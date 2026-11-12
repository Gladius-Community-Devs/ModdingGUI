# -*- coding: utf-8 -*-
"""Unit ``.idx`` pack/unpack.

The seven ``data/units/*.idx`` files describe every gladiator and the shared
tables they reference (names, schools, tints, skillsets, statsets, itemsets).
This module unpacks them into editable ``.txt`` files and repacks them back into
binary, byte-identically for vanilla data.

``unpack`` and ``repack`` take the ``data/`` folder as their single argument,
mirroring the original ``Gladius_Units_IDX_Unpack.py`` / ``_Repack.py`` scripts.
"""

import re
from dataclasses import dataclass, field

AFFINITY = ["None", "Air", "Dark", "Earth", "Fire",
            "5 - this should not appear; if it does, something broke", "Water"]

_ENTRY_SIZE = 14


class IDXDataError(Exception):
    """Raised when the editable unit tables cannot be repacked into ``.idx``.

    Carries a user-facing message that names the offending file and entry, so a
    bad edit surfaces as a clear, actionable error instead of a bare
    ``IndexError``/``ValueError`` deep inside the repack.
    """


def _check_ref(ref_text, unit_name, field, num_entries, source_file):
    """Validate a ``gladiators.txt`` set/school reference and return it as an int.

    ``num_entries`` is the number of entries the repack actually knows about for
    the referenced table. Raises :class:`IDXDataError` (instead of letting the
    lookup blow up with an ``IndexError``/``ValueError``) when the reference is
    missing, non-numeric, or out of range.
    """
    try:
        ref = int(ref_text)
    except (TypeError, ValueError):
        raise IDXDataError(
            "gladiators.txt: unit \"%s\" has \"%s: %s\", which is not a whole "
            "number. Use an integer from 0 to %d." % (unit_name, field, ref_text,
                                                      num_entries - 1))
    if ref < 0 or ref >= num_entries:
        raise IDXDataError(
            "gladiators.txt: unit \"%s\" references %s %d, but the repack only "
            "knows entries 0-%d. This usually means you forgot to save %s after "
            "editing it, or the unit points at an entry that was deleted."
            % (unit_name, field, ref, num_entries - 1, source_file))
    return ref


def _check_byte(value_text, file_label, context, low, high):
    """Validate that ``value_text`` is an integer in ``[low, high]``.

    Used for fields that are later packed into a single byte; without this the
    repack surfaces a bare ``OverflowError: int too big to convert`` (or silently
    truncates, in the case of the 5-bit outfit field).
    """
    try:
        value = int(value_text)
    except (TypeError, ValueError):
        raise IDXDataError(
            "%s: %s is not a whole number: \"%s\"." % (file_label, context, value_text))
    if value < low or value > high:
        raise IDXDataError(
            "%s: %s is %d, but it must be between %d and %d."
            % (file_label, context, value, low, high))
    return value


def _check_numentries(header_value, actual_count, file_label, section_label):
    """Compare a file's ``NUMENTRIES:`` header against the number of sections the
    repack actually parsed. Catches the common "forgot to save the file" /
    "renumbered a section out of order" mistake before it corrupts the .idx.
    """
    if header_value is None:
        raise IDXDataError(
            "%s is missing its 'NUMENTRIES:' line. Unpack the vanilla data again "
            "to regenerate it." % file_label)
    if header_value != actual_count:
        raise IDXDataError(
            "%s declares NUMENTRIES: %d, but the repack found %d \"%s\" section(s). "
            "This usually means you forgot to save the file after editing, deleted "
            "a section, or renumbered a \"%s N:\" header out of sequence."
            % (file_label, header_value, actual_count, section_label, section_label))


@dataclass
class Unit:
    name: str
    class_name: str
    outfit: int
    affinity: str
    tint_set: int
    skill_set: int
    stat_set: int
    item_set: int
    school: int


@dataclass
class SkillSet:
    entries: list = field(default_factory=list)


@dataclass
class StatSet:
    entries: list = field(default_factory=list)


@dataclass
class ItemSet:
    entries: list = field(default_factory=list)


###############################################################################
# UNPACK
###############################################################################

def unpack(data_folder):
    data_folder = _norm(data_folder)

    unitunitsIDX = data_folder + "units/unitunits.idx"
    unitnamesIDX = data_folder + "units/unitnames.idx"
    unittintsIDX = data_folder + "units/unittints.idx"
    unitskillsIDX = data_folder + "units/unitskills.idx"
    unitstatsIDX = data_folder + "units/unitstats.idx"
    unititemsIDX = data_folder + "units/unititems.idx"
    unitschoolsIDX = data_folder + "units/unitschools.idx"
    skillsTOK = data_folder + "config/skills.tok"
    classdefsTOK = data_folder + "config/classdefs.tok"
    itemsTOK = data_folder + "config/items.tok"
    gladiatorsOutput = data_folder + "units/gladiators.txt"
    tintsOutput = data_folder + "units/tints.txt"
    skillsetsOutput = data_folder + "units/skillsets.txt"
    statsetsOutput = data_folder + "units/statsets.txt"
    itemsetsOutput = data_folder + "units/itemsets.txt"
    schoolsOutput = data_folder + "units/schools.txt"

    # Parse unit entries from UnitUnits.IDX (dictionary file)
    unitEntries = []
    with open(unitunitsIDX, "rb") as f:
        numUnits = int.from_bytes(f.read(2), "big")
        print("Number of units recorded in file header: " + str(numUnits) + "\n")
        line = f.read(14)
        while line:
            unitEntries.append(line)
            line = f.read(14)

    print("Number of unit entries scanned: " + str(len(unitEntries)) + "\n")
    if len(unitEntries) == numUnits:
        print("File input success: Header matches number of entries.\n")
    else:
        raise ValueError("Error: Header does not match number of entries. Exiting...")

    # Populate unit name addresses and parse UnitNames.IDX
    unitNames = []
    i = 0
    with open(unitnamesIDX, "rb") as f:
        for line in unitEntries:
            f.seek(int.from_bytes(line[0:2], "big"))
            temp = f.read(1)
            unitNames.append(temp)
            while True:
                temp = f.read(1)
                if temp == b"\x00":
                    break
                else:
                    unitNames[i] += temp
            unitNames[i] = bytes.decode(unitNames[i], "utf8")
            i += 1

    # Populate unit school addresses and parse UnitSchools.IDX
    unitSchoolAddresses = []
    unitSchools = []
    for line in unitEntries:
        unitSchoolAddresses.append(int.from_bytes(line[12:14], "big"))
    unitSchoolAddressesIndex = list(set(unitSchoolAddresses))
    unitSchoolAddressesIndex.sort()
    i = 0
    with open(unitschoolsIDX, "rb") as f:
        unitSchoolsIDXFull = f.read()
        unitSchoolAddressesIndex.append(len(unitSchoolsIDXFull))
        for line in unitSchoolAddressesIndex:
            if i + 1 == len(unitSchoolAddressesIndex):
                break
            f.seek(unitSchoolAddressesIndex[i])
            unitSchools.append(bytes.decode(
                f.read(unitSchoolAddressesIndex[i + 1] - unitSchoolAddressesIndex[i] - 1), "utf8"))
            i += 1
    unitSchoolsIndex = []
    for line in unitSchoolAddresses:
        i = 0
        for line2 in unitSchoolAddressesIndex:
            if line2 == line:
                unitSchoolsIndex.append(i)
            i += 1

    # Index ability names from skills.tok
    skills = []
    i = 0
    with open(skillsTOK, "r", encoding="utf8") as f:
        skillsInput = f.readlines()
        for line in skillsInput:
            reOutput = re.search(r"^NUMENTRIES: (.*?)\n", skillsInput[i])
            if reOutput:
                numSkills = int(reOutput.group(1))
            reOutput = re.search(r"^SKILLCREATE: \"(.*?)\",", skillsInput[i])
            if reOutput:
                skills.append(reOutput.group(1))
            i += 1

    # Index class names from classdefs.tok
    classes = []
    i = 0
    with open(classdefsTOK, "r", encoding="utf8") as f:
        classInput = f.readlines()
        for line in classInput:
            reOutput = re.search(r"^NUMCLASSDEFS: (.*?)\n", classInput[i])
            if reOutput:
                numClasses = int(reOutput.group(1))
            reOutput = re.search(r"^CREATECLASS: (.*?)\n", classInput[i])
            if reOutput:
                classes.append(reOutput.group(1))
            i += 1

    # Index item names from items.tok
    items = []
    itemsType = []
    i = 0
    with open(itemsTOK, "r", encoding="utf8") as f:
        itemsInput = f.readlines()
        for line in itemsInput:
            reOutput = re.search(r"^NUMENTRIES: (.*?)\n", itemsInput[i])
            if reOutput:
                numItems = int(reOutput.group(1))
            reOutput = re.search(r"^ITEMCREATE: \"(.*?)\", \"(.*?)\",", itemsInput[i])
            if reOutput:
                items.append(reOutput.group(1))
                itemsType.append(reOutput.group(2))
            i += 1

    # Index unit classes
    unitClasses = []
    for line in unitEntries:
        unitClasses.append(classes[int.from_bytes(line[2:3], "big")])

    # Index unit outfits and affinity (byte: outfit 5 bits, affinity 3 bits).
    unitOutfits = []
    unitAffinity = []
    for line in unitEntries:
        temp = bin(int.from_bytes(line[3:4], "big"))[2:].zfill(8)
        unitOutfits.append(int(temp[0:5], 2))
        unitAffinity.append(AFFINITY[int(temp[5:8], 2)])

    # Index tints and parse UnitTints.IDX
    tintInput = []
    unitTints = []
    for line in unitEntries:
        unitTints.append(max(0, int((int.from_bytes(line[4:6], "big") + 17) / 18)))
    with open(unittintsIDX, "rb") as f:
        tintInput.append(int.from_bytes(f.read(1), "big"))
        line = f.read(18)
        while line:
            tintInput.append(line)
            line = f.read(18)

    # Parse UnitSkills.IDX and populate unit skillset addresses
    unitSkillAddresses = []
    for line in unitEntries:
        unitSkillAddresses.append(int.from_bytes(line[6:8], "big"))

    unitSkillAddressesIndex = [1]
    unitSkillsRaw = []
    skillsetRaw = bytearray()
    i = 0
    with open(unitskillsIDX, "rb") as f:
        unitSkillsIDXFull = f.read()
        f.seek(1)
        while True:
            data = f.read(2)
            if not data:
                break
            if data == b"\x00\x00" and (len(skillsetRaw) % 4 == 0 or len(skillsetRaw) == 0):
                unitSkillsRaw.append(bytes(skillsetRaw))
                unitSkillAddressesIndex.append(unitSkillAddressesIndex[i] + len(skillsetRaw) + 2)
                skillsetRaw = bytearray()
                i += 1
            else:
                skillsetRaw.append(int.from_bytes(data[0:1], "big"))
                skillsetRaw.append(int.from_bytes(data[1:2], "big"))

    unitSkills = []
    for line in unitSkillAddresses:
        i = 0
        for line2 in unitSkillAddressesIndex:
            if line2 == line:
                unitSkills.append(i)
            i += 1

    # New statset handling
    unitStatAddresses = []
    for line in unitEntries:
        unitStatAddresses.append(int.from_bytes(line[8:10], "big"))

    unitStatAddressesIndex = [1]
    unitStatsRaw = []
    i = 0
    with open(unitstatsIDX, "rb") as f:
        unitStatsIDXFull = f.read()
        f.seek(1)
        while True:
            data = f.read(150)
            if not data:
                break
            unitStatsRaw.append(data)
            unitStatAddressesIndex.append(unitStatAddressesIndex[i] + 150)
            i += 1

    unitStats = []
    for line in unitStatAddresses:
        i = 0
        for line2 in unitStatAddressesIndex:
            if line2 == line:
                unitStats.append(i)
            i += 1

    # Parse UnitItems.IDX and populate unit itemset addresses
    unitItemAddresses = []
    for line in unitEntries:
        unitItemAddresses.append(int.from_bytes(line[10:12], "big"))

    unitItemAddressesIndex = [0, 1]
    unitItemsRaw = []
    itemsetRaw = bytearray()
    i = 1
    unitItemsRaw.append(b"")
    with open(unititemsIDX, "rb") as f:
        unitItemsIDXFull = f.read()
        f.seek(1)
        while True:
            data = f.read(2)
            if not data:
                break
            if data == b"\x00\x00" and (len(itemsetRaw) % 4 == 0 or len(itemsetRaw) == 0):
                unitItemsRaw.append(bytes(itemsetRaw))
                unitItemAddressesIndex.append(unitItemAddressesIndex[i] + len(itemsetRaw) + 2)
                itemsetRaw = bytearray()
                i += 1
            else:
                itemsetRaw.append(int.from_bytes(data[0:1], "big"))
                itemsetRaw.append(int.from_bytes(data[1:2], "big"))

    unitItems = []
    for line in unitItemAddresses:
        i = 0
        for line2 in unitItemAddressesIndex:
            if line2 == line:
                unitItems.append(i)
            i += 1

    # Output to file
    i = 0
    with open(gladiatorsOutput, "w", encoding="utf8", newline="\n") as f:
        for line in unitEntries:
            f.write("Name: " + unitNames[i] + "\n")
            f.write("Class: " + str(unitClasses[i]) + "\n")
            f.write("Outfit: " + str(unitOutfits[i]) + "\n")
            f.write("Affinity: " + str(unitAffinity[i]) + "\n")
            f.write("Tint set: " + str(unitTints[i]) + "\n")
            f.write("Skill set: " + str(unitSkills[i]) + "\n")
            f.write("Stat set: " + str(unitStats[i]) + "\n")
            f.write("Item set: " + str(unitItems[i]) + "\n")
            f.write("School: " + str(unitSchoolsIndex[i]) + "\n")
            f.write("\n")
            i += 1

    with open(schoolsOutput, "w", encoding="utf8") as f:
        f.write("Header (IDK what this means; change it if you want to experiment): "
                + str(int.from_bytes(unitSchoolsIDXFull[0:1], "big")) + "\n\n")
        f.write("NUMENTRIES: " + str(len(unitSchools)) + "\n\n")
        i = 0
        for line in unitSchools:
            f.write(str(i) + ": " + line + "\n")
            i += 1

    with open(skillsetsOutput, "w", encoding="utf8") as f:
        f.write("NUMENTRIES: " + str(len(unitSkillsRaw)) + "\n\n")
        f.write("Legend:\n")
        f.write("MinLevelLearned MaxLevelLearned SkillName\n\n")
        i = 0
        for line in unitSkillsRaw:
            f.write("Skillset " + str(i) + ":\n")
            entries = [line[j:j + 4] for j in range(0, len(line), 4)]
            for entry in entries:
                f.write(str(int.from_bytes(entry[0:1], "big")) + " "
                        + str(int.from_bytes(entry[1:2], "big")) + " "
                        + skills[int.from_bytes(entry[2:4], "big")] + "\n")
            f.write("\n")
            i += 1

    with open(statsetsOutput, "w", encoding="utf8") as f:
        f.write("NUMENTRIES: " + str(len(unitStatsRaw)) + "\n")
        f.write("Legend:\n")
        f.write("Level: Con Pow Acc Def Ini/Mov\n\n")
        i = 0
        for line in unitStatsRaw:
            f.write("Statset " + str(i) + ":\n")
            entries = [line[j:j + 5] for j in range(0, len(line), 5)]
            j = 1
            for entry in entries:
                f.write(str(j) + ": " + str(int.from_bytes(entry[0:1], "big")) + " "
                        + str(int.from_bytes(entry[1:2], "big")) + " "
                        + str(int.from_bytes(entry[2:3], "big")) + " "
                        + str(int.from_bytes(entry[3:4], "big")) + " "
                        + str(int.from_bytes(entry[4:5], "big")) + "\n")
                j += 1
            f.write("\n")
            i += 1

    with open(itemsetsOutput, "w", encoding="utf8") as f:
        f.write("NUMENTRIES: " + str(len(unitItemsRaw)) + "\n\n")
        f.write("Legend:\n")
        f.write("MinLevelUsed MaxLevelUsed ItemName  [Any text here is ignored; use this space "
                "for notes. If you don't want to use it, delete the square brackets and remove any "
                "whitespace after the item name.]\n\n")
        i = 0
        for line in unitItemsRaw:
            f.write("Itemset " + str(i) + ":\n")
            entries = [line[j:j + 4] for j in range(0, len(line), 4)]
            for entry in entries:
                f.write(str(int.from_bytes(entry[0:1], "big")) + " "
                        + str(int.from_bytes(entry[1:2], "big")) + " "
                        + items[int.from_bytes(entry[2:4], "big")]
                        + " [" + itemsType[int.from_bytes(entry[2:4], "big")] + "]\n")
            f.write("\n")
            i += 1

    i = 0
    with open(tintsOutput, "w", encoding="utf8") as f:
        for line in tintInput:
            if i != 0:
                f.write("Tint set " + str(i) + ":\n")
                f.write("Cloth1: " + str(int.from_bytes(line[0:1], "big")) + " "
                        + str(int.from_bytes(line[1:2], "big")) + " "
                        + str(int.from_bytes(line[2:3], "big")) + "\n")
                f.write("Cloth2: " + str(int.from_bytes(line[3:4], "big")) + " "
                        + str(int.from_bytes(line[4:5], "big")) + " "
                        + str(int.from_bytes(line[5:6], "big")) + "\n")
                f.write("Armor1: " + str(int.from_bytes(line[6:7], "big")) + " "
                        + str(int.from_bytes(line[7:8], "big")) + " "
                        + str(int.from_bytes(line[8:9], "big")) + "\n")
                f.write("Armor2: " + str(int.from_bytes(line[9:10], "big")) + " "
                        + str(int.from_bytes(line[10:11], "big")) + " "
                        + str(int.from_bytes(line[11:12], "big")) + "\n")
                f.write("Skin: " + str(int.from_bytes(line[12:13], "big")) + " "
                        + str(int.from_bytes(line[13:14], "big")) + " "
                        + str(int.from_bytes(line[14:15], "big")) + "\n")
                f.write("Hair: " + str(int.from_bytes(line[15:16], "big")) + " "
                        + str(int.from_bytes(line[16:17], "big")) + " "
                        + str(int.from_bytes(line[17:18], "big")) + "\n\n")
            else:
                f.write("Header (IDK what this means; change it if you want to experiment): "
                        + str(tintInput[0]) + "\n\n")
                f.write("NUMENTRIES: " + str(len(tintInput)) + "\n\n")
                f.write("Tint set 0:\nThis tint set is blank; it is unknown what it means for a "
                        "unit to use tint set 0 (possibly default colours?). Needs more research.\n\n")
            i += 1


###############################################################################
# REPACK
###############################################################################

def repack(data_folder):
    data_folder = _norm(data_folder)

    gladiatorsTXT = data_folder + "units/gladiators.txt"
    tintsTXT = data_folder + "units/tints.txt"
    skillsetsTXT = data_folder + "units/skillsets.txt"
    statsetsTXT = data_folder + "units/statsets.txt"
    itemsetsTXT = data_folder + "units/itemsets.txt"
    schoolsTXT = data_folder + "units/schools.txt"
    classdefsTOK = data_folder + "config/classdefs.tok"
    skillsTOK = data_folder + "config/skills.tok"
    itemsTOK = data_folder + "config/items.tok"
    unitNamesIDX = data_folder + "units/unitnames.idx"
    unitTintsIDX = data_folder + "units/unittints.idx"
    unitSkillsIDX = data_folder + "units/unitskills.idx"
    unitStatsIDX = data_folder + "units/unitstats.idx"
    unitItemsIDX = data_folder + "units/unititems.idx"
    unitSchoolsIDX = data_folder + "units/unitschools.idx"
    unitUnitsIDX = data_folder + "units/unitunits.idx"

    unitData = []
    unitUnitsRaw = []

    # Parse Gladiators.txt
    unitNames = []
    nameOffsets = []
    unitOutfits = []
    unitAffinities = []
    unitTints = []
    unitSkillsets = []
    unitItemsets = []
    unitStatsets = []
    unitSchools = []
    i = 1
    # The vanilla export is LF-only, but a text editor (e.g. classic Windows
    # Notepad) may re-save the file with CRLF line endings. Detect that so the
    # trailing \r of every line can be treated as a line-ending artifact rather
    # than part of a unit name (the Muduk unit really does end in \r in the
    # vanilla data, so it must be preserved for LF files).
    with open(gladiatorsTXT, "rb") as _f:
        _raw = _f.read()
    _crlf_lines = _raw.count(b"\r\n")
    _lf_lines = _raw.count(b"\n")
    _crlf_saved = _crlf_lines > _lf_lines - _crlf_lines
    with open(gladiatorsTXT, "r", newline="\n") as f:
        unitData = f.readlines()
        f.seek(0)
        unitDataRaw = f.read()
        reOutput = re.findall("(?s)Name: (.*?)\nClass", unitDataRaw)
        for matchObj in reOutput:
            if _crlf_saved:
                matchObj = matchObj.rstrip("\r")
            unitNames.append(matchObj)
            nameOffsets.append(i)
            i += len(matchObj) + 1

    for element in nameOffsets:
        unitUnitsRaw.append(element.to_bytes(2, "big"))

    for line in unitData:
        reOutput = re.search(r"^Outfit: (.*?)$", line)
        if reOutput:
            unitOutfits.append(reOutput.group(1).rstrip("\r"))
    for line in unitData:
        reOutput = re.search(r"^Affinity: (.*?)$", line)
        if reOutput:
            try:
                unitAffinities.append(AFFINITY.index(reOutput.group(1).rstrip("\r")))
            except ValueError:
                raise IDXDataError(
                    "gladiators.txt has an invalid affinity: \"%s\". Valid "
                    "affinities are: %s." % (reOutput.group(1).rstrip("\r"),
                                             ", ".join(AFFINITY)))
    for line in unitData:
        reOutput = re.search(r"^Tint set: (.*?)$", line)
        if reOutput:
            unitTints.append(reOutput.group(1).rstrip("\r"))
    for line in unitData:
        reOutput = re.search(r"^Skill set: (.*?)$", line)
        if reOutput:
            unitSkillsets.append(reOutput.group(1).rstrip("\r"))
    for line in unitData:
        reOutput = re.search(r"^Stat set: (.*?)$", line)
        if reOutput:
            unitStatsets.append(reOutput.group(1).rstrip("\r"))
    for line in unitData:
        reOutput = re.search(r"^Item set: (.*?)$", line)
        if reOutput:
            unitItemsets.append(reOutput.group(1).rstrip("\r"))
    for line in unitData:
        reOutput = re.search(r"^School: (.*?)$", line)
        if reOutput:
            unitSchools.append(reOutput.group(1).rstrip("\r"))

    # Index class names from classdefs.tok
    classes = []
    i = 0
    with open(classdefsTOK, "r", encoding="utf8") as f:
        classInput = f.readlines()
        for line in classInput:
            reOutput = re.search(r"^NUMCLASSDEFS: (.*?)\n", classInput[i])
            if reOutput:
                numClasses = int(reOutput.group(1))
            reOutput = re.search(r"^CREATECLASS: (.*?)\n", classInput[i])
            if reOutput:
                classes.append(reOutput.group(1))
            i += 1

    unitClasses = []
    unitClassesText = []
    for line in unitData:
        reOutput = re.search(r"^Class: (.*?)$", line)
        if reOutput:
            unitClassesText.append(reOutput.group(1).rstrip("\r"))

    # Every unit must have a full set of fields, or the parallel lists below
    # drift out of sync and a later lookup fails deep inside the repack. Checked
    # up front so a missing line is reported cleanly (not as an IndexError).
    for label, arr in (("Class", unitClassesText), ("Outfit", unitOutfits),
                       ("Affinity", unitAffinities), ("Tint set", unitTints),
                       ("Skill set", unitSkillsets), ("Stat set", unitStatsets),
                       ("Item set", unitItemsets), ("School", unitSchools)):
        if len(arr) != len(unitNames):
            raise IDXDataError(
                "gladiators.txt defines %d units, but only %d \"%s:\" entries "
                "were found. Every unit needs every line (Class, Outfit, "
                "Affinity, Tint set, Skill set, Stat set, Item set, School); a "
                "missing or blank line throws the tables out of sync."
                % (len(unitNames), len(arr), label))

    i = 0
    for line in unitClassesText:
        try:
            class_index = classes.index(line)
        except ValueError:
            raise IDXDataError(
                "gladiators.txt: unit \"%s\" has class \"%s\", but classdefs.tok "
                "does not define it (it defines %d classes). Check the spelling "
                "in gladiators.txt or add the class to config/classdefs.tok."
                % (unitNames[i], line, len(classes)))
        unitClasses.append(class_index)
        unitUnitsRaw[i] += class_index.to_bytes(1, "big")
        i += 1

    # Calculate outfit and affinity bytes
    i = 0
    for line in unitNames:
        _check_byte(unitOutfits[i], "gladiators.txt",
                    "unit \"%s\" Outfit" % unitNames[i], 0, 31)
        temp = "{:05b}".format(int(unitOutfits[i])) + "{:03b}".format(int(unitAffinities[i]))
        temp = int(temp, 2).to_bytes(1, "big")
        unitUnitsRaw[i] += temp
        i += 1

    # Index tints from tints TXT
    tintsRaw = []
    tints = []
    with open(tintsTXT, "r") as f:
        tintsRaw = f.readlines()
    if not tintsRaw or re.search(": (.*?)$", tintsRaw[0]) is None:
        raise IDXDataError(
            "tints.txt is empty or missing its header line. Unpack the vanilla "
            "data again to regenerate it.")
    tintsHeader = re.search(": (.*?)$", tintsRaw[0]).group(1)
    numTints = None
    tintSectionCount = 0
    for line in tintsRaw:
        reOutput = re.search(r"^NUMENTRIES: (.*?)\n", line)
        if reOutput:
            numTints = int(reOutput.group(1))
        if re.search(r"^Tint set (\d*?):", line):
            tintSectionCount += 1
        reOutput = re.search(r"^(?:Skin|Hair|Cloth1|Cloth2|Armor1|Armor2): (\d*?) (\d*?) (\d*?)$", line)
        if reOutput:
            tints.append(reOutput.group(1))
            tints.append(reOutput.group(2))
            tints.append(reOutput.group(3))
    _check_numentries(numTints, tintSectionCount, "tints.txt", "Tint set")

    i = 0
    for line in unitTints:
        _check_byte(line, "gladiators.txt",
                    "unit \"%s\" Tint set" % unitNames[i], 0, tintSectionCount)
        if int(line) == 0:
            temp = b"\x00\x00"
        else:
            temp = int(((int(line) - 1) * 18 + 1)).to_bytes(2, "big")
        unitUnitsRaw[i] += temp
        i += 1

    # Index ability names from skills.tok
    skills = []
    with open(skillsTOK, "r", encoding="utf8") as f:
        skillsInput = f.readlines()
        for line in skillsInput:
            reOutput = re.search(r"^NUMENTRIES: (.*?)\n", line)
            if reOutput:
                numSkills = int(reOutput.group(1))
            reOutput = re.search(r"^SKILLCREATE: \"(.*?)\",", line)
            if reOutput:
                skills.append(reOutput.group(1))

    # Index item names from items.tok
    items = []
    with open(itemsTOK, "r", encoding="utf8") as f:
        itemsInput = f.readlines()
        for line in itemsInput:
            reOutput = re.search(r"^NUMENTRIES: (.*?)\n", line)
            if reOutput:
                numItems = int(reOutput.group(1))
            reOutput = re.search(r"^ITEMCREATE: \"(.*?)\",", line)
            if reOutput:
                items.append(reOutput.group(1))

    # Index skillsets from skillsets TXT
    skillsetsInput = []
    skillsets = []
    skillsetOffsets = []
    skillsetOffsets.append(1)
    tempSkillset = []
    numSkillsets = None
    i = 0
    with open(skillsetsTXT) as f:
        skillsetsInput = f.readlines()
        for line in skillsetsInput:
            reOutput = re.search(r"^NUMENTRIES: (.*?)\n", line)
            if reOutput:
                numSkillsets = int(reOutput.group(1))
            reOutput = re.search(r"^Skillset (\d*?):.*?$", line)
            if reOutput:
                if int(reOutput.group(1)) == i + 1:
                    skillsets.append(tempSkillset)
                    skillsetOffsets.append(2 + (len(tempSkillset) * 4) + skillsetOffsets[i])
                    tempSkillset = []
                    i += 1
            reOutput = re.search(r"^(\d*?) (\d*?) (.*?)$", line)
            if reOutput:
                skill_name = reOutput.group(3)
                _check_byte(reOutput.group(1), "skillsets.txt",
                            "\"%s\" MinLevel" % line.strip(), 0, 255)
                _check_byte(reOutput.group(2), "skillsets.txt",
                            "\"%s\" MaxLevel" % line.strip(), 0, 255)
                try:
                    skill_index = skills.index(skill_name)
                except ValueError:
                    raise IDXDataError(
                        "skillsets.txt: skill \"%s\" (from the line \"%s\") is not "
                        "defined in config/skills.tok (which defines %d skills). "
                        "Check the spelling in skillsets.txt or add the skill to "
                        "config/skills.tok." % (skill_name, line.strip(), len(skills)))
                tempSkillset.append([reOutput.group(1), reOutput.group(2),
                                     skill_index])
        skillsets.append(tempSkillset)
    _check_numentries(numSkillsets, len(skillsetOffsets), "skillsets.txt", "Skillset")

    # Index statsets from statsets TXT
    statsetsInput = []
    statsets = []
    statsetOffsets = []
    statsetOffsets.append(1)
    tempStatset = []
    numStatsets = None
    i = 0
    with open(statsetsTXT) as f:
        statsetsInput = f.readlines()
        for line in statsetsInput:
            reOutput = re.search(r"^NUMENTRIES: (.*?)\n", line)
            if reOutput:
                numStatsets = int(reOutput.group(1))
            reOutput = re.search(r"^Statset (\d*?):$", line)
            if reOutput:
                if int(reOutput.group(1)) == i + 1:
                    statsets.append(tempStatset)
                    statsetOffsets.append((len(tempStatset) * 5) + statsetOffsets[i])
                    tempStatset = []
                    i += 1
            reOutput = re.search(r"^\d*?: (\d*?) (\d*?) (\d*?) (\d*?) (\d*?)$", line)
            if reOutput:
                for k in range(1, 6):
                    _check_byte(reOutput.group(k), "statsets.txt",
                                "\"%s\" value %d" % (line.strip(), k), 0, 255)
                tempStatset.append([reOutput.group(1), reOutput.group(2), reOutput.group(3),
                                    reOutput.group(4), reOutput.group(5)])
        statsets.append(tempStatset)
    _check_numentries(numStatsets, len(statsetOffsets), "statsets.txt", "Statset")

    # Index itemsets from itemsets TXT
    itemsetsInput = []
    itemsets = []
    itemsetOffsets = []
    itemsetOffsets.append(0)
    itemsetOffsets.append(1)
    tempItemset = []
    numItemsets = None
    i = 1
    with open(itemsetsTXT) as f:
        itemsetsInput = f.readlines()
        for line in itemsetsInput:
            reOutput = re.search(r"^NUMENTRIES: (.*?)\n", line)
            if reOutput:
                numItemsets = int(reOutput.group(1))
            reOutput = re.search(r"^Itemset (\d*?):$", line)
            if reOutput:
                if int(reOutput.group(1)) == i + 1:
                    itemsets.append(tempItemset)
                    itemsetOffsets.append(2 + (len(tempItemset) * 4) + itemsetOffsets[i])
                    tempItemset = []
                    i += 1
            reOutput = re.search(r"^(\d*?) (\d*?) (.*?)(\W*?\[.*?\]|$)", line)
            if reOutput:
                item_name = reOutput.group(3)
                _check_byte(reOutput.group(1), "itemsets.txt",
                            "\"%s\" MinLevel" % line.strip(), 0, 255)
                _check_byte(reOutput.group(2), "itemsets.txt",
                            "\"%s\" MaxLevel" % line.strip(), 0, 255)
                try:
                    item_index = items.index(item_name)
                except ValueError:
                    raise IDXDataError(
                        "itemsets.txt: item \"%s\" (from the line \"%s\") is not "
                        "defined in config/items.tok (which defines %d items). "
                        "Check the spelling in itemsets.txt or add the item to "
                        "config/items.tok." % (item_name, line.strip(), len(items)))
                tempItemset.append([reOutput.group(1), reOutput.group(2),
                                    item_index])
        itemsets.append(tempItemset)
    _check_numentries(numItemsets, len(itemsetOffsets), "itemsets.txt", "Itemset")

    # Assign bytes for skill, stat, and itemsets
    i = 0
    for line in unitNames:
        skillset_ref = _check_ref(unitSkillsets[i], unitNames[i], "Skill set",
                                  len(skillsetOffsets), "skillsets.txt")
        statset_ref = _check_ref(unitStatsets[i], unitNames[i], "Stat set",
                                 len(statsetOffsets), "statsets.txt")
        itemset_ref = _check_ref(unitItemsets[i], unitNames[i], "Item set",
                                 len(itemsetOffsets), "itemsets.txt")
        unitUnitsRaw[i] += int(skillsetOffsets[skillset_ref]).to_bytes(2, "big")
        unitUnitsRaw[i] += int(statsetOffsets[statset_ref]).to_bytes(2, "big")
        unitUnitsRaw[i] += int(itemsetOffsets[itemset_ref]).to_bytes(2, "big")
        i += 1

    # Index schools from schools TXT
    schoolsRaw = []
    schools = []
    schoolOffsets = []
    schoolOffsets.append(0)
    with open(schoolsTXT) as f:
        schoolsRaw = f.readlines()
    if not schoolsRaw or re.search(": (.*?)$", schoolsRaw[0]) is None:
        raise IDXDataError(
            "schools.txt is empty or missing its header line. Unpack the vanilla "
            "data again to regenerate it.")
    schoolsHeader = re.search(": (.*?)$", schoolsRaw[0]).group(1)
    numSchools = None
    schoolSectionCount = 0
    for line in schoolsRaw:
        reOutput = re.search(r"^NUMENTRIES: (.*?)\n", line)
        if reOutput:
            numSchools = int(reOutput.group(1))
        if re.search(r"^\d+: ", line):
            schoolSectionCount += 1
        reOutput = re.search(r"\d: (.*?)$", line)
        if reOutput:
            schools.append(reOutput.group(1))
    schools.pop(0)
    _check_numentries(numSchools, schoolSectionCount, "schools.txt", "school")
    schoolOffsets.append(1)
    i = 0
    for line in schools:
        schoolOffsets.append(i + len(line) + 2)
        i += len(line) + 1

    unitSchoolOffsets = []
    for i, line in enumerate(unitSchools):
        school_ref = _check_ref(unitSchools[i], unitNames[i], "School",
                                len(schoolOffsets), "schools.txt")
        unitSchoolOffsets.append(schoolOffsets[school_ref])

    i = 0
    for line in unitSchoolOffsets:
        temp = int(line).to_bytes(2, "big")
        unitUnitsRaw[i] += temp
        i += 1

    # Generate UnitNames.IDX
    with open(unitNamesIDX, "wb") as f:
        f.write(b"\xcd")
        for line in unitNames:
            f.write(str.encode(line))
            f.write(b"\x00")

    # Generate UnitSchoolsIDX
    with open(unitSchoolsIDX, "wb") as f:
        f.write(int(schoolsHeader).to_bytes(1, "big"))
        for line in schools:
            f.write(str.encode(line))
            f.write(b"\x00")

    # Generate UnitTintsIDX
    with open(unitTintsIDX, "wb") as f:
        f.write(int(tintsHeader).to_bytes(1, "big"))
        for line in tints:
            f.write(int(line).to_bytes(1, "big"))

    # Generate UnitSkillsIDX
    with open(unitSkillsIDX, "wb") as f:
        f.write(b"\xcd")
        for skillset in skillsets:
            for skill in skillset:
                f.write(int(skill[0]).to_bytes(1, "big"))
                f.write(int(skill[1]).to_bytes(1, "big"))
                f.write(int(skill[2]).to_bytes(2, "big"))
            f.write(b"\x00\x00")

    # Generate UnitStatsIDX
    with open(unitStatsIDX, "wb") as f:
        f.write(b"\xcd")
        for statset in statsets:
            for stat in statset:
                f.write(int(stat[0]).to_bytes(1, "big"))
                f.write(int(stat[1]).to_bytes(1, "big"))
                f.write(int(stat[2]).to_bytes(1, "big"))
                f.write(int(stat[3]).to_bytes(1, "big"))
                f.write(int(stat[4]).to_bytes(1, "big"))

    # Generate UnitItemsIDX
    with open(unitItemsIDX, "wb") as f:
        f.write(b"\xcd")
        for itemset in itemsets:
            for item in itemset:
                f.write(int(item[0]).to_bytes(1, "big"))
                f.write(int(item[1]).to_bytes(1, "big"))
                f.write(int(item[2]).to_bytes(2, "big"))
            f.write(b"\x00\x00")

    # Generate UnitUnits.IDX
    with open(unitUnitsIDX, "wb") as f:
        f.write(len(unitUnitsRaw).to_bytes(2, "big"))
        for line in unitUnitsRaw:
            f.write(line)


def _norm(data_folder):
    if not data_folder.endswith(("/", "\\")):
        return data_folder + "/"
    return data_folder
