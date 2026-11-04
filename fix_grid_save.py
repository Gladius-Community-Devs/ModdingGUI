"""Save must keep a grid's own tag format: version 1 stores bare names, version 2 F/E prefixes."""
from pathlib import Path

p = Path("ModdingGUI.Core/Grids/GridFile.cs")
t = p.read_text(encoding="utf-8")

old = '''                byte[] tagDataBuffer = new byte[32];
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
                }'''

new = '''                // Version 1 grids store bare tag names; version 2+ prefix each with F (in use) or
                // E (unused). Writing the wrong form keeps the file's version byte but makes the
                // slots unreadable, so the format has to follow _fileHeaderValue.
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
                    else if (tagStringToWrite.Equals(NO_VALUE))
                    {
                        // A version 1 slot with no name is simply empty.
                        tagStringToWrite = string.Empty;
                    }

                    byte[] stringBytes = Encoding.UTF8.GetBytes(tagStringToWrite);
                    int lengthToCopy = Math.Min(stringBytes.Length, tagDataBuffer.Length - offset);
                    Buffer.BlockCopy(stringBytes, 0, tagDataBuffer, offset, lengthToCopy);

                    bw.Write(tagDataBuffer);
                }'''

if old not in t:
    raise SystemExit("save tag block not found")
t = t.replace(old, new, 1)
p.write_text(t, encoding="utf-8", newline="")
print("Save follows the grid's own tag format")
