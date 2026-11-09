namespace ModdingGUI.Helpers;

using System;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// Opens a folder by asking the shell that is already running, instead of starting a new <c>explorer.exe</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two failure modes made the older approaches unusable: spawning <c>explorer.exe</c> ourselves dies with
/// <c>0xC0000142</c> (DLL initialisation failed) because the child inherits this process's environment, and the
/// ShellExecute verb is refused with "Access is denied" in locked-down contexts. <c>SHOpenFolderAndSelectItems</c>
/// hands the request to the existing shell through the shell namespace, so no new process is created at all.
/// </para>
/// </remarks>
internal static class ShellFolderOpener
{
    private const int Success = 0;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHParseDisplayName(string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn,
        out uint psfgaoOut);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, IntPtr[] apidl, uint dwFlags);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern void ILFree(IntPtr pidl);

    /// <summary>
    /// The form the shell needs: absolute, backslash separated, no trailing separator (except a drive root).
    /// </summary>
    /// <param name="path">A path as the tool holds it, possibly with forward slashes.</param>
    internal static string NormaliseForShell(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string full = Path.GetFullPath(path.Trim());

        // "E:\\" must stay "E:\\": trimming it would leave "E:", which GetFullPath reads as a relative path.
        bool isRoot = full.EndsWith(":\\", StringComparison.Ordinal) || full.EndsWith(":/", StringComparison.Ordinal);
        return isRoot ? full : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Ask the shell to show a folder. Returns false with a reason when the shell refuses.
    /// </summary>
    /// <param name="folder">An existing folder. A missing path is reported as a failure, not thrown.</param>
    /// <param name="error">Why it failed, when it did.</param>
    public static bool TryOpen(string folder, out string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        // The shell namespace rejects forward slashes outright (SHParseDisplayName returns E_INVALIDARG,
        // 0x80070057), and the tool's path fields hold forward slashes, so normalise before asking.
        string normalised = NormaliseForShell(folder);

        IntPtr pidl = IntPtr.Zero;
        try
        {
            int hr = SHParseDisplayName(normalised, IntPtr.Zero, out pidl, 0, out _);
            if (hr != Success)
            {
                error = $"the shell could not resolve '{normalised}' (0x{hr:X8})";
                return false;
            }

            hr = SHOpenFolderAndSelectItems(pidl, 0, null!, 0);
            if (hr != Success)
            {
                error = $"the shell refused to open '{normalised}' (0x{hr:X8})";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (DllNotFoundException ex)
        {
            error = $"shell32 is unavailable ({ex.Message})";
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            error = $"the shell API is unavailable ({ex.Message})";
            return false;
        }
        finally
        {
            if (pidl != IntPtr.Zero) ILFree(pidl);
        }
    }
}
