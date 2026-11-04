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

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Register all built-in Core schemas (items, etc.) before any GUI
            // editor uses the API layer. Idempotent and thread-safe.
            ModdingGUI.Core.Core.Initialize();

            Application.Run(new frmMain());
            return 0;
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
