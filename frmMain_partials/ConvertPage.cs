namespace ModdingGUI
{
    using ModdingGUI.Core.Discs;

    /// <summary>
    /// The Convert page: turn any supported disc image into a plain ISO, or into a Dolphin container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Users arrive here with an RVZ/WIA/GCZ/CISO/NKit file and want an ISO they can unpack or patch. Before this page
    /// existed they had to find an external tool themselves; the app already knows how to drive Dolphin's CLI and the
    /// bundled NKit converter, so the conversion belongs in the app.
    /// </para>
    /// <para>
    /// The target format is a row of tinted toggle buttons rather than a ComboBox: a WinForms DropDownList does not take
    /// the palette (its closed box is painted by the system), and the Randomizer already established this pattern.
    /// </para>
    /// <para>
    /// Every control is named so the UI driver - and therefore an agent - can drive the page: see <c>convert</c> in
    /// docs/UI_AUTOMATION.md.
    /// </para>
    /// </remarks>
    public partial class frmMain
    {
        private readonly Dictionary<DiscImageTarget, CheckBox> _convertTargetToggles = new();

        private TextBox _txtConvertSource = null!;
        private TextBox _txtConvertDestination = null!;
        private Button _btnConvertRun = null!;
        private Button _btnConvertOpenFolder = null!;
        private Label _lblConvertDetected = null!;
        private RichTextBox _rtbConvertOutput = null!;

        private DiscImageFormat _convertSourceFormat = DiscImageFormat.Unknown;
        private DiscImageTarget _convertTarget = DiscImageTarget.Iso;
        private string? _lastConvertOutput;

        /// <summary>Build the Convert tab and page. Called once from the constructor.</summary>
        private void BuildConvertPage()
        {
            if (Controls.Find("tabContainer", true).FirstOrDefault() is not TabControl tabs) return;
            if (tabs.TabPages.ContainsKey("tabConvert")) return;

            var page = new TabPage("Convert") { Name = "tabConvert", BackColor = UiTheme.Surface };
            tabs.TabPages.Add(page);

            int pad = UiTheme.Space(page, 3);
            page.Padding = new Padding(pad);

            var card = UiKit.Card("cardConvert", "Convert a disc image",
                "Rebuild an RVZ, WIA, GCZ, CISO or NKit image as an ISO the tools can read - or compress an ISO for storage.");
            var body = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };
            var form = UiKit.Form(card, 112);

            _txtConvertSource = new TextBox { Name = "txtConvertSource", ReadOnly = true };
            var browseSource = UiKit.Browse(page, "btnConvertBrowseSource", (_, _) => BrowseConvertSource());
            UiKit.RowWithButton(form, page, "Source image", _txtConvertSource, browseSource);

            _lblConvertDetected = new Label
            {
                Name = "lblConvertDetected",
                Text = "Pick an image to see what it is.",
                AutoSize = false,
                Height = UiKit.FieldHeight(page),
                ForeColor = UiTheme.InkMuted,
                BackColor = UiTheme.Surface,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            UiKit.Row(form, page, "Detected", _lblConvertDetected);

            var targets = new FlowLayoutPanel
            {
                Name = "pnlConvertTargets",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0),
            };

            foreach (DiscImageTarget target in new[] { DiscImageTarget.Iso, DiscImageTarget.Rvz, DiscImageTarget.Wia, DiscImageTarget.Gcz })
            {
                string label = target.ToString().ToUpperInvariant();
                DiscImageTarget choice = target;

                // A checkbox drawn as a button, like the Randomizer's options: the only control shape in the kit that
                // takes the palette in both states.
                var toggle = new CheckBox
                {
                    Name = "chkConvertTarget" + label,
                    Text = label,
                    Appearance = Appearance.Button,
                    FlatStyle = FlatStyle.Flat,
                    AutoSize = false,
                    Width = UiTheme.Scale(page, 72),
                    Height = UiKit.FieldHeight(page),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = new Padding(0, 0, UiTheme.Space(page, 2), 0),
                };

                toggle.CheckedChanged += (_, _) =>
                {
                    if (toggle.Checked) SetConvertTarget(choice);
                    else if (_convertTarget == choice) toggle.Checked = true;   // one is always chosen
                };

                UiTheme.TintToggle(toggle);
                _convertTargetToggles[target] = toggle;
                targets.Controls.Add(toggle);
            }

            UiKit.Row(form, page, "Convert to", targets);

            _txtConvertDestination = new TextBox { Name = "txtConvertDestination" };
            var browseDestination = UiKit.Browse(page, "btnConvertBrowseDestination", (_, _) => BrowseConvertDestination());
            UiKit.RowWithButton(form, page, "Destination", _txtConvertDestination, browseDestination);

            var actions = new FlowLayoutPanel
            {
                Name = "pnlConvertActions",
                Dock = DockStyle.Bottom,
                Height = UiTheme.Scale(page, 40),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = UiTheme.Surface,
                Padding = new Padding(0),
            };

            _btnConvertRun = UiKit.Primary(page, "btnConvertRun", "Convert", (_, _) => RunConvertFromPage());
            _btnConvertOpenFolder = UiKit.Secondary(page, "btnConvertOpenFolder", "Open folder", (_, _) => OpenConvertFolder());
            _btnConvertOpenFolder.Enabled = false;
            actions.Controls.Add(_btnConvertRun);
            actions.Controls.Add(_btnConvertOpenFolder);

            body.Controls.Add(form);
            body.Controls.Add(actions);
            UiKit.Body(card, body);

            var logCard = UiKit.Card("cardConvertLog", "Activity");
            _rtbConvertOutput = new RichTextBox { Name = "rtbConvertOutput", ReadOnly = true, BorderStyle = BorderStyle.None };
            UiKit.Style(_rtbConvertOutput);
            _rtbConvertOutput.Dock = DockStyle.Fill;
            _rtbConvertOutput.Margin = new Padding(0);

            var logBody = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Padding = new Padding(pad, 0, pad, UiTheme.Space(page, 2)),
            };
            logBody.Controls.Add(_rtbConvertOutput);
            UiKit.Body(logCard, logBody);

            var columns = new TableLayoutPanel
            {
                Name = "tblConvertColumns",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0),
            };
            columns.RowStyles.Add(new RowStyle(SizeType.Absolute, UiKit.CardHeight(page, formRows: 4)));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            columns.Controls.Add(card, 0, 0);
            columns.Controls.Add(logCard, 0, 1);
            page.Controls.Add(columns);

            UiTheme.Apply(page);
            SetConvertTarget(DiscImageTarget.Iso);

            // After the form is up: logging into a RichTextBox that is not on screen yet silently drops the text, and
            // BeginInvoke throws until the handle exists (this runs from the constructor).
            void Announce() => ConvertLog("Convert page ready. " + DescribeConvertAvailability(), LogKind.Info);
            if (IsHandleCreated) BeginInvoke(new Action(Announce));
            else Load += (_, _) => Announce();
        }

        private enum LogKind { Info, Success, Warning, Error }

        private void ConvertLog(string message, LogKind kind)
        {
            Color colour = kind switch
            {
                LogKind.Success => UiTheme.Success,
                LogKind.Warning => UiTheme.Warning,
                LogKind.Error => UiTheme.Error,
                _ => UiTheme.Info,
            };

            AppendLog(message, colour, _rtbConvertOutput);
        }

        private string DescribeConvertAvailability()
        {
            DiscImageConverter converter = CreateConverter();
            var targets = converter.AvailableTargets();

            string dolphin = converter.HasDolphinTool ? "Dolphin CLI found" : "no Dolphin CLI configured";
            string nkit = converter.HasNkitTool ? "NKit converter bundled" : "no NKit converter";

            return $"({dolphin}; {nkit}; targets: {(targets.Count == 0 ? "none" : string.Join(", ", targets))})";
        }

        private DiscImageConverter CreateConverter()
        {
            string appDirectory = GetAppDirectory();
            string? dolphinTool = ResolveDiscImageConverter();

            return new DiscImageConverter(dolphinTool, DiscImageConverter.NkitFolderIn(appDirectory), _log);
        }

        private void BrowseConvertSource()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Choose a disc image",
                Filter = "Disc images (*.iso;*.gcm;*.nkit.iso;*.rvz;*.wia;*.gcz;*.ciso)|*.iso;*.gcm;*.nkit.iso;*.rvz;*.wia;*.gcz;*.ciso|All files (*.*)|*.*",
            };

            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            LoadConvertSource(dialog.FileName);
        }

        /// <summary>Point the page at a source image, detect it and suggest a destination.</summary>
        private void LoadConvertSource(string path)
        {
            _txtConvertSource.Text = path;
            DetectConvertSource();
            UpdateConvertDestination();
        }

        private void DetectConvertSource()
        {
            string path = _txtConvertSource.Text;
            _convertSourceFormat = DiscImageFormat.Unknown;

            if (!File.Exists(path))
            {
                _lblConvertDetected.Text = "Pick an image to see what it is.";
                return;
            }

            try
            {
                _convertSourceFormat = DiscImageProbe.Detect(path);
            }
            catch (IOException ex)
            {
                _lblConvertDetected.Text = "Unreadable: " + ex.Message;
                return;
            }

            string summary = DiscImageProbe.Describe(_convertSourceFormat);
            _lblConvertDetected.Text = summary;
            ConvertLog($"'{Path.GetFileName(path)}' is {summary}.", LogKind.Info);

            // NKit only ever produces an ISO, so say so rather than failing after the fact.
            if (_convertSourceFormat == DiscImageFormat.Nkit)
            {
                SetConvertTarget(DiscImageTarget.Iso);
                ConvertLog("NKit images are rebuilt as an ISO; compress the result afterwards if you want RVZ.", LogKind.Warning);
            }
        }

        private DiscImageTarget SelectedConvertTarget() => _convertTarget;

        private void SetConvertTarget(DiscImageTarget target)
        {
            _convertTarget = target;

            foreach ((DiscImageTarget candidate, CheckBox toggle) in _convertTargetToggles)
            {
                toggle.Checked = candidate == target;
                UiTheme.TintToggle(toggle);
            }

            UpdateConvertDestination();
        }

        private void UpdateConvertDestination()
        {
            string source = _txtConvertSource.Text;
            if (!File.Exists(source)) return;

            _txtConvertDestination.Text = DefaultConvertDestination(source, _convertTarget);
        }

        /// <summary>
        /// Where a conversion should land by default: beside the source, same stem, target extension. Converting to the
        /// format the file already is would otherwise suggest the source path itself, which the converter refuses.
        /// </summary>
        private static string DefaultConvertDestination(string sourcePath, DiscImageTarget target)
        {
            string folder = Path.GetDirectoryName(sourcePath) ?? string.Empty;

            string name = Path.GetFileName(sourcePath);
            foreach (string known in new[] { ".nkit.iso", ".iso", ".gcm", ".rvz", ".wia", ".gcz", ".ciso" })
            {
                if (!name.EndsWith(known, StringComparison.OrdinalIgnoreCase)) continue;
                name = name[..^known.Length];
                break;
            }

            string candidate = Path.Combine(folder, name + DiscImageConverter.ExtensionFor(target));

            // Converting RVZ to RVZ would otherwise suggest the source itself, which the converter refuses.
            if (string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
            {
                candidate = Path.Combine(folder, name + "-converted" + DiscImageConverter.ExtensionFor(target));
            }

            return candidate;
        }

        private void BrowseConvertDestination()
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Where should the converted image go?",
                Filter = _convertTarget switch
                {
                    DiscImageTarget.Rvz => "RVZ (*.rvz)|*.rvz",
                    DiscImageTarget.Wia => "WIA (*.wia)|*.wia",
                    DiscImageTarget.Gcz => "GCZ (*.gcz)|*.gcz",
                    _ => "ISO (*.iso)|*.iso",
                },
                FileName = Path.GetFileName(_txtConvertDestination.Text),
            };

            string? folder = Path.GetDirectoryName(_txtConvertDestination.Text);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)) dialog.InitialDirectory = folder;

            if (dialog.ShowDialog(this) == DialogResult.OK) _txtConvertDestination.Text = dialog.FileName;
        }

        private async void RunConvertFromPage()
        {
            await RunConvertAsync(_txtConvertSource.Text, _txtConvertDestination.Text, _convertTarget);
        }

        /// <summary>Run one conversion, logging and reporting the outcome.</summary>
        private async Task<DiscImageConversion> RunConvertAsync(string sourcePath, string destinationPath, DiscImageTarget target)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                ConvertLog("Choose a source image first.", LogKind.Warning);
                return new DiscImageConversion(false, null, "No source image.");
            }

            _btnConvertRun.Enabled = false;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                ConvertLog($"Converting '{Path.GetFileName(sourcePath)}' to {target}...", LogKind.Info);
                DiscImageConverter converter = CreateConverter();

                DiscImageConversion result = await Task.Run(() => converter.Convert(sourcePath, destinationPath, target));
                watch.Stop();

                if (result.Ok)
                {
                    _lastConvertOutput = result.OutputPath;
                    ConvertLog($"{result.Message} ({watch.Elapsed.TotalSeconds:0.0}s) -> {result.OutputPath}", LogKind.Success);
                    _btnConvertOpenFolder.Enabled = result.OutputPath is not null;
                }
                else
                {
                    ConvertLog(result.Message, LogKind.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                ConvertLog($"Conversion failed: {ex.Message}", LogKind.Error);
                return new DiscImageConversion(false, null, ex.Message);
            }
            finally
            {
                _btnConvertRun.Enabled = true;
            }
        }

        private void OpenConvertFolder()
        {
            string? folder = _lastConvertOutput is not null
                ? Path.GetDirectoryName(_lastConvertOutput)
                : Path.GetDirectoryName(_txtConvertDestination.Text);

            if (folder is null || !Directory.Exists(folder))
            {
                ConvertLog("No destination folder to open yet.", LogKind.Warning);
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
    }
}
