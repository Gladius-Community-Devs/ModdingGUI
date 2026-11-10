namespace ModdingGUI;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

/// <summary>
/// Drives the running window over loopback JSON lines, so a script — or an AI agent helping a user — can operate the
/// app and see what it looks like.
/// </summary>
/// <remarks>
/// <para>
/// Off unless asked for: the app only listens when it is started with <c>--ui-server</c>, and it binds
/// <c>127.0.0.1</c> exclusively. Nothing here is a second code path into the game data: the commands press the same
/// buttons and fill the same fields a user would, so every edit still goes through the Core APIs (PLAN.md §0).
/// </para>
/// <para>
/// One request per line, one response per line:
/// </para>
/// <code>
/// {"cmd":"snapshot"}                                  // control tree + selected tab + log tail
/// {"cmd":"click","name":"btnTeamAddUnit"}             // press a button / toggle a checkbox
/// {"cmd":"set","name":"txtTeamUnitName","text":"Ludo"} // text, combo index/text, checked, tab
/// {"cmd":"selectNode","name":"tvwTeam","text":"Ludo"}  // pick a tree node
/// {"cmd":"screenshot","path":"C:\\tmp\\shot.png"}      // the window as a PNG (DrawToBitmap)
/// {"cmd":"log","count":50}                            // the app log tail
/// {"cmd":"openTeamBuilder","project":"C:\\mods\\x"}   // convenience: select a project, open the tab
/// {"cmd":"quit"}                                      // close the window
/// </code>
/// </remarks>
public static class UiDriver
{
    /// <summary>Port used when <c>--ui-server</c> is given without a number.</summary>
    public const int DefaultPort = 4571;

    /// <summary>What the command line asked the driver to do.</summary>
    public sealed record Options(bool Server, int Port, string? ScriptPath, bool ExitWhenDone)
    {
        /// <summary>Read the driver flags out of a command line; null when none are present.</summary>
        public static Options? Parse(string[] args)
        {
            if (args is null || args.Length == 0) return null;

            bool server = false;
            int port = DefaultPort;
            string? script = null;
            bool exit = false;
            bool found = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--ui-server":
                        server = true;
                        found = true;
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int explicitPort))
                        {
                            port = explicitPort;
                            i++;
                        }
                        break;

                    case "--ui-script" when i + 1 < args.Length:
                        script = args[++i];
                        found = true;
                        break;

