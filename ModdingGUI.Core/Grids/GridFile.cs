using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace ModdingGUI.Core.Grids
{
    public sealed class GridFile
    {
        public const int ArenaSize = 32;   // 32×32 squares
        public const int NumSlots = 30;   // Max bits for named tags (0-29)
        public const string NO_VALUE = "**NONE**";

        private int _fileHeaderValue = 0; // Stores the first integer from the GRD file (layout version)
        private int _activeCountFromFile;  // Header count, preserved verbatim: vanilla files disagree
                                           // with their own F/E prefixes, so it is informational.

        // Master list of known tag names for UI suggestions or initializing NEW grids.
        // The order here defines a "default" layout for NEW files.
        public static readonly string[] DefaultTeamTagsMasterList =
        {
            "Start1","Start2","Start3","Start4",
            "Start5","Start6","Start7","Start8"
        };
        public static readonly string[] DefaultGameplayTagsMasterList =
        {
            "NoMoveNoCursor", "NoMove", "Gate", "NoLargeUnits",
            "MapCenter", "LineOfSightPass"
        };

        // Combined master list for UI purposes (e.g., a dropdown to pick any known tag for a slot)
        public static readonly string[] AllKnownTagsMasterList = DefaultTeamTagsMasterList.Concat(DefaultGameplayTagsMasterList).Distinct().OrderBy(s => s).ToArray();


        // Palette for colors - keyed by bit INDEX.
        // This means bit 0 will be Red, bit 1 Green, etc., REGARDLESS of the tag NAME at that bit.
        // This matches the Java tool's hardcoded color logic.
        private static readonly Dictionary<int, Color> BitIndexColorPalette = new()
        {
            [0] = Color.Red,
            [1] = Color.Green,
            [2] = Color.Blue,
            [3] = Color.Pink,
            [4] = Color.Orange,
            // Bit 5 in Java example was uncolored (would be black)
            [6] = Color.Cyan,
            // Add more fixed colors for specific bit indices if needed
            // e.g., [7] = Color.Magenta, [8] = Color.Yellow, etc.
        };

        // Instance members: these will hold the actual tags and active states loaded from a specific file
        private readonly string[] _tags = new string[NumSlots];       // Names of tags for bits 0-29, loaded from file
        private readonly bool[] _activeTags = new bool[NumSlots]; // Active state for bits 0-29, loaded from file

        private readonly int[] _data = new int[ArenaSize * ArenaSize]; // Raw grid cell data
        private readonly int[] _originalData = new int[ArenaSize * ArenaSize]; // For dirty checking
        private byte[] _trailingData = Array.Empty<byte>(); // Bytes past the 1024 cells (version 1 carries
                                                            // an unmodelled block here; preserved verbatim)
        private bool _hasBaseline = false;

        public bool IsDirty { get; private set; }

        // Constructor for a NEW, EMPTY grid.
        // It will set up a default set of tags.
        public GridFile()
        {
            _fileHeaderValue = 0; // Default for a new file

            // Initialize all slots to inactive and NO_VALUE
            for (int i = 0; i < NumSlots; i++)
            {
                _tags[i] = NO_VALUE;
                _activeTags[i] = false;
            }

            // Setup a default layout for a NEW grid (e.g., Team Tags first, then Gameplay Tags)
            int currentSlot = 0;
            foreach (string tagName in DefaultTeamTagsMasterList)
            {
                if (currentSlot < NumSlots)
                {
                    _tags[currentSlot] = tagName;
                    _activeTags[currentSlot] = true; // Make default tags active
                    currentSlot++;
                }
                else break;
            }
            foreach (string tagName in DefaultGameplayTagsMasterList)
            {
                if (currentSlot < NumSlots)
                {
                    // Avoid adding duplicates if they were already in team tags (though unlikely for these defaults)
                    bool alreadyAdded = false;
                    for (int k = 0; k < currentSlot; ++k)
                    {
                        if (_tags[k] == tagName)
                        {
                            alreadyAdded = true;
                            break;
                        }
                    }
                    if (!alreadyAdded)
                    {
                        _tags[currentSlot] = tagName;
                        _activeTags[currentSlot] = true; // Make default tags active
                        currentSlot++;
                    }
                }
                else break;
            }

            // _data is already initialized to zeros
            Array.Copy(_data, _originalData, _data.Length);
            _hasBaseline = true;
            IsDirty = false;
        }

        // Internal helper to set/update a tag for the CURRENT GridFile instance
        // This would be called by the UI if the user changes the tag assigned to a slot
        public void AssignTagToSlot(int slotIndex, string newTagName, bool isActive)
        {
            if (slotIndex < 0 || slotIndex >= NumSlots) return;

            string nameToAssign = string.IsNullOrEmpty(newTagName) ? NO_VALUE : newTagName;

            if (_tags[slotIndex] != nameToAssign || _activeTags[slotIndex] != isActive)
            {
                _tags[slotIndex] = nameToAssign;
                _activeTags[slotIndex] = isActive; // User explicitly sets active state with name
                IsDirty = true;
            }
        }

        public int GetValue(int row, int col) => _data[Index(row, col)];

        public void SetValue(int row, int col, int value)
        {
            int currentIndex = Index(row, col);
            if (_data[currentIndex] != value)
            {
                _data[currentIndex] = value;
                IsDirty = true;
            }
        }

        public string GetTagNameForSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= NumSlots) return string.Empty;
            return _tags[slotIndex];
        }

        public bool IsSlotActive(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= NumSlots) return false;
            return _activeTags[slotIndex];
        }

        public Color GetColor(int row, int col)
        {
            int currentValue = GetValue(row, col);
            if (currentValue == 0) return Color.White;

            Color determinedColor = Color.Black; // Default if no colored bit is found or lowest active bit is uncolored

            for (int i = 0; i < NumSlots; i++) // Iterate to find the lowest indexed active+set bit
            {
                if (_activeTags[i] && (currentValue & (1 << i)) != 0) // If this bit is active for this file AND set in cell
                {
                    // This is the lowest indexed active+set bit. Its color determines the cell color.
                    if (BitIndexColorPalette.TryGetValue(i, out var paletteColor))
                    {
                        determinedColor = paletteColor;
                    }
                    else
                    {
                        determinedColor = Color.Black; // Bit is active & set, but no color in palette for this INDEX
                    }
                    break; // Color is determined by the first (lowest index) active and set bit
                }
            }

            // Blend if dirty (C# specific visual feature)
            if (_hasBaseline && _originalData[Index(row, col)] != currentValue)
            {
                float blendFactor = 0.3f;
                int r = (int)(determinedColor.R * (1 - blendFactor) + Color.White.R * blendFactor);
                int g = (int)(determinedColor.G * (1 - blendFactor) + Color.White.G * blendFactor);
                int b = (int)(determinedColor.B * (1 - blendFactor) + Color.White.B * blendFactor);
                return Color.FromArgb(determinedColor.A, Math.Min(255, r), Math.Min(255, g), Math.Min(255, b));
            }
            return determinedColor;
        }

        public string GetInfoForValue(int value)
        {
            if (value == 0) return "(empty)";
            var tagStringsInCell = new List<string>();

            for (int i = 0; i < NumSlots; i++) // Check bits 0-29
            {
                if ((value & (1 << i)) != 0) // If bit 'i' is set in the cell's value
                {
                    if (_activeTags[i]) // And if slot 'i' is active for this loaded file
                    {
                        string tagName = _tags[i];
                        if (tagName.Equals(NO_VALUE, StringComparison.Ordinal) || string.IsNullOrEmpty(tagName))
                        {
                            tagStringsInCell.Add($"Slot {i} (Active, Unnamed)");
                        }
                        else
                        {
                            tagStringsInCell.Add(tagName.Trim());
                        }
                    }
                    else
                    {
                        // Bit 'i' is set, but the slot 'i' is NOT active (e.g., "E" prefix in file or no tag defined for it)
                        tagStringsInCell.Add($"Bit {i} (Inactive Slot)");
                    }
                }
            }

            // Check for any bits set beyond NumSlots (e.g. bit 30, 31 if NumSlots is 30)
            int higherBitsMask = ~((1 << NumSlots) - 1); // Mask for bits >= NumSlots
            if ((value & higherBitsMask) != 0)
            {
                tagStringsInCell.Add($"Other Flags (0x{(value & higherBitsMask):X})");
            }


            if (tagStringsInCell.Count == 0)
            {
                // This case should ideally not happen if value != 0, as some bit must be set.
                // It implies all set bits correspond to slots > NumSlots or something unexpected.
                return $"(Unknown flags: 0x{value:X})";
            }

            return string.Join(", ", tagStringsInCell);
        }

        public static GridFile Load(string fileName)
        {
            var g = new GridFile(); // Creates a new GridFile with default tag setup initially
            long minExpectedFileSize = 4 + 4 + (NumSlots * 32);
            string stage = "Initializing";
            try
            {
                using (var fs = File.OpenRead(fileName))
                {
                    if (fs.Length < minExpectedFileSize)
                    {
                        throw new IOException($"File '{Path.GetFileName(fileName)}' is smaller ({fs.Length} bytes) than the minimum expected ({minExpectedFileSize} bytes for header and tags). It may be corrupt or not a valid GRD file.");
                    }
                    using (var br = new BinaryReader(fs, Encoding.UTF8, false))
                    {
                        stage = "Reading File Header";
                        g._fileHeaderValue = br.ReadInt32(); // Format version: 1 = bare tag names, 2+ = F/E prefixed
                        stage = "Reading Tag Count";
                        g._activeCountFromFile = br.ReadInt32(); // informational; preserved on save

                        if (g._fileHeaderValue < 2)
                        {
                            // Legacy layout, see FUN_800b0e84 in the DOL: the header count is a BIG-endian
                            // name count, names are raw 32-byte records, and the cells come from a table of
                            // 1024 (u16 count, u32 offset) records whose offsets point at arrays of u32 slot
                            // indices. It is not a bitmask grid at all.
                            fs.Position = 0;
                            var legacyBytes = new byte[fs.Length];
                            int read = fs.Read(legacyBytes, 0, legacyBytes.Length);
                            if (read != legacyBytes.Length)
                                throw new EndOfStreamException(
                                    $"Incomplete read of legacy grid '{Path.GetFileName(fileName)}'.");

                            LoadLegacy(g, legacyBytes);
                            Array.Copy(g._data, g._originalData, g._data.Length);
                            g._hasBaseline = true;
                            g.IsDirty = false;
                            return g;
                        }

                        // 27 of the 430 vanilla grids are version 1 and store plain names ("Start1"),
                        // with no F/E prefix. Treating those as unknown made every slot vanish, which
                        // the Grid Builder showed as an empty tag list and the encounter validator as
                        // "slot does not exist" on maps that work in game.
                        bool bareTagNames = g._fileHeaderValue < 2;

                        stage = "Reading Tags from file to overwrite defaults";
                        byte[] tagBuffer = new byte[32];
                        for (int i = 0; i < NumSlots; ++i) // Read definitions for slots 0-29
                        {
                            int bytesRead = br.Read(tagBuffer, 0, tagBuffer.Length);
                            if (bytesRead < tagBuffer.Length)
                                throw new EndOfStreamException($"Incomplete read for tag block {i}. Expected 32 bytes, got {bytesRead}.");

                            string rawFullTagFromFile = Encoding.UTF8.GetString(tagBuffer, 0, bytesRead).TrimEnd('\0').Trim();

                            bool fileTagIsActive = false;
                            string fileTagName = NO_VALUE; // Default for this slot if parsing fails or empty

                            if (!string.IsNullOrEmpty(rawFullTagFromFile))
                            {
                                if (rawFullTagFromFile.StartsWith("F"))
                                {
                                    fileTagIsActive = true;
                                    fileTagName = (rawFullTagFromFile.Length > 1) ? rawFullTagFromFile.Substring(1).Trim() : NO_VALUE;
                                    if (string.IsNullOrEmpty(fileTagName)) fileTagName = NO_VALUE;
                                }
                                else if (rawFullTagFromFile.StartsWith("E"))
                                {
                                    fileTagIsActive = false;
                                    fileTagName = (rawFullTagFromFile.Length > 1) ? rawFullTagFromFile.Substring(1).Trim() : NO_VALUE;
                                    if (string.IsNullOrEmpty(fileTagName)) fileTagName = NO_VALUE;
                                }
                                else if (bareTagNames)
                                {
                                    // Version 1: the block is the tag name itself, always usable.
                                    fileTagIsActive = true;
                                    fileTagName = rawFullTagFromFile;
                                }
                                else
                                {
                                    // Version 2 without a prefix: no usable name in this slot.
                                    fileTagIsActive = false;
                                    fileTagName = NO_VALUE;
                                }
                            }
                            // Overwrite the default-initialized tags with what's in the file
                            g._activeTags[i] = fileTagIsActive;
                            g._tags[i] = fileTagName;
                        }

                        stage = "Reading Grid Data";
                        if (fs.Position + (g._data.Length * 4) > fs.Length)
                        {
                            throw new EndOfStreamException($"Not enough data for grid ({g._data.Length * 4} bytes expected) after reading tags. File may be truncated.");
                        }
                        for (int i = 0; i < g._data.Length; ++i)
                        {
                            g._data[i] = br.ReadInt32();
                        }

                        // Anything after the cells belongs to a layout we do not model yet (27 version-1
                        // grids carry 3.2 kB extra). Keeping the bytes makes those maps editable without
                        // pretending to understand them - the original Java tool silently truncated them.
                        long remaining = fs.Length - fs.Position;
                        if (remaining > 0)
                        {
                            g._trailingData = br.ReadBytes(checked((int)remaining));
                        }
                    }
                }
                Array.Copy(g._data, g._originalData, g._data.Length);
                g._hasBaseline = true;
                g.IsDirty = false; // Loaded file is not dirty initially
                return g;
            }
            catch (EndOfStreamException ex)
            {
                long fileLength = -1; try { using (var fsCheck = File.OpenRead(fileName)) { fileLength = fsCheck.Length; } } catch { /* ignore */ }
                throw new EndOfStreamException($"Unable to read beyond the end of the stream while processing '{Path.GetFileName(fileName)}' during stage: {stage}. " +
                                               (fileLength != -1 ? $"File length: {fileLength} bytes. " : "") +
                                               $"Original error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to load GRD file '{Path.GetFileName(fileName)}' during stage: {stage}. Error: {ex.Message}", ex);
            }
        }

        /// <summary>True when this grid's layout is fully understood (version 2 or newer).</summary>
        public bool IsWritableLayout => _fileHeaderValue >= 2;

        /// <summary>Layout version from the file header (1 = bare tag names and an unmapped body).</summary>
        public int LayoutVersion => _fileHeaderValue;

        public void Save(string fileName)
        {
            if (!IsWritableLayout)
            {
                throw new NotSupportedException(
                    $"Grid format version {_fileHeaderValue} is read-only: '{Path.GetFileName(fileName)}' " +
                    "has a body layout we have not mapped (its size implies two 32x32 layers behind a " +
                    "4-slot tag header, which does not fit all 27 version-1 files), so writing it could " +
                    "corrupt the map. The original Java tool wrote these files as version 2 and silently " +
                    "dropped the extra 3.2 kB. Reading the tags is supported.");
            }
            using (var fs = File.Create(fileName))
            using (var bw = new BinaryWriter(fs, Encoding.UTF8, false))
            {
                bw.Write(this._fileHeaderValue); // Write the stored/original header value

                // Preserved verbatim: some vanilla grids declare a count that disagrees with their own
                // F/E prefixes (qstinvasion1.grd says 3 while six tags are active), which proves the game
                // does not rely on it. Recomputing it would rewrite those files for no reason.
                bw.Write(_activeCountFromFile);

                // Version 1 grids store bare tag names; version 2+ prefix each with F (in use) or
                // E (unused). Writing the wrong form keeps the version byte but makes the slots
                // unreadable, so the format has to follow _fileHeaderValue.
                bool bareTagNames = _fileHeaderValue < 2;

                byte[] tagDataBuffer = new byte[32];
                for (int i = 0; i < NumSlots; i++) // For slots 0-29
                {
                    Array.Clear(tagDataBuffer, 0, tagDataBuffer.Length);

                    string tagStringToWrite = _tags[i] ?? NO_VALUE;
                    int offset = 0;

                    if (!bareTagNames)
                    {
                        tagDataBuffer[0] = (byte)(_activeTags[i] ? 'F' : 'E');
                        offset = 1;
                    }

                    // An unnamed slot holds the prefix alone (or nothing in version 1): the vanilla
                    // files write 'E', not 'E**NONE**'.
                    if (tagStringToWrite.Equals(NO_VALUE, StringComparison.Ordinal))
                        tagStringToWrite = string.Empty;

                    byte[] stringBytes = Encoding.UTF8.GetBytes(tagStringToWrite);
                    int lengthToCopy = Math.Min(stringBytes.Length, tagDataBuffer.Length - offset);
                    Buffer.BlockCopy(stringBytes, 0, tagDataBuffer, offset, lengthToCopy);

                    bw.Write(tagDataBuffer);
                }

                foreach (int v in _data)
                {
                    bw.Write(v);
                }

                // Trailing block last: version-1 files carry an unmodelled block after the cells.
                if (_trailingData.Length > 0)
                {
                    bw.Write(_trailingData);
                }
            }
            Array.Copy(_data, _originalData, _data.Length); // Update baseline
            _hasBaseline = true;
            IsDirty = false; // Saved file is no longer dirty
        }

        /// <summary>
        /// Parse the legacy (version &lt; 2) layout, matching <c>FUN_800b0e84</c> in the DOL.
        /// </summary>
        /// <remarks>
        /// <para>Header: <c>u32 version</c>, <c>u32 BE nameCount</c>, then <c>nameCount</c> × 32-byte raw
        /// names (no F/E prefix; every name is an active slot).</para>
        /// <para>Then 1024 records of 8 bytes: <c>u16 BE entryCount</c>, <c>u16 unused</c>,
        /// <c>u32 BE offset</c>. A record's offset points into the same file at an array of
        /// <c>u32 BE</c> slot indices; each index below 30 sets that slot's bit on the cell. Cells with
        /// <c>entryCount == 0</c> are empty, and the payload that the offsets point at sits after the
        /// record table — which is why these files are ~3.2 kB larger than version 2.</para>
        /// </remarks>
        private static void LoadLegacy(GridFile grid, byte[] data)
        {
            for (int slot = 0; slot < NumSlots; slot++)
            {
                grid._tags[slot] = NO_VALUE;
                grid._activeTags[slot] = false;
            }
            Array.Clear(grid._data, 0, grid._data.Length);

            if (data.Length < 8 + NumSlots * 2) return;

            int nameCount = data[4] | (data[5] << 8) | (data[6] << 16) | (data[7] << 24);
            if (nameCount < 0 || nameCount > NumSlots) nameCount = Math.Min(Math.Max(nameCount, 0), NumSlots);

            const int nameSize = 32;
            int tableStart = 8 + nameCount * nameSize;

            for (int i = 0; i < nameCount; i++)
            {
                int offset = 8 + i * nameSize;
                if (offset + nameSize > data.Length) break;

                string name = Encoding.ASCII.GetString(data, offset, nameSize)
                    .TrimEnd('\0').Trim();
                if (name.Length == 0) continue;

                grid._tags[i] = name;
                grid._activeTags[i] = true;
            }

            const int recordSize = 8;
            for (int cell = 0; cell < grid._data.Length; cell++)
            {
                int record = tableStart + cell * recordSize;
                if (record + recordSize > data.Length) break;

                int entries = data[record] | (data[record + 1] << 8);
                if (entries <= 0) continue;

                long payload = (uint)(data[record + 4] | (data[record + 5] << 8)
                             | (data[record + 6] << 16) | (data[record + 7] << 24));

                int value = 0;
                for (int e = 0; e < entries; e++)
                {
                    long at = payload + e * 4L;
                    if (at < 0 || at + 4 > data.Length) break;

                    int index = data[at] | (data[at + 1] << 8)
                              | (data[at + 2] << 16) | (data[at + 3] << 24);
                    if (index >= 0 && index < 0x1e) value |= 1 << index;
                }

                grid._data[cell] = value;
            }
        }

        // Helper to get the 1D array index from 2D grid coordinates
        private static int Index(int row, int col)
        {
            // Java: row = 32 - row - 1; return column * 32 + row;
            // (0,0) in visual grid (top-left) -> game logic (0,0) -> Java array index 31
            // (row 0, col 0 in C# terms of array access for visual top-left)
            int flippedGameRow = ArenaSize - 1 - row;
            return col * ArenaSize + flippedGameRow;
        }
    }
}
