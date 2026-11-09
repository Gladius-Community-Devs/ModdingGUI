namespace ModdingGUI.Core.History;

using System;

/// <summary>
/// One entry in a project's <c>.mod-history</c>: a copy of a file as it was before some edit.
/// </summary>
/// <param name="Timestamp">When the backup was taken, parsed from its file name.</param>
/// <param name="RelativePath">The project-relative path of the file that was backed up (always <c>/</c> separated).</param>
/// <param name="BackupPath">Absolute path of the <c>.bak</c> file itself.</param>
/// <param name="Size">Size of the backup in bytes.</param>
public sealed record BackupEntry(DateTime Timestamp, string RelativePath, string BackupPath, long Size)
{
    /// <summary>Display form for a list: <c>2026-07-11 18:08:03  data/units/gladiators.txt  (1.2 MB)</c>.</summary>
    public override string ToString() => $"{Timestamp:yyyy-MM-dd HH:mm:ss}  {RelativePath}  ({Size:N0} bytes)";
}
