namespace ModdingGUI
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Register all built-in Core schemas (items, etc.) before any GUI
            // editor uses the API layer. Idempotent and thread-safe.
            ModdingGUI.Core.Core.Initialize();

            Application.Run(new frmMain());
        }
    }
}