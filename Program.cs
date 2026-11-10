namespace ModdingGUI
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        /// <remarks>
        /// With no arguments this starts the WinForms shell. With <c>--pack</c> or
        /// <c>--unpack</c> it runs the same batch pipeline the GUI generates (see
        /// <see cref="CliInvocation"/>) and exits with that batch's exit code, without
        /// initialising any UI.
        /// </remarks>
        [STAThread]
        static int Main(string[] args)
        {
            var invocation = CliInvocation.TryParse(args, out string? cliError);
            if (invocation is not null || cliError is not null)
                return RunCli(invocation, cliError);

            // --ui-server / --ui-script: let a script (or an AI agent) drive the window. Off unless asked for;
            // see Helpers/UiDriver.cs.
            UiDriver.Options? uiDriver = UiDriver.Options.Parse(args);

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Register all built-in Core schemas (items, etc.) before any GUI
            // editor uses the API layer. Idempotent and thread-safe.
            ModdingGUI.Core.Core.Initialize();

            // Several handlers are async event handlers, where a thrown exception has nowhere to go: the process
            // dies with no log and the user just sees the window vanish. Route both kinds of failure through the
            // app log and a message box instead.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ReportUnhandled("UI thread", e.Exception);
            AppDomain.CurrentDomain.UnhandledException +=
                (_, e) => ReportUnhandled("background thread", e.ExceptionObject as Exception);

            var form = new frmMain();
            if (uiDriver is not null) UiDriver.Attach(form, uiDriver);

            Application.Run(form);
            return 0;
        }

        /// <summary>
        /// Log an exception that reached the top of the stack and show it once. Never rethrows: the alternative is
        /// the silent exit this is here to prevent.
        /// </summary>
        private static void ReportUnhandled(string origin, Exception? exception)
        {
            if (exception is null) return;

            string detail = exception.ToString();

            try
            {
                var log = ModdingGUI.Core.Logging.AppLog.CreateForRun();
                log.Write(ModdingGUI.Core.Logging.LogLevel.Error,
                    $"Unhandled exception on the {origin}: {detail}", "app");

                MessageBox.Show(
                    $"Something went wrong on the {origin}:\n\n{exception.Message}\n\n" +
                    "The details were written to the application log.",
                    "Unexpected Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception)
            {
                // Reporting must not become its own failure; the message box below is the last resort.
                MessageBox.Show($"Something went wrong:\n\n{detail}", "Unexpected Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static int RunCli(CliInvocation? invocation, string? error)
        {
            if (error is not null)
            {
                Console.Error.WriteLine($"error: {error}");
                Console.Error.WriteLine();
                Console.Error.WriteLine(CliInvocation.Usage);
                return 2;
            }

            if (invocation!.IsHelp)
            {
                Console.WriteLine(CliInvocation.Usage);
                return 0;
            }

            try
            {
                return invocation.Run(AppContext.BaseDirectory);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return 1;
            }
        }
    }
}
