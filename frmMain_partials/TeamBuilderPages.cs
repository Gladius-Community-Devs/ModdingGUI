namespace ModdingGUI
{
    /// <summary>
    /// Converts the Team Builder pages from fixed-position group boxes to the card layout the other pages use.
    /// </summary>
    /// <remarks>
    /// These pages are built in code (<c>TeamBuilder.cs</c>) rather than the designer, but each one is still a stack of
    /// <see cref="GroupBox"/>es around nested table layouts. Rather than rewrite five pages of logic, the groups are
    /// replaced in place: the card takes the group's cell, the group's children move into the card body, and every
    /// control keeps its name and handlers. Rows are sized from the content's preferred height, so nothing is guessed.
    /// </remarks>
    internal static class TeamBuilderPages
    {
        /// <summary>Team Builder pages whose group boxes are converted.</summary>
        private static readonly string[] Pages =
        {
            "tabTeamBuilder", "tabTeamRulesSelection", "tabTeamUnitSelection",
            "tabTeamGearSelection", "tabTeamSkillSelection",
        };

        public static void Build(Control root)
        {
            foreach (string name in Pages)
            {
                if (root.Controls.Find(name, true).FirstOrDefault() is not TabPage page) continue;

                ConvertGroups(root, page);
                UiTheme.Apply(page);
            }
        }

        private static void ConvertGroups(Control root, TabPage page)
        {
            int pad = UiTheme.Space(page, 3);

            foreach (GroupBox group in Descendants(page).OfType<GroupBox>().Where(g => g.Visible).ToArray())
            {
                if (group.Parent is not TableLayoutPanel host) continue;

                int row = host.GetRow(group);
                int column = host.GetColumn(group);

                var card = UiKit.Card("card" + group.Name, group.Text);
                var body = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(pad, 0, pad, UiTheme.Space(page, 2)),
                };

                // Measure before detaching: PreferredSize is computed from the children, so reading it after they move
                // reports the empty group and the card comes out short (rows clipped).
                int contentHeight = 0;
                foreach (Control child in group.Controls) contentHeight += child.PreferredSize.Height;
                if (contentHeight <= 0) contentHeight = Math.Max(group.DisplayRectangle.Height, UiTheme.Scale(page, 40));

                // The group holds one nested table that carries the rows; it moves across with its layout intact.
                foreach (Control child in group.Controls.Cast<Control>().ToArray())
                {
                    group.Controls.Remove(child);
                    child.Dock = DockStyle.Fill;
                    child.Margin = new Padding(0);
                    body.Controls.Add(child);
                }

                UiKit.Body(card, body);

                int height = UiKit.HeaderHeight(page, hasCaption: false) + contentHeight + UiTheme.Space(page, 2);

                host.RowStyles[row] = new RowStyle(SizeType.Absolute, height);
                host.Controls.Remove(group);
                group.Visible = false;

                card.Height = height;
                host.Controls.Add(card, column, row);
            }
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;

                foreach (Control deeper in Descendants(child)) yield return deeper;
            }
        }
    }
}
