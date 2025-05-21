using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace ModdingGUI
{
    public sealed class GridFile
    {
        public const int ArenaSize = 32;   // 32×32 squares
        public const int NumSlots = 30;   // Max bits for named tags (0-29)
        public const string NO_VALUE = "**NONE**";

        private int _fileHeaderValue = 0; // Stores the first integer from the GRD file

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
            bool unknownBitsPresent = false;

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
                        unknownBitsPresent = true;
                    }
                }
            }

            // Check for any bits set beyond NumSlots (e.g. bit 30, 31 if NumSlots is 30)
            int higherBitsMask = ~((1 << NumSlots) - 1); // Mask for bits >= NumSlots
            if ((value & higherBitsMask) != 0)
            {
                tagStringsInCell.Add($"Other Flags (0x{(value & higherBitsMask):X})");
                unknownBitsPresent = true;
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
                        g._fileHeaderValue = br.ReadInt32(); // Store the actual header value
                        stage = "Reading Tag Count";
                        br.ReadInt32(); // This is the "activeCount" in Java, can be ignored if we rely on F/E prefixes

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
                                else
                                {
                                    // No F/E prefix. Java's behavior: m_activeTags[i] remains false (from its init).
                                    // m_gridTags[i] retains its constructor-initialized value.
                                    // For C#, if we want to be robust: treat as inactive, name is NO_VALUE.
                                    // Or, if the game expects any non-F/E string to be an active tag name:
                                    // fileTagIsActive = true; fileTagName = rawFullTagFromFile.Trim();
                                    // Given Java's strict F/E, let's assume no prefix = inactive.
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

        public void Save(string fileName)
        {
            using (var fs = File.Create(fileName))
            using (var bw = new BinaryWriter(fs, Encoding.UTF8, false))
            {
                bw.Write(this._fileHeaderValue); // Write the stored/original header value

                int activeTagCountCalculated = 0;
                for (int i = 0; i < NumSlots; i++)
                {
                    if (_activeTags[i])
                    {
                        activeTagCountCalculated++;
                    }
                }
                bw.Write(activeTagCountCalculated); // This is the "numEntries" from Java

                byte[] tagDataBuffer = new byte[32];
                for (int i = 0; i < NumSlots; i++) // For slots 0-29
                {
                    Array.Clear(tagDataBuffer, 0, tagDataBuffer.Length);

                    tagDataBuffer[0] = (byte)(_activeTags[i] ? 'F' : 'E');

                    // Use the current tag name for this slot (_tags[i])
                    string tagStringToWrite = _tags[i] ?? NO_VALUE;
                    if (!_activeTags[i] && !tagStringToWrite.Equals(NO_VALUE))
                    {
                        // If tag is inactive but has a name other than NO_VALUE,
                        // Java would still write E + that name.
                        // If it's inactive and NO_VALUE, it writes E + NO_VALUE.
                    }
                    else if (_activeTags[i] && tagStringToWrite.Equals(NO_VALUE))
                    {
                        // This is like "F**NONE**" - active but name is NO_VALUE
                    }


                    byte[] stringBytes = Encoding.UTF8.GetBytes(tagStringToWrite);
                    int lengthToCopy = Math.Min(stringBytes.Length, tagDataBuffer.Length - 1); // Max 31 bytes for tag name
                    Buffer.BlockCopy(stringBytes, 0, tagDataBuffer, 1, lengthToCopy);

                    bw.Write(tagDataBuffer);
                }

                foreach (int v in _data)
                {
                    bw.Write(v);
                }
            }
            Array.Copy(_data, _originalData, _data.Length); // Update baseline
            _hasBaseline = true;
            IsDirty = false; // Saved file is no longer dirty
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
