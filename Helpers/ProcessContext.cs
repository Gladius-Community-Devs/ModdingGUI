namespace ModdingGUI.Helpers;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

/// <summary>
/// What a folder-open failure is fighting: elevation, integrity level, whether the process is inside a restricting job
/// object, and whether it is interactive at all. Windows refuses shell operations in exactly these conditions, and the
/// answer is otherwise invisible from the log.
/// </summary>
/// <remarks>
/// Every call here is defensive on purpose. An earlier version sized the integrity-level buffer with a constant, which
/// wrote the SID past the end of a stack slot and then freed memory that Marshal had not allocated; the result was an
/// <see cref="AccessViolationException"/>, which .NET cannot catch. Buffers are now sized from the API itself.
/// </remarks>
internal static class ProcessContext
{
    private const uint TokenQuery = 0x0008;
    private const int TokenIntegrityLevel = 25;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length,
        out int returned);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsProcessInJob(IntPtr process, IntPtr job, out bool inJob);

    /// <summary>True when the process runs with an elevated token. Managed, so it cannot fault.</summary>
    public static bool IsElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Medium, High, Low, System, or "unknown" when the token cannot be read.</summary>
    public static string IntegrityLevel()
    {
        IntPtr token = IntPtr.Zero;
        IntPtr buffer = IntPtr.Zero;

        try
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TokenQuery, out token) || token == IntPtr.Zero)
            {
                return "unknown";
            }

            // Ask for the size first: the TOKEN_MANDATORY_LABEL carries a SID whose length varies, so a fixed buffer
            // would overrun.
            GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out int needed);
            if (needed <= 0) return "unknown";

            buffer = Marshal.AllocHGlobal(needed);
            if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, needed, out _)) return "unknown";

            // TOKEN_MANDATORY_LABEL { SID_AND_ATTRIBUTES { PSID Sid; DWORD Attributes } }
            IntPtr sid = Marshal.ReadIntPtr(buffer);
            if (sid == IntPtr.Zero) return "unknown";

            int subAuthorityCount = Marshal.ReadByte(sid, 1);
            if (subAuthorityCount <= 0) return "unknown";

            // SID layout: revision(1), count(1), authority(6), then the sub-authorities (4 bytes each).
            int last = Marshal.ReadInt32(sid, 8 + ((subAuthorityCount - 1) * 4));

            return last switch
            {
                0x0000 => "Untrusted",
                0x1000 => "Low",
                0x2000 => "Medium",
                0x2100 => "Medium-Plus",
                0x3000 => "High",
                0x4000 => "System",
                _ => $"0x{last:X4}",
            };
        }
        catch (Exception)
        {
            return "unknown";
        }
        finally
        {
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            if (token != IntPtr.Zero) CloseHandle(token);
        }
    }

    /// <summary>True when the process belongs to a job object, which is how a sandbox restricts it.</summary>
    public static bool IsInJob()
    {
        try
        {
            return IsProcessInJob(IntPtr.Zero, IntPtr.Zero, out bool inJob) && inJob;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>False for a process with no desktop (a service, or a window station that is not the user's).</summary>
    public static bool IsInteractive()
    {
        try
        {
            return Environment.UserInteractive;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The session the process runs in; 0 is the services session, not a user desktop.</summary>
    public static int SessionId()
    {
        try
        {
            using Process process = Process.GetCurrentProcess();
            return process.SessionId;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    /// <summary>One line describing the environment, for the log when a shell operation is refused.</summary>
    public static string Describe() =>
        $"elevated={IsElevated()}, integrity={IntegrityLevel()}, inJob={IsInJob()}, " +
        $"interactive={IsInteractive()}, session={SessionId()}";
}
