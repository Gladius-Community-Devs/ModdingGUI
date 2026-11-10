namespace ModdingGUI
{
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// The Windows bits the UI driver needs: finding a dialog that is blocking the window, and closing it.
    /// </summary>
    /// <remarks>
    /// A WinForms modal dialog is a Form and shows up in <c>Application.OpenForms</c>, but a <c>MessageBox</c> is a
    /// native #32770 window and appears in neither — hence the P/Invoke. Both are needed because the app has ~130
    /// <c>MessageBox.Show</c> calls: an automated session must be able to recover from one instead of hanging.
    /// </remarks>
    internal static class NativeDialogs
    {
        private const uint WmClose = 0x0010;
        private const uint BmClick = 0x00F5;

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr state);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder name, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr state);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        /// <summary>The first visible message box belonging to <paramref name="processId"/>, or <see cref="IntPtr.Zero"/>.</summary>
        public static IntPtr FindMessageBox(uint processId)
        {
            IntPtr found = IntPtr.Zero;

            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out uint owner);
                if (owner != processId || !IsWindowVisible(window)) return true;

                var className = new StringBuilder(64);
                GetClassName(window, className, className.Capacity);
                if (!className.ToString().Equals("#32770", StringComparison.Ordinal)) return true;

                found = window;
                return false;
            }, IntPtr.Zero);

            return found;
        }

        /// <summary>
        /// What the dialog says: its caption plus the text of its static children, which is where a message box keeps
        /// the actual message ("A unit name is required.") while the caption only names the topic.
        /// </summary>
        public static string TextOf(IntPtr window)
        {
            var caption = new StringBuilder(512);
            GetWindowText(window, caption, caption.Capacity);

            var body = new StringBuilder();
            EnumChildWindows(window, (child, _) =>
            {
                var className = new StringBuilder(64);
                GetClassName(child, className, className.Capacity);
                if (!className.ToString().Equals("Static", StringComparison.Ordinal)) return true;

                var text = new StringBuilder(512);
                GetWindowText(child, text, text.Capacity);
                if (text.Length > 0) body.Append(body.Length == 0 ? string.Empty : " ").Append(text);

                return true;
            }, IntPtr.Zero);

            string title = caption.Length == 0 ? "(untitled dialog)" : caption.ToString();
            return body.Length == 0 ? title : $"{title}: {body}";
        }

        /// <summary>
        /// Find a push button in the dialog by its label, so a confirmation can be answered instead of dismissed.
        /// </summary>
        /// <param name="window">The dialog.</param>
        /// <param name="labels">Labels to accept, in preference order ("&amp;Yes" and "Yes" both match).</param>
        public static IntPtr FindButton(IntPtr window, params string[] labels)
        {
            IntPtr found = IntPtr.Zero;

            foreach (string label in labels)
            {
                if (found != IntPtr.Zero) break;

                EnumChildWindows(window, (child, _) =>
                {
                    var className = new StringBuilder(64);
                    GetClassName(child, className, className.Capacity);
                    if (!className.ToString().Equals("Button", StringComparison.Ordinal)) return true;

                    var text = new StringBuilder(64);
                    GetWindowText(child, text, text.Capacity);
                    if (!text.ToString().Trim().TrimStart('&').Equals(label.TrimStart('&'), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    found = child;
                    return false;
                }, IntPtr.Zero);
            }

            return found;
        }

        /// <summary>Press a button the way a mouse would.</summary>
        public static void PostClick(IntPtr button) => PostMessage(button, BmClick, IntPtr.Zero, IntPtr.Zero);

        /// <summary>Ask the dialog to close the same way the title-bar X would.</summary>
        public static void PostClose(IntPtr window) =>
            PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
    }
}
