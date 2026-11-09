namespace ModdingGUI.Helpers;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

/// <summary>
/// What a folder-open failure is fighting: elevation, integrity level, a restricting job object, and the desktop the
/// process is attached to. Windows refuses shell operations with <c>ACCESS DENIED</c> in exactly these conditions, and
/// the answer to "why won't it open a folder" is otherwise invisible from the log.
/// </summary>
internal static class ProcessContext
{
    private const int TokenElevation = 20;
    private const int TokenIntegrityLevel = 25;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, int infoClass, out int info, int length,
        out int returned);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, int infoClass, out IntPtr info, int length,
        out int returned);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsProcessInJob(IntPtr process, IntPtr job, out bool inJob);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcessWindowStation();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetUserObjectInformation(IntPtr handle, int index, char[] info, int length,
        out int needed);

    /// <summary>True when the process token is elevated.</summary>
    public static bool IsElevated()
    {
        try
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0008 /* TOKEN_QUERY */, out IntPtr token))
            {
                return false;
            }

            try
            {
                bool ok = GetTokenInformation(token, TokenElevation, out int elevated, sizeof(int), out _);
                return ok && elevated != 0;
            }
            finally
            {
                CloseHandle(token);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Medium, High, Low or System, or "unknown".</summary>
    public static string IntegrityLevel()
    {
        try
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0008, out IntPtr token)) return "unknown";

            try
            {
                if (!GetTokenInformation(token, TokenIntegrityLevel, out IntPtr info, IntPtr.Size == 8 ? 32 : 20,
                        out _)) return "unknown";

                try
                {
                    // TOKEN_MANDATORY_LABEL = { SID_AND_ATTRIBUTES { PSID Sid } } - read the last sub-authority.
                    IntPtr sid = Marshal.ReadIntPtr(info);
                    int subAuthorityCount = Marshal.ReadByte(sid, 1);
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
                finally
                {
                    Marshal.FreeHGlobal(info);
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    /// <summary>True when the process belongs to a job object, which a sandbox uses to restrict it.</summary>
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

    /// <summary>The window station name: "WinSta0" is the interactive one, anything else cannot reach the desktop.</summary>
    public static string WindowStationName()
    {
        try
        {
            char[] buffer = new char[256];
            return GetUserObjectInformation(GetProcessWindowStation(), 2 /* UOI_NAME */, buffer, buffer.Length * 2,
                out int needed)
                ? new string(buffer, 0, Math.Max(0, needed / 2 - 1))
                : "unknown";
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    /// <summary>One line describing the environment, for the log when a shell operation is refused.</summary>
    public static string Describe()
    {
        string parent = "unknown";
        try
        {
            using var process = Process.GetCurrentProcess();
            parent = process.ProcessName;
        }
        catch (Exception)
        {
            // ignore
        }

        return $"elevated={IsElevated()}, integrity={IntegrityLevel()}, inJob={IsInJob()}, " +
               $"windowStation={WindowStationName()}, process={parent}";
    }
}
