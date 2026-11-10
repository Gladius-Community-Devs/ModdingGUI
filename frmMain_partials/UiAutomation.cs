using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace ModdingGUI
{
    /// <summary>
    /// The command set behind <see cref="UiDriver"/>: look at the window, press its buttons, fill its fields.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything here runs on the UI thread and goes through the same controls a user touches, so an automated run
    /// exercises the real code path — it cannot reach a Core API except by pressing the button that does. That is the
    /// whole point: an agent helping a user should drive the app, not reimplement it.
    /// </para>
    /// <para>
    /// Tested surface as of Phase 7 UX work: <c>ping</c>, <c>snapshot</c>, <c>click</c>, <c>set</c>, <c>selectTab</c>,
    /// <c>selectNode</c>, <c>screenshot</c>, <c>log</c>, <c>openTeamBuilder</c>, <c>clickMenuItem</c>, <c>quit</c>.
    /// </para>
    /// </remarks>
    public partial class frmMain
    {
        /// <summary>True while a driver is attached; dialogs are reported instead of blocking the automation.</summary>
        public static bool UiAutomationActive { get; internal set; }

        private const int UiSnapshotItemLimit = 40;

        /// <summary>Execute one JSON command and return one JSON response line.</summary>
        internal string ExecuteUiCommand(string requestJson)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(requestJson);
            }
            catch (JsonException ex)
            {
                return UiFail($"bad JSON: {ex.Message}");
            }

            using (document)
            {
                JsonElement request = document.RootElement;
                string command = UiString(request, "cmd") ?? string.Empty;

                try
                {
                    switch (command)
                    {
                        case "ping":
                            return UiOk(new { pong = true, tab = tabContainer.SelectedTab?.Name });

                        case "snapshot":
                            return Snapshot();

                        case "click":
                            return ClickControl(UiString(request, "name"));

                        case "set":
                            return SetControl(request);

                        case "selectTab":
                            return SelectTab(UiString(request, "name"));

                        case "selectNode":
                            return SelectTreeNode(UiString(request, "name"), UiString(request, "text"));

                        case "clickMenuItem":
                            return ClickMenuItem(UiString(request, "name"));

                        case "screenshot":
                            return Screenshot(UiString(request, "path"));

                        case "log":
                            return LogTail(request.TryGetProperty("count", out JsonElement count) ? count.GetInt32() : 50);

                        case "items":
                            return ListItems(request);

                        case "openTeamBuilder":
                            return OpenTeamBuilder(UiString(request, "project"));

                        case "quit":
                            BeginInvoke(new Action(Close));
                            return UiOk(new { closing = true });

                        default:
                            return UiFail($"unknown command '{command}'");
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException)
                {
                    return UiFail(ex.Message);
                }
            }
        }

        // ---- Commands ------------------------------------------------------------------------------------

        private string Snapshot()
        {
            var controls = new List<object>();

            void Walk(Control parent)
            {
                foreach (Control child in parent.Controls)
                {
                    if (!string.IsNullOrEmpty(child.Name))
                    {
                        Point origin;
                        try
                        {
                            origin = PointToClient(child.PointToScreen(Point.Empty));
                        }
                        catch (InvalidOperationException)
                        {
                            origin = child.Location;
                        }

                        var entry = new Dictionary<string, object?>
                        {
                            ["name"] = child.Name,
                            ["type"] = child.GetType().Name,
                            ["text"] = Shorten(child.Text, 200),
                            ["enabled"] = child.Enabled,
                            ["visible"] = child.Visible,
                            ["x"] = origin.X,
                            ["y"] = origin.Y,
                            ["w"] = child.Width,
                            ["h"] = child.Height,
                        };

                        switch (child)
                        {
                            case ListBox list:
                                entry["items"] = list.Items.Cast<object>()
                                    .Take(UiSnapshotItemLimit)
                                    .Select(item => Shorten(item?.ToString(), 120))
                                    .ToArray();
                                entry["itemCount"] = list.Items.Count;
                                entry["selectedIndex"] = list.SelectedIndex;
                                break;

                            case ComboBox combo:
                                entry["items"] = combo.Items.Cast<object>()
                                    .Take(UiSnapshotItemLimit)
                                    .Select(item => Shorten(item?.ToString(), 120))
                                    .ToArray();
                                entry["itemCount"] = combo.Items.Count;
                                entry["selectedIndex"] = combo.SelectedIndex;
                                break;

                            case CheckBox check:
                                entry["checked"] = check.Checked;
                                break;

                            case TreeView tree:
                                entry["nodes"] = tree.Nodes.Cast<TreeNode>()
                                    .Take(UiSnapshotItemLimit)
                                    .Select(node => node.Text)
                                    .ToArray();
                                break;

                            case TabControl tabs:
                                entry["tabs"] = tabs.TabPages.Cast<TabPage>()
                                    .Select(page => page.Name)
                                    .ToArray();
                                entry["selectedTab"] = tabs.SelectedTab?.Name;
                                break;
                        }

                        controls.Add(entry);
                    }

                    Walk(child);
                }
            }

            Walk(this);

            var log = Output.Tail(40)
                .Select(entry => new { level = entry.Level.ToString(), message = Shorten(entry.Message, 200) })
                .ToArray();

            return UiOk(new
            {
                title = Text,
                selectedTab = tabContainer.SelectedTab?.Name,
                width = Width,
                height = Height,
                controls,
                log,
            });
        }

        private string ClickControl(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return UiFail("click needs a name");

            Control? control = FindControl(name);
            if (control is null) return UiFail($"no control named '{name}'");

            switch (control)
            {
                case Button button:
                    if (!button.Enabled) return UiFail($"'{name}' is disabled");
                    button.PerformClick();
                    return UiOk(new { clicked = name });

                case CheckBox check:
                    check.Checked = !check.Checked;
                    return UiOk(new { clicked = name, @checked = check.Checked });

                case RadioButton radio:
                    radio.Checked = true;
                    return UiOk(new { clicked = name });

                default:
                    return UiFail($"'{name}' is a {control.GetType().Name}; use set, not click");
            }
        }

        private string SetControl(JsonElement request)
        {
            string? name = UiString(request, "name");
            if (string.IsNullOrWhiteSpace(name)) return UiFail("set needs a name");

            Control? control = FindControl(name);
            if (control is null) return UiFail($"no control named '{name}'");

            bool changed = false;

            if (request.TryGetProperty("text", out JsonElement text))
            {
                string value = text.GetString() ?? string.Empty;
                control.Text = value;
                changed = true;
            }

            if (request.TryGetProperty("checked", out JsonElement checkValue) && control is CheckBox checkBox)
            {
                checkBox.Checked = checkValue.GetBoolean();
                changed = true;
            }

            if (request.TryGetProperty("index", out JsonElement indexValue) && control is ComboBox combo)
            {
                int index = indexValue.GetInt32();
                if (index < -1 || index >= combo.Items.Count) return UiFail($"{name} has no index {index}");
                combo.SelectedIndex = index;
                changed = true;
            }

            if (request.TryGetProperty("index", out JsonElement listIndexValue) && control is ListBox listBox)
            {
                int index = listIndexValue.GetInt32();
                if (index < -1 || index >= listBox.Items.Count) return UiFail($"{name} has no index {index}");
                listBox.SelectedIndex = index;
                changed = true;
            }

            if (request.TryGetProperty("item", out JsonElement itemValue) && control is ComboBox comboItem)
            {
                string wanted = itemValue.GetString() ?? string.Empty;
                int index = comboItem.Items.Cast<object>()
                    .Select((candidate, i) => (candidate, i))
                    .Where(pair => Shorten(pair.candidate?.ToString(), 120)?.Contains(wanted, StringComparison.OrdinalIgnoreCase) == true)
                    .Select(pair => pair.i)
                    .DefaultIfEmpty(-1)
                    .First();
                if (index < 0) return UiFail($"{name} has no item containing '{wanted}'");
                comboItem.SelectedIndex = index;
                changed = true;
            }

            if (request.TryGetProperty("item", out JsonElement listItemValue) && control is ListBox listItem)
            {
                // Select by (partial) text so a scenario survives a list whose indexes moved.
                string wanted = listItemValue.GetString() ?? string.Empty;
                int index = listItem.Items.Cast<object>()
                    .Select((candidate, i) => (candidate, i))
                    .Where(pair => Shorten(pair.candidate?.ToString(), 200)?.Contains(wanted, StringComparison.OrdinalIgnoreCase) == true)
                    .Select(pair => pair.i)
                    .DefaultIfEmpty(-1)
                    .First();
                if (index < 0) return UiFail($"{name} has no item containing '{wanted}'");
                listItem.SelectedIndex = index;
                changed = true;
            }

            if (request.TryGetProperty("item", out JsonElement tabValue) && control is TabControl tabs)
            {
                string wanted = tabValue.GetString() ?? string.Empty;
                TabPage? page = tabs.TabPages.Cast<TabPage>()
                    .FirstOrDefault(candidate => candidate.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase));
                if (page is null) return UiFail($"{name} has no tab '{wanted}'");
                tabs.SelectedTab = page;
                changed = true;
            }

            if (!changed) return UiFail("set needs text, checked, index or item");

            Application.DoEvents();
            return UiOk(new { set = name, text = Shorten(control.Text, 200) });
        }

        private string SelectTab(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return UiFail("selectTab needs a name");

            TabPage? page = tabContainer.TabPages.Cast<TabPage>()
                .FirstOrDefault(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (page is null) return UiFail($"no tab named '{name}'");

            tabContainer.SelectedTab = page;
            Application.DoEvents();
            return UiOk(new { selectedTab = page.Name });
        }

        private string SelectTreeNode(string? name, string? text)
        {
            if (string.IsNullOrWhiteSpace(name)) return UiFail("selectNode needs a name");
            if (FindControl(name) is not TreeView tree) return UiFail($"no tree named '{name}'");
            if (string.IsNullOrWhiteSpace(text)) return UiFail("selectNode needs text");

            TreeNode? match = FindNode(tree.Nodes, text);
            if (match is null) return UiFail($"no node containing '{text}'");

            tree.SelectedNode = match;
            tree.Focus();
            Application.DoEvents();
            return UiOk(new { selected = match.Text });
        }

        private static TreeNode? FindNode(TreeNodeCollection nodes, string text)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Text.Contains(text, StringComparison.OrdinalIgnoreCase)) return node;
                TreeNode? deeper = FindNode(node.Nodes, text);
                if (deeper is not null) return deeper;
            }

            return null;
        }

        private string ClickMenuItem(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return UiFail("clickMenuItem needs a name");

            ToolStripItem? item = FindMenuItem(name);
            if (item is null) return UiFail($"no menu item named '{name}'");

            if (item is ToolStripMenuItem menuItem)
            {
                menuItem.Checked = true;
                menuItem.PerformClick();
            }
            else
            {
                item.PerformClick();
            }

            Application.DoEvents();
            return UiOk(new { clicked = name });
        }

        private string Screenshot(string? path)
        {
            string target = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Path.GetTempPath(), "gladius-ui", $"shot-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png")
                : Environment.ExpandEnvironmentVariables(path);

            string? directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using var bitmap = new Bitmap(Width, Height);
            DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
            bitmap.Save(target, ImageFormat.Png);

            return UiOk(new { path = target, width = Width, height = Height });
        }

        private string LogTail(int count)
        {
            int take = Math.Clamp(count, 1, 2000);
            var entries = Output.Tail(take)
                .Select(entry => new { level = entry.Level.ToString(), source = entry.Source, message = entry.Message })
                .ToArray();

            return UiOk(new { count = entries.Length, entries });
        }

        /// <summary>
        /// Every item of a list or combo box, with its index. <c>snapshot</c> truncates at 40 so a transcript stays
        /// readable; this is how a caller reads the rest (and how it picks an index to select).
        /// </summary>
        private string ListItems(JsonElement request)
        {
            string? name = UiString(request, "name");
            if (string.IsNullOrWhiteSpace(name)) return UiFail("items needs a name");

            Control? control = FindControl(name);
            if (control is null) return UiFail($"no control named '{name}'");

            string[] source = control switch
            {
                ListBox list => list.Items.Cast<object>().Select(item => Shorten(item?.ToString(), 200)).ToArray(),
                ComboBox combo => combo.Items.Cast<object>().Select(item => Shorten(item?.ToString(), 200)).ToArray(),
                _ => Array.Empty<string>(),
            };

            if (control is not ListBox && control is not ComboBox)
            {
                return UiFail($"'{name}' is a {control.GetType().Name}, not a list");
            }

            int offset = request.TryGetProperty("offset", out JsonElement offsetValue) ? Math.Max(0, offsetValue.GetInt32()) : 0;
            int count = request.TryGetProperty("count", out JsonElement countValue) ? Math.Clamp(countValue.GetInt32(), 1, 5000) : 200;

            var page = source.Skip(offset).Take(count)
                .Select((text, index) => new { index = offset + index, text })
                .ToArray();

            return UiOk(new { total = source.Length, offset, items = page });
        }

        /// <summary>
        /// Select a project and open the Team Builder tab, exactly as the menu does. The class load is asynchronous,
        /// so callers should <c>wait</c> before asserting on the class list.
        /// </summary>
        private string OpenTeamBuilder(string? project)
        {
            if (!string.IsNullOrWhiteSpace(project))
            {
                // A relative path is relative to the app, because that is where projects live.
                string resolved = Path.IsPathRooted(project)
                    ? project
                    : Path.GetFullPath(Path.Combine(GetAppDirectory(), project));

                if (!Directory.Exists(resolved)) return UiFail($"project not found: {resolved}");

                txtPackPath.Text = resolved;
                txtRandomizerPath.Text = resolved;
            }

            if (string.IsNullOrWhiteSpace(txtPackPath.Text) || !Directory.Exists(txtPackPath.Text))
            {
                return UiFail("no project selected; pass one in the command");
            }

            teamBuilderToolStripMenuItem.Checked = true;
            if (!tabContainer.TabPages.Contains(tabTeamBuilder)) tabContainer.TabPages.Add(tabTeamBuilder);
            tabContainer.SelectedTab = tabTeamBuilder;

            LoadClassesIntoDropdown();
            InitializeTeamBuilderUI();
            Application.DoEvents();

            return UiOk(new { tab = tabTeamBuilder.Name, project = txtPackPath.Text });
        }

        // ---- Lookup / plumbing ---------------------------------------------------------------------------

        /// <summary>
        /// A yes/no question from the shell. Under automation a modal would deadlock the driver (it owns the UI
        /// thread), so the answer is logged and taken as yes rather than silently hanging the run.
        /// </summary>
        internal bool UiConfirm(string message, string title)
        {
            if (UiAutomationActive)
            {
                AppendLog($"[automation] {title}: {message} → yes", Color.Gray);
                return true;
            }

            return MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        /// <summary>
        /// An informational popup. Under automation a modal would deadlock the driver (it owns the UI thread), so the
        /// message goes to the log instead — which is where a script is reading anyway.
        /// </summary>
        internal void UiAlert(string message, string title)
        {
            if (UiAutomationActive)
            {
                AppendLog($"[automation] {title}: {message}", Color.Black);
                return;
            }

            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private Control? FindControl(string name)
        {
            Control? Match(Control parent)
            {
                foreach (Control child in parent.Controls)
                {
                    if (child.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return child;

                    Control? deeper = Match(child);
                    if (deeper is not null) return deeper;
                }

                return null;
            }

            return Match(this);
        }

        private ToolStripItem? FindMenuItem(string name)
        {
            IEnumerable<ToolStripItem> Flatten(ToolStripItemCollection items)
            {
                foreach (ToolStripItem item in items)
                {
                    yield return item;

                    if (item is ToolStripDropDownItem dropdown)
                    {
                        foreach (ToolStripItem child in Flatten(dropdown.DropDownItems)) yield return child;
                    }
                }
            }

            foreach (Control control in Controls)
            {
                if (control is not MenuStrip menu) continue;

                foreach (ToolStripItem item in Flatten(menu.Items))
                {
                    if (item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return item;
                }
            }

            return null;
        }

        private static string? UiString(JsonElement element, string property) =>
            element.TryGetProperty(property, out JsonElement value) ? value.GetString() : null;

        private static string Shorten(string? value, int limit)
        {
            if (value is null) return string.Empty;
            string single = value.Replace('\r', ' ').Replace('\n', ' ');
            return single.Length <= limit ? single : single[..limit] + "…";
        }

        internal static string UiOk(object payload) => JsonSerializer.Serialize(new { ok = true, result = payload });

        internal static string UiFail(string message) => JsonSerializer.Serialize(new { ok = false, error = message });
    }
}
