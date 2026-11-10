namespace ModdingGUI
{
    /// <summary>
    /// A one-line message strip that an editor can show without interrupting anybody.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The editors answered every bad field with a <c>MessageBox</c>: "Cost must be a whole number of 1 or more",
    /// "MinLevel must be between 1 and 30", "Skill name is required". Each one steals focus, needs a click, and hides
    /// the field it is about. A strip at the bottom of the page says the same thing, stays until the next action, and
    /// leaves the form usable.
    /// </para>
    /// <para>
    /// The caller also logs the message, so the record is in the log exactly as a modal's would have been.
    /// </para>
    /// </remarks>
    internal sealed class InlineNotice : Label
    {
        private static readonly Color ErrorInk = Color.FromArgb(168, 32, 32);
        private static readonly Color NoteInk = Color.FromArgb(150, 106, 0);

        /// <summary>Docked strip, hidden until it has something to say.</summary>
        public InlineNotice(string name)
        {
            Name = name;
            Dock = DockStyle.Bottom;
            Height = 28;
            Padding = new Padding(9, 0, 9, 0);
            TextAlign = ContentAlignment.MiddleLeft;
            AutoEllipsis = true;
            Visible = false;
        }

        /// <summary>Something the user has to fix.</summary>
        public void Warn(string message)
        {
            ForeColor = ErrorInk;
            Text = message;
            Visible = true;
        }

        /// <summary>Worth saying, not wrong.</summary>
        public void Note(string message)
        {
            ForeColor = NoteInk;
            Text = message;
            Visible = true;
        }

        /// <summary>Clear the strip (after a successful action).</summary>
        public void Clear()
        {
            Text = string.Empty;
            Visible = false;
        }
    }
}
