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
    /// <c>selectNode</c>, <c>screenshot</c>, <c>log</c>, <c>openTeamBuilder</c>, <c>clickMenuItem</c>, <c>quit</c>, and
    /// the waiting commands <c>jobs</c>, <c>waitForJob</c>, <c>waitForLog</c> (which run off the UI thread).
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

                        case "capture":
                            return CaptureWindow(UiString(request, "path"));

                        case "window":
                            return PlaceWindow(request);

                        case "closeDialog":
                            return CloseDialog();

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

        /// <summary>
        /// Commands that must not run on the UI thread, because they wait: <c>jobs</c>, <c>waitForJob</c>,
        /// <c>waitForLog</c>.
        /// </summary>
        /// <remarks>
        /// Marshal one of these onto the UI thread and the wait freezes the window — the very repaint that shows the
        /// unpack progressing would stop. They touch no control, so they run on the driver's own thread against the
        /// job registry and the log, both of which are thread-safe.
        /// </remarks>
        internal bool TryExecuteBackgroundUiCommand(string requestJson, out string response)
        {
            response = string.Empty;

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(requestJson);
            }
            catch (JsonException)
            {
                return false;
            }

            using (document)
            {
                JsonElement request = document.RootElement;
                string command = UiString(request, "cmd") ?? string.Empty;

                try
                {
                    switch (command)
                    {
                        case "jobs":
                            response = UiOk(new
                            {
                                active = Jobs.Active.Select(DescribeJob).ToArray(),
                                recent = Jobs.Recent(10).Select(DescribeJob).ToArray(),
                            });
                            return true;

                        case "waitForJob":
                            response = WaitForJob(request);
                            return true;

                        case "waitForLog":
                            response = WaitForLog(request);
                            return true;

                        case "cancelJob":
                        {
                            string? cancelId = UiString(request, "id");
                            string? cancelKind = UiString(request, "kind");

                            ModdingGUI.Core.Jobs.BuildJob? target = string.IsNullOrWhiteSpace(cancelId)
                                ? Jobs.Latest(cancelKind)
                                : Jobs.Find(cancelId);

                            if (target is null) response = UiFail("no matching job");
                            else if (target.IsFinished) response = UiFail($"job {target.Id} already finished");
                            else
                            {
                                target.Cancel();
                                response = UiOk(new { cancelled = target.Id, kind = target.Kind });
                            }

                            return true;
                        }

                        default:
                            return false;
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                {
                    response = UiFail(ex.Message);
                    return true;
                }
            }
        }

        private static object DescribeJob(ModdingGUI.Core.Jobs.BuildJob job) => new
        {
            id = job.Id,
            kind = job.Kind,
            target = job.Target,
            state = job.State.ToString(),
            elapsedSeconds = Math.Round(job.Elapsed.TotalSeconds, 1),
            exitCode = job.Result?.ExitCode,
            failedStep = job.Result?.FirstFailure?.Name,
            steps = job.Result?.Steps
                .Select(step => new
                {
                    name = step.Name,
                    exitCode = step.ExitCode,
                    seconds = Math.Round(step.Duration.TotalSeconds, 1),
                    lines = step.OutputLines,
                })
                .ToArray(),
        };

        /// <summary>
        /// Wait for a job to finish — by id, or the latest of a kind — and answer with its result.
        /// </summary>
        /// <remarks>
        /// This is the replacement for polling the log for a phrase: the response arrives when the work ends, with the
        /// exit code, the failed step's name and the duration. A timeout is still accepted, but a caller that does not
        /// pass one waits as long as the unpack takes.
        /// </remarks>
        private string WaitForJob(JsonElement request)
        {
            string? id = UiString(request, "id");
            string? kind = UiString(request, "kind");

            ModdingGUI.Core.Jobs.BuildJob? job = string.IsNullOrWhiteSpace(id) ? Jobs.Latest(kind) : Jobs.Find(id);
            if (job is null)
            {
                return UiFail(string.IsNullOrWhiteSpace(id)
                    ? $"no '{kind ?? "job"}' has run yet"
                    : $"no job with id '{id}'");
            }

            TimeSpan timeout = request.TryGetProperty("timeoutMs", out JsonElement timeoutValue) && timeoutValue.GetInt32() > 0
                ? TimeSpan.FromMilliseconds(timeoutValue.GetInt32())
                : TimeSpan.FromMinutes(30);

            if (!job.Completion.Wait(timeout))
            {
                return UiFail($"job {job.Id} ({job.Kind}) is still running after {timeout.TotalSeconds:0}s");
            }

            return UiOk(new
            {
                job = DescribeJob(job),
                succeeded = job.Result?.Succeeded ?? false,
                firstFailure = job.Result?.FirstFailure?.Name,
            });
        }

        /// <summary>
        /// Wait for a line of the log to appear. The fallback for a flow that has no job object (a UI-only action
        /// such as loading a roster), and a way to assert on what a run printed.
        /// </summary>
        private string WaitForLog(JsonElement request)
        {
            string text = UiString(request, "text") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return UiFail("waitForLog needs text");

            TimeSpan timeout = request.TryGetProperty("timeoutMs", out JsonElement timeoutValue) && timeoutValue.GetInt32() > 0
                ? TimeSpan.FromMilliseconds(timeoutValue.GetInt32())
                : TimeSpan.FromMinutes(30);

            var found = new TaskCompletionSource<ModdingGUI.Core.Logging.LogEntry>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            void OnWritten(object? sender, ModdingGUI.Core.Logging.LogEntry entry)
            {
                if (entry.Message.Contains(text, StringComparison.OrdinalIgnoreCase)) found.TrySetResult(entry);
            }

            // Subscribe first, then look at the tail: a line written in between would otherwise be missed.
            Output.Written += OnWritten;
            try
            {
                var already = Output.Tail(2000)
                    .Where(entry => entry.Message.Contains(text, StringComparison.OrdinalIgnoreCase))
                    .Select(entry => (ModdingGUI.Core.Logging.LogEntry?)entry)
                    .LastOrDefault();

                if (already is not null)
                {
                    return UiOk(new
                    {
                        matched = already.Value.Message,
                        level = already.Value.Level.ToString(),
                        waitedSeconds = 0,
                    });
                }

                if (!found.Task.Wait(timeout))
                {
                    return UiFail($"no log line containing '{text}' within {timeout.TotalSeconds:0}s");
                }

                var entry = found.Task.Result;
                return UiOk(new { matched = entry.Message, level = entry.Level.ToString() });
            }
            finally
            {
                Output.Written -= OnWritten;
            }
        }

        private string Snapshot()
        {
            var controls = new List<object>();

            // Control.Visible is the native window flag and lags a programmatic SelectedTab change, so it reported the
            // *previous* tab's page as the visible one and the new page as hidden. Walk the selected-tab chain instead:
            // a control is on screen only if every ancestor is visible AND every TabPage ancestor is the selected page
            // of its TabControl. Without this a caller cannot tell what the user is looking at.
            static bool OnScreen(Control control)
            {
                for (Control? cursor = control; cursor is not null; cursor = cursor.Parent)
                {
                    if (cursor is TabPage page)
                    {
                        if (page.Parent is not TabControl tabs || !ReferenceEquals(tabs.SelectedTab, page)) return false;
                    }

                    if (cursor is TabControl tabControl && !ReferenceEquals(tabControl.SelectedTab, null))
                    {
                        // the page check above covers it; a TabControl itself is visible with its parent
                    }
                }

                return control.Visible;
            }

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
                            ["visible"] = OnScreen(child),
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

            // A click may start a job (Unpack, Pack). Report its id so a script can wait on *that* job rather than
            // guessing which one "the latest" was.
            var before = Jobs.Latest();

            switch (control)
            {
                case Button button:
                    if (!button.Enabled) return UiFail($"'{name}' is disabled");
                    button.PerformClick();
                    return Clicked(name, before);

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

        /// <summary>Answer a click, naming the job it started when it started one.</summary>
        private string Clicked(string name, ModdingGUI.Core.Jobs.BuildJob? before)
        {
            var after = Jobs.Latest();
            if (after is not null && !ReferenceEquals(after, before))
            {
                return UiOk(new { clicked = name, job = DescribeJob(after) });
            }

            return UiOk(new { clicked = name });
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

        /// <summary>
        /// Move and size the window — onto a monitor, or to explicit bounds.
        /// </summary>
        /// <remarks>
        /// Exists so an automated session can keep the app on a screen the user is not working on, and so a layout
        /// check can be repeated at several sizes. <c>monitor</c> takes a device name (<c>\\.\DISPLAY2</c>) or an index
        /// into <see cref="Screen.AllScreens"/>; the window is fitted to that monitor's working area unless a size is
        /// given.
        /// </remarks>
        private string PlaceWindow(JsonElement request)
        {
            int? width = request.TryGetProperty("width", out JsonElement w) ? w.GetInt32() : null;
            int? height = request.TryGetProperty("height", out JsonElement h) ? h.GetInt32() : null;

            int? x = request.TryGetProperty("x", out JsonElement px) ? px.GetInt32() : null;
            int? y = request.TryGetProperty("y", out JsonElement py) ? py.GetInt32() : null;

            string? monitor = UiString(request, "monitor");
            if (!string.IsNullOrWhiteSpace(monitor))
            {
                Screen? screen = Screen.AllScreens.FirstOrDefault(candidate =>
                                   candidate.DeviceName.Equals(monitor, StringComparison.OrdinalIgnoreCase))
                               ?? (int.TryParse(monitor, out int index) && index >= 0 && index < Screen.AllScreens.Length
                                   ? Screen.AllScreens[index]
                                   : null);

                if (screen is null)
                {
                    return UiFail($"no monitor '{monitor}'; have " +
                                  string.Join(", ", Screen.AllScreens.Select(s => s.DeviceName)));
                }

                Rectangle area = screen.WorkingArea;
                x = area.X;
                y = area.Y;
                width ??= area.Width;
                height ??= area.Height;
            }

            if (x is null || y is null) return UiFail("window needs x/y, or a monitor");

            StartPosition = FormStartPosition.Manual;
            Location = new Point(x.Value, y.Value);
            if (width is not null && height is not null) Size = new Size(width.Value, height.Value);

            return UiOk(new
            {
                x = Location.X,
                y = Location.Y,
                width = Width,
                height = Height,
                monitor = Screen.FromControl(this).DeviceName,
            });
        }

        /// <summary>
        /// Grab the window from the screen — what the user actually sees, occlusion, cursor and all.
        /// </summary>
        /// <remarks>
        /// <see cref="Screenshot"/> renders this form's own control tree with <c>DrawToBitmap</c>, which can paint a
        /// control the compositor is not showing (a panel behind a tab, a child that has not repainted). When those
        /// two disagree, the painting is the liar and this is the evidence: it is a copy of the desktop.
        /// </remarks>
        private string CaptureWindow(string? path)
        {
            string target = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Path.GetTempPath(), "gladius-ui", $"capture-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png")
                : Environment.ExpandEnvironmentVariables(path);

            string? directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            Rectangle bounds = Bounds;
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
            }

            bitmap.Save(target, ImageFormat.Png);

            return UiOk(new { path = target, x = bounds.X, y = bounds.Y, width = bounds.Width, height = bounds.Height });
        }

        /// <summary>
        /// Close whatever dialog is on top: a WinForms dialog this form opened, or a native message box.
        /// </summary>
        /// <remarks>
        /// A modal dialog blocks the caller that opened it, so a command that triggers one never returns until someone
        /// presses a button and an automated run just looks hung. This is the escape hatch - and it reports what was
        /// blocking, which is how "switching to the Editors tab pops a project picker" was found.
        /// </remarks>
        private string CloseDialog()
        {
            Form? open = Application.OpenForms.Cast<Form>()
                .FirstOrDefault(form => !ReferenceEquals(form, this) && form.Visible);

            if (open is not null)
            {
                string title = open.Text;
                open.Close();
                Application.DoEvents();
                return UiOk(new { closed = title, kind = "form" });
            }

            IntPtr dialog = NativeDialogs.FindMessageBox((uint)Environment.ProcessId);
            if (dialog == IntPtr.Zero) return UiFail("no dialog is open");

            string text = NativeDialogs.TextOf(dialog);
            NativeDialogs.PostClose(dialog);
            Application.DoEvents();
            return UiOk(new { closed = text, kind = "messagebox" });
        }

        private string Screenshot(string? path)        {
            string target = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Path.GetTempPath(), "gladius-ui", $"shot-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png")
                : Environment.ExpandEnvironmentVariables(path);

            string? directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            // Force a repaint first: DrawToBitmap can reuse the previously painted page after a programmatic tab
            // switch, which made a "Packing" screenshot show the Editors content. `capture` is the ground truth.
            Refresh();
            Update();
            Application.DoEvents();

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