                    case "--ui-exit":
                        exit = true;
                        break;
                }
            }

            return found ? new Options(server, port, script, exit) : null;
        }
    }

    /// <summary>
    /// Attach the driver to <paramref name="form"/>. Must be called on the UI thread, before the message loop runs.
    /// </summary>
    public static void Attach(Form form, Options options)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(options);

        if (form is not frmMain window) throw new ArgumentException("The driver needs the app's main form.", nameof(form));

        frmMain.UiAutomationActive = true;

        // A MessageBox shown inside a button handler blocks that handler, and the driver's Invoke with it: the run just
        // hangs with a dialog nobody is there to click. In automation mode, dismiss native message boxes as they appear
        // and record what they said - that is a report line, not a lost test.
        StartDialogGuard(window);

        if (options.Server) StartServer(window, options.Port);

        if (options.ScriptPath is not null)
        {
            form.Shown += async (_, _) =>
            {
                // Let the first paint finish: the tree scan and settings load are kicked off from Load/Shown.
                await Task.Delay(700);
                int exitCode = await RunScriptAsync(window, options.ScriptPath);
                if (options.ExitWhenDone)
                {
                    form.Close();
                }
                else
                {
                    Console.WriteLine($"[ui] script finished (exit {exitCode})");
                }
            };
        }
    }

    private static string? _statusPath;

    /// <summary>A small file naming the port and pid, so a client does not have to guess.</summary>
    public static string StatusPath => _statusPath ??= Path.Combine(
        Path.GetTempPath(), "gladius-ui-driver.json");

    private static void StartServer(frmMain window, int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        try
        {
            listener.Start();
        }
        catch (SocketException ex)
        {
            // Another instance may already be listening; the window still opens, which is what matters.
            Console.Error.WriteLine($"[ui] could not listen on 127.0.0.1:{port}: {ex.Message}");
            return;
        }

        try
        {
            File.WriteAllText(StatusPath, JsonSerializer.Serialize(new
            {
                port,
                pid = Environment.ProcessId,
                started = DateTimeOffset.Now,
            }));
        }
        catch (IOException)
        {
            // Diagnostic only.
        }

        Console.WriteLine($"[ui] listening on 127.0.0.1:{port}");

        var accept = new Thread(() =>
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch (Exception ex) when (ex is SocketException or ObjectDisposedException or InvalidOperationException)
                {
                    return;   // listener closed with the form
                }

                ThreadPool.QueueUserWorkItem(_ => Serve(window, client));
            }
        })
        {
            IsBackground = true,
            Name = "ui-driver-accept",
        };

        accept.Start();

        window.FormClosed += (_, _) =>
        {
            try { listener.Stop(); } catch (SocketException) { }
            try { if (File.Exists(StatusPath)) File.Delete(StatusPath); } catch (IOException) { }
        };
    }

    private static void Serve(frmMain window, TcpClient client)
    {
        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
            {
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string response = Invoke(window, line);
                    writer.WriteLine(response);

                    if (line.Contains("\"quit\"", StringComparison.Ordinal)) return;
                }
            }
        }
        catch (IOException)
        {
            // Client hung up mid-command; nothing to report to.
        }
        catch (ObjectDisposedException)
        {
            // The window closed while the client was connected.
        }
    }

    /// <summary>Run one command on the UI thread and return its response line.</summary>
    public static string Invoke(frmMain window, string requestJson)
    {
        // Waits must not run on the UI thread, or the window freezes while the unpack runs. They touch no control.
        if (window.TryExecuteBackgroundUiCommand(requestJson, out string background)) return background;

        try
        {
            if (window.IsDisposed) return Failure("the window is closed");

            return window.InvokeRequired
                ? (string)window.Invoke(new Func<string>(() => Execute(window, requestJson)))
                : Execute(window, requestJson);
        }
        catch (ObjectDisposedException)
        {
            return Failure("the window is closed");
        }
        catch (InvalidOperationException ex)
        {
            return Failure(ex.Message);
        }
    }

    /// <summary>Execute a command file (one command per line) and wait for it to finish.</summary>
    public static async Task<int> RunScriptAsync(frmMain window, string scriptPath)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(scriptPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"[ui] cannot read {scriptPath}: {ex.Message}");
            return 2;
        }

        int failures = 0;
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            // A "wait" is the one command that must not block the UI thread for its duration: the paint that
            // makes an assertion meaningful only happens while the thread is free.
            if (line.Contains("\"wait\"", StringComparison.Ordinal))
            {
                int ms = System.Text.Json.JsonDocument.Parse(line).RootElement.TryGetProperty("ms", out var msValue)
                    ? msValue.GetInt32()
                    : 250;
                await Task.Delay(ms);
                Console.WriteLine($"[ui] wait {ms}ms");
                continue;
            }

            string response = Invoke(window, line);
            Console.WriteLine($"[ui] {line} -> {response}");
            if (!response.StartsWith("{\"ok\":true", StringComparison.Ordinal)) failures++;
        }

        return failures == 0 ? 0 : 1;
    }

    private static string Execute(frmMain window, string requestJson) => window.ExecuteUiCommand(requestJson);

    internal static string Failure(string message) =>
        JsonSerializer.Serialize(new { ok = false, error = message });

    /// <summary>
    /// Watch for native message boxes and close them, logging what they said.
    /// </summary>
    /// <remarks>
    /// Automation must be able to walk every path a user can, including the ones that raise a MessageBox. Without this
    /// the first validation dialog (an empty unit name, say) stops the run for good, because the call that raised it is
    /// still on the stack.
    /// </remarks>
    private static void StartDialogGuard(frmMain window)
    {
        // One line per dialog, not one per timer tick: a Yes/No box refuses WM_CLOSE, so an un-answerable dialog used to
        // be re-reported five times a second while the run waited for a human.
        IntPtr lastDialog = IntPtr.Zero;
        string lastText = string.Empty;

        var timer = new System.Windows.Forms.Timer { Interval = 250 };
        timer.Tick += (_, _) =>
        {
            IntPtr dialog = NativeDialogs.FindMessageBox((uint)Environment.ProcessId);
            if (dialog == IntPtr.Zero)
            {
                lastDialog = IntPtr.Zero;
                lastText = string.Empty;
                return;
            }

            string text = NativeDialogs.TextOf(dialog);
            if (dialog == lastDialog && text == lastText) return;

            lastDialog = dialog;
            lastText = text;

            // A confirmation gets the answer a scripted run would give the user's own prompt: yes, and it is logged.
            IntPtr affirmative = NativeDialogs.FindButton(dialog, "Yes", "Continue");
            IntPtr acknowledgement = affirmative == IntPtr.Zero ? NativeDialogs.FindButton(dialog, "OK") : IntPtr.Zero;
            string answer = affirmative != IntPtr.Zero ? "answered yes"
                : acknowledgement != IntPtr.Zero ? "acknowledged"
                : "dismissed";
            if (affirmative != IntPtr.Zero) NativeDialogs.PostClick(affirmative);
            else if (acknowledgement != IntPtr.Zero) NativeDialogs.PostClick(acknowledgement);
            else NativeDialogs.PostClose(dialog);

            window.Output.Write(
                ModdingGUI.Core.Logging.LogLevel.Warning,
                $"[automation] dialog {answer}: {text}",
                "driver");
        };

        timer.Start();
    }
}
