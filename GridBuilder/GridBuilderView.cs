using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D; // Required for InterpolationMode and RotateFlipType
using System.Collections.Generic; // For Dictionary

namespace ModdingGUI
{
    public partial class GridBuilderView : UserControl, IGridContext
    {
        /* ───── IGridContext ─────────────────────────────────────────── */
        public GridFile CurrentGrid { get; private set; } = new GridFile();
        public int CurrentDrawMask { get; private set; } = 1 << 0;

        // Map Display Properties to be passed to GridPanel via IGridContext
        public Point MapDisplayOffset { get; private set; } = Point.Empty;
        public float MapDisplayScale { get; private set; } = 1.0f;
        public Image? CurrentMapArenaImage => _currentArenaMapImage;
        public RotateFlipType MapDisplayRotation { get; private set; } = RotateFlipType.RotateNoneFlipNone;


        public string GetFriendlyNameForBit(int bitIndex)
        {
            if (CurrentGrid == null || bitIndex < 0 || bitIndex >= GridFile.NumSlots)
            {
                return $"Slot {bitIndex}"; // Fallback
            }
            string tagName = CurrentGrid.GetTagNameForSlot(bitIndex);
            if (tagName == GridFile.NO_VALUE || string.IsNullOrEmpty(tagName))
            {
                if (CurrentGrid.IsSlotActive(bitIndex))
                {
                    return $"Slot {bitIndex} (Active, Unnamed)";
                }
                return $"Slot {bitIndex} (Inactive)";
            }
            return tagName;
        }

        /* ───── child controls ───────────────────────────────────────── */
        private readonly GridPanel _panel = new GridPanel();
        private readonly ToolStripComboBox _paintSlotBox = new ToolStripComboBox();
        private readonly ToolStripComboBox _arenaBox = new ToolStripComboBox();
        private readonly ToolStripLabel _hoverLbl = new ToolStripLabel();
        private readonly ToolStripButton _btnSave = new ToolStripButton("Save");
        private ToolStripButton _btnToggleMap;

        private CheckBox _chkVerboseLoggingRegular;
        private ToolStripControlHost _chkVerboseLoggingHost;

        private NumericUpDown _mapOffsetXNumeric = new NumericUpDown();
        private NumericUpDown _mapOffsetYNumeric = new NumericUpDown();
        private NumericUpDown _mapScaleNumeric = new NumericUpDown();
        private ToolStripComboBox _mapRotationBox = new ToolStripComboBox();

        private ToolStripControlHost _mapOffsetXHost;
        private ToolStripControlHost _mapOffsetYHost;
        private ToolStripControlHost _mapScaleHost;
        private ToolStripLabel _mapXLabel;
        private ToolStripLabel _mapYLabel;
        private ToolStripLabel _mapScaleLabel;
        private ToolStripLabel _mapRotLabel;
        private ToolStripSeparator _mapControlsSeparator;

        /* ───── Internal Logging Control ─────────────────────────────── */
        private RichTextBox _internalLogRtb = new RichTextBox();
        private SplitContainer _mainSplitContainer = new SplitContainer();

        /* ───── NEW: Controls for Commands/Help Display ──────────────── */
        private RichTextBox _commandsHelpRtb;
        private SplitContainer _lowerSplitContainer; // To host commands and log

        /* ───── state ────────────────────────────────────────────────── */
        private string? _projectRoot;
        private string? _currentArenaPath;
        private Image? _currentArenaMapImage = null;
        private string? _currentMapImagePath = null;

        private struct MapTransformDefaults
        {
            public int OffsetX { get; set; }
            public int OffsetY { get; set; }
            public decimal Scale { get; set; }
            public int RotationIndex { get; set; }
        }

        private readonly Dictionary<string, MapTransformDefaults> _mapDefaultTransforms = new Dictionary<string, MapTransformDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            { "expansedunes", new MapTransformDefaults { OffsetX = 0, OffsetY = 0, Scale = 2.5M, RotationIndex = 0 } },
            { "pirgosarena", new MapTransformDefaults { OffsetX = 50, OffsetY = -75, Scale = 2.5M, RotationIndex = 2 } },
            { "expanseoasis", new MapTransformDefaults { OffsetX = 0, OffsetY = 0, Scale = 2.5M, RotationIndex = 0 } },
            { "expanseshore", new MapTransformDefaults { OffsetX = 0, OffsetY = -300, Scale = 2.5M, RotationIndex = 0 } },
            { "offeringplate", new MapTransformDefaults { OffsetX = 150, OffsetY = -125, Scale = 2.4M, RotationIndex = 0 } },
            { "palaceibliis", new MapTransformDefaults { OffsetX = 40, OffsetY = 0, Scale = 2.5M, RotationIndex = 0 } },
            { "scorchedoasis", new MapTransformDefaults { OffsetX = 125, OffsetY = 25, Scale = 2.4M, RotationIndex = 2 } },
            { "belfortarena", new MapTransformDefaults { OffsetX = 75, OffsetY = -100, Scale = 2.6M, RotationIndex = 2 } },
            { "bloodyhalo", new MapTransformDefaults { OffsetX = 75, OffsetY = -75, Scale = 2.5M, RotationIndex = 2 } },
            { "caltharena", new MapTransformDefaults { OffsetX = 75, OffsetY = 37, Scale = 2.4M, RotationIndex = 2 } },
        };


        public GridBuilderView()
        {
            InitializeComponent();
            BuildUi();
            UpdatePaintSlotBox();
            AppendLog($"MapDefaultTransforms dictionary contains {_mapDefaultTransforms.Count} entries.", "DimGray", isDetail: true);
            if (_chkVerboseLoggingRegular.Checked)
            {
                foreach (var kvp in _mapDefaultTransforms)
                {
                    AppendLog($"  - Key: '{kvp.Key}', OffsetX: {kvp.Value.OffsetX}, OffsetY: {kvp.Value.OffsetY}, Scale: {kvp.Value.Scale}, RotIdx: {kvp.Value.RotationIndex}", "DimGray", isDetail: true);
                }
            }
        }

        private void AppendLog(string message, string colorName, bool isDetail = true)
        {
            if (isDetail && (_chkVerboseLoggingRegular == null || !_chkVerboseLoggingRegular.Checked))
            {
                return;
            }

            if (_internalLogRtb == null || _internalLogRtb.IsDisposed) return;

            _internalLogRtb.Invoke((MethodInvoker)delegate
            {
                _internalLogRtb.SelectionStart = _internalLogRtb.TextLength;
                _internalLogRtb.SelectionLength = 0;

                Color messageColor;
                try { messageColor = Color.FromName(colorName); }
                catch (Exception) { messageColor = Color.Black; }

                _internalLogRtb.SelectionColor = messageColor;
                _internalLogRtb.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                _internalLogRtb.SelectionColor = _internalLogRtb.ForeColor;
                _internalLogRtb.ScrollToCaret();
            });
        }


        private string GetAppDirectory()
        {
            return Application.StartupPath ?? AppContext.BaseDirectory ?? ".";
        }

        public void SetProjectRoot(string projectFolder)
        {
            _projectRoot = projectFolder;
            PopulateArenas();
        }

        private void BuildUi()
        {
            // ToolStrip setup
            var bar = new ToolStrip();
            bar.Items.Add(new ToolStripLabel("Paint slot:"));
            _paintSlotBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _paintSlotBox.SelectedIndexChanged += (_, __) =>
            {
                if (_paintSlotBox.SelectedIndex >= 0)
                {
                    CurrentDrawMask = 1 << _paintSlotBox.SelectedIndex;
                }
            };
            bar.Items.Add(_paintSlotBox);

            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(new ToolStripLabel("Arena:"));
            _arenaBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _arenaBox.AutoSize = false; _arenaBox.Width = 200; _arenaBox.DropDownWidth = 300; _arenaBox.MaxDropDownItems = 25;
            _arenaBox.SelectedIndexChanged += (_, __) => LoadSelectedArena();
            bar.Items.Add(_arenaBox);

            bar.Items.Add(new ToolStripSeparator());
            _btnSave.Enabled = false;
            _btnSave.Click += (_, __) => SaveArena();
            bar.Items.Add(_btnSave);

            bar.Items.Add(new ToolStripSeparator());
            _hoverLbl.Text = ""; _hoverLbl.AutoSize = false; _hoverLbl.Width = 350;
            bar.Items.Add(_hoverLbl);

            bar.Items.Add(new ToolStripSeparator());
            _btnToggleMap = new ToolStripButton("Show Map");
            _btnToggleMap.Click += ToggleMap_Click;
            bar.Items.Add(_btnToggleMap);

            _mapControlsSeparator = new ToolStripSeparator();
            bar.Items.Add(_mapControlsSeparator);

            _mapXLabel = new ToolStripLabel("Map X:");
            bar.Items.Add(_mapXLabel);
            _mapOffsetXNumeric.Minimum = -2000; _mapOffsetXNumeric.Maximum = 2000; _mapOffsetXNumeric.Value = 0; _mapOffsetXNumeric.Width = 60;
            _mapOffsetXNumeric.ValueChanged += MapTransformControls_ValueChanged;
            _mapOffsetXHost = new ToolStripControlHost(_mapOffsetXNumeric);
            bar.Items.Add(_mapOffsetXHost);

            _mapYLabel = new ToolStripLabel("Y:");
            bar.Items.Add(_mapYLabel);
            _mapOffsetYNumeric.Minimum = -2000; _mapOffsetYNumeric.Maximum = 2000; _mapOffsetYNumeric.Value = 0; _mapOffsetYNumeric.Width = 60;
            _mapOffsetYNumeric.ValueChanged += MapTransformControls_ValueChanged;
            _mapOffsetYHost = new ToolStripControlHost(_mapOffsetYNumeric);
            bar.Items.Add(_mapOffsetYHost);

            _mapScaleLabel = new ToolStripLabel("Scale:");
            bar.Items.Add(_mapScaleLabel);
            _mapScaleNumeric.Minimum = 0.1M; _mapScaleNumeric.Maximum = 10; _mapScaleNumeric.Value = 1.0M; _mapScaleNumeric.DecimalPlaces = 2; _mapScaleNumeric.Increment = 0.1M; _mapScaleNumeric.Width = 60;
            _mapScaleNumeric.ValueChanged += MapTransformControls_ValueChanged;
            _mapScaleHost = new ToolStripControlHost(_mapScaleNumeric);
            bar.Items.Add(_mapScaleHost);

            _mapRotLabel = new ToolStripLabel("Rot:");
            bar.Items.Add(_mapRotLabel);
            _mapRotationBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _mapRotationBox.Items.AddRange(new object[] { "None", "90°", "180°", "270°" });
            _mapRotationBox.SelectedIndex = 0;
            _mapRotationBox.Width = 70;
            _mapRotationBox.SelectedIndexChanged += MapTransformControls_ValueChanged;
            bar.Items.Add(_mapRotationBox);

            bar.Items.Add(new ToolStripSeparator());
            _chkVerboseLoggingRegular = new CheckBox
            {
                Text = "Verbose Logging",
                Checked = false
            };
            _chkVerboseLoggingHost = new ToolStripControlHost(_chkVerboseLoggingRegular);
            bar.Items.Add(_chkVerboseLoggingHost);

            bar.Dock = DockStyle.Top;

            // Initialize GridPanel
            _panel.Initialise(this);
            _panel.MouseMove += Panel_MouseMove;
            // _panel.Dock = DockStyle.Fill; // This will be handled by SplitContainer

            // Initialize Internal Log RichTextBox
            _internalLogRtb.Dock = DockStyle.Fill;
            _internalLogRtb.ReadOnly = true;
            _internalLogRtb.WordWrap = true;
            _internalLogRtb.ScrollBars = RichTextBoxScrollBars.Vertical;
            _internalLogRtb.BackColor = Color.FromArgb(240, 240, 240);
            _internalLogRtb.Font = new Font("Consolas", 9f);

            // NEW: Initialize Commands Help RichTextBox
            _commandsHelpRtb = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.None, // Keep it short
                Text = "Mouse Controls (Grid Editor):\n" +
                       "• Left Click: Add selected tag to cell(s).\n" +
                       "• Shift + Left Click: Toggle selected tag in cell(s).\n" +
                       "• Ctrl + Left Click or Right Click: Erase all tags in cell(s).\n" +
                       "• Drag Mouse: Apply current action to a rectangular area.",
                Font = new Font("Segoe UI", 9f), // A standard UI font
                BackColor = SystemColors.Info, // A distinct background
                BorderStyle = BorderStyle.FixedSingle
            };
            // Optional: Add some padding inside the RichTextBox
            _commandsHelpRtb.SelectAll();
            _commandsHelpRtb.SelectionIndent += 5; // Indent text by 5 pixels
            _commandsHelpRtb.DeselectAll();


            // NEW: Initialize Lower SplitContainer (for Commands and Log)
            _lowerSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel1, // Command panel height is fixed during resize
                SplitterWidth = 4 // Standard splitter width
            };
            _lowerSplitContainer.Panel1.Controls.Add(_commandsHelpRtb);
            _lowerSplitContainer.Panel2.Controls.Add(_internalLogRtb);
            _lowerSplitContainer.SplitterDistance = 90; // Height of the commands panel in pixels

            // Initialize Main SplitContainer (for Grid and Lower Area)
            _mainSplitContainer.Dock = DockStyle.Fill;
            _mainSplitContainer.Orientation = Orientation.Vertical; // Grid on top, commands/log below
            _mainSplitContainer.Panel1.Controls.Add(_panel);
            _mainSplitContainer.Panel2.Controls.Add(_lowerSplitContainer); // Add the container for commands & log
            _mainSplitContainer.SplitterDistance = (int)(this.Height * 0.65); // Grid panel gets 65% of height

            Controls.Add(_mainSplitContainer);
            Controls.Add(bar);

            Dock = DockStyle.Fill;

            SetMapControlsVisibility(false);
            _btnToggleMap.Text = "Show Map";
        }

        private void SetMapControlsVisibility(bool isVisible)
        {
            _mapControlsSeparator.Visible = isVisible;
            _mapXLabel.Visible = isVisible;
            _mapOffsetXHost.Visible = isVisible;
            _mapYLabel.Visible = isVisible;
            _mapOffsetYHost.Visible = isVisible;
            _mapScaleLabel.Visible = isVisible;
            _mapScaleHost.Visible = isVisible;
            _mapRotLabel.Visible = isVisible;
            _mapRotationBox.Visible = isVisible;
        }

        private void ToggleMap_Click(object? sender, EventArgs e)
        {
            if (_currentArenaMapImage == null)
            {
                if (!string.IsNullOrEmpty(_currentMapImagePath) && File.Exists(_currentMapImagePath))
                {
                    try
                    {
                        _currentArenaMapImage?.Dispose();
                        using (FileStream fs = new FileStream(_currentMapImagePath, FileMode.Open, FileAccess.Read))
                        {
                            _currentArenaMapImage = Image.FromStream(fs);
                        }
                        AppendLog($"Map image loaded: {_currentMapImagePath}", "Green", isDetail: true);
                        _btnToggleMap.Text = "Hide Map";
                        SetMapControlsVisibility(true);
                    }
                    catch (Exception imgEx)
                    {
                        AppendLog($"Error loading map image from {_currentMapImagePath}: {imgEx.Message}", "Red", isDetail: false);
                        _currentArenaMapImage = null;
                        _btnToggleMap.Text = "Show Map";
                        SetMapControlsVisibility(false);
                    }
                }
                else
                {
                    AppendLog($"Map image path not found or not set. Path: '{_currentMapImagePath ?? "null"}'", "OrangeRed", isDetail: false);
                    _btnToggleMap.Text = "Show Map";
                    SetMapControlsVisibility(false);
                }
            }
            else
            {
                _currentArenaMapImage?.Dispose();
                _currentArenaMapImage = null;
                _btnToggleMap.Text = "Show Map";
                SetMapControlsVisibility(false);
                AppendLog("Map image unloaded.", "Black", isDetail: true);
            }
            _panel.Invalidate();
        }


        private void MapTransformControls_ValueChanged(object? sender, EventArgs e)
        {
            MapDisplayOffset = new Point((int)_mapOffsetXNumeric.Value, (int)_mapOffsetYNumeric.Value);
            MapDisplayScale = (float)_mapScaleNumeric.Value;

            MapDisplayRotation = _mapRotationBox.SelectedIndex switch
            {
                0 => RotateFlipType.RotateNoneFlipNone,
                1 => RotateFlipType.Rotate90FlipNone,
                2 => RotateFlipType.Rotate180FlipNone,
                3 => RotateFlipType.Rotate270FlipNone,
                _ => RotateFlipType.RotateNoneFlipNone,
            };
            _panel.Invalidate();
        }


        private void UpdatePaintSlotBox()
        {
            _paintSlotBox.Items.Clear();
            if (CurrentGrid == null) return;
            for (int i = 0; i < GridFile.NumSlots; i++)
            {
                string tagName = CurrentGrid.GetTagNameForSlot(i);
                bool isActive = CurrentGrid.IsSlotActive(i);
                string displayText;
                if (isActive)
                {
                    displayText = string.IsNullOrEmpty(tagName) || tagName == GridFile.NO_VALUE
                                  ? $"Slot {i} (Active, Unnamed)"
                                  : $"{tagName} (bit {i})";
                }
                else
                {
                    displayText = string.IsNullOrEmpty(tagName) || tagName == GridFile.NO_VALUE
                                  ? $"{GridFile.NO_VALUE} (bit {i}, Inactive)"
                                  : $"{tagName} (bit {i}, Inactive)";
                }
                _paintSlotBox.Items.Add(displayText);
            }
            if (_paintSlotBox.Items.Count > 0)
            {
                int previousIndex = _paintSlotBox.SelectedIndex;
                _paintSlotBox.SelectedIndex = (previousIndex >= 0 && previousIndex < _paintSlotBox.Items.Count) ? previousIndex : 0;
                if (_paintSlotBox.SelectedIndex >= 0) CurrentDrawMask = 1 << _paintSlotBox.SelectedIndex;
            }
            else
            {
                CurrentDrawMask = 0;
            }
        }

        private void ResetMapState()
        {
            AppendLog("ResetMapState called.", "CornflowerBlue", isDetail: true);
            _currentArenaMapImage?.Dispose();
            _currentArenaMapImage = null;
            _currentMapImagePath = null;

            _mapOffsetXNumeric.Value = 0;
            _mapOffsetYNumeric.Value = 0;
            _mapScaleNumeric.Value = 1.0M;
            _mapRotationBox.SelectedIndex = 0;

            if (_btnToggleMap != null) _btnToggleMap.Text = "Show Map";
            SetMapControlsVisibility(false);
            AppendLog("ResetMapState finished. UI controls set to neutral.", "CornflowerBlue", isDetail: true);
        }

        private void PopulateArenas()
        {
            _arenaBox.Items.Clear();
            _currentArenaPath = null;
            _btnSave.Enabled = false;

            ResetMapState();
            MapTransformControls_ValueChanged(null, EventArgs.Empty);


            if (string.IsNullOrWhiteSpace(_projectRoot))
            {
                LoadSelectedArena();
                return;
            }

            string encRoot = Path.Combine(
                _projectRoot,
                $"{Path.GetFileName(_projectRoot)}_BEC",
                "data",
                "encounters");
            if (!Directory.Exists(encRoot))
            {
                encRoot = Path.Combine(_projectRoot, "data", "encounters");
                if (!Directory.Exists(encRoot))
                {
                    encRoot = Path.Combine(_projectRoot, "encounters");
                    if (!Directory.Exists(encRoot))
                    {
                        LoadSelectedArena();
                        return;
                    }
                }
            }

            try
            {
                foreach (string file in Directory.GetFiles(encRoot, "*.grd", SearchOption.AllDirectories)
                                                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                {
                    string rel = Path.GetRelativePath(encRoot, file);
                    _arenaBox.Items.Add(new ComboItem(rel, file));
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error populating arenas from '{encRoot}': {ex.Message}", "Red", isDetail: false);
            }


            if (_arenaBox.Items.Count > 0) _arenaBox.SelectedIndex = 0;
            else LoadSelectedArena();
        }


        private void LoadSelectedArena()
        {
            AppendLog("LoadSelectedArena called.", "Teal", isDetail: false);
            ResetMapState();

            string? rawGrdDirectoryName = null;
            string? processedGrdKey = null;
            bool autoShowMap = false;

            if (_arenaBox.SelectedItem is not ComboItem ci)
            {
                CurrentGrid = new GridFile();
                _currentArenaPath = null;
                _btnSave.Enabled = false;
                AppendLog("No arena selected or list empty, loaded default grid.", "Black", isDetail: false);
                _currentMapImagePath = null;
            }
            else
            {
                AppendLog($"Loading arena: '{ci.Display}' from path: '{ci.FullPath}'", "Blue", isDetail: false);
                try
                {
                    CurrentGrid = GridFile.Load(ci.FullPath);
                    _currentArenaPath = ci.FullPath;
                    _btnSave.Enabled = true;
                    AppendLog($"Successfully loaded GridFile: '{ci.FullPath}'", "DarkGreen", isDetail: true);

                    var directoryOfGrd = Path.GetDirectoryName(ci.FullPath);
                    AppendLog($"Path.GetDirectoryName(ci.FullPath) result: '{directoryOfGrd}'", "DarkGray", isDetail: true);

                    if (!string.IsNullOrEmpty(directoryOfGrd))
                    {
                        rawGrdDirectoryName = Path.GetFileName(directoryOfGrd);
                        AppendLog($"Raw GRD File's Parent Directory Name (from Path.GetFileName): '{rawGrdDirectoryName}'", "DarkGray", isDetail: false);

                        processedGrdKey = rawGrdDirectoryName?.Trim();
                        if (!string.IsNullOrEmpty(processedGrdKey))
                        {
                            AppendLog($"Processed GRD Key for lookup (after Trim): '{processedGrdKey}'", "DarkBlue", isDetail: false);

                            string mapImageFileName = $"map_{processedGrdKey}.png";
                            string appDir = GetAppDirectory();
                            string mapDir = Path.Combine(appDir, "maps");
                            string potentialMapPath = Path.Combine(mapDir, mapImageFileName);
                            AppendLog($"Attempting to find map image at: '{potentialMapPath}'", "Black", isDetail: true);
                            if (File.Exists(potentialMapPath))
                            {
                                _currentMapImagePath = potentialMapPath;
                                AppendLog($"Map image path found and stored: '{_currentMapImagePath}'", "Green", isDetail: true);
                            }
                            else
                            {
                                _currentMapImagePath = null;
                                AppendLog($"Map image file NOT FOUND at '{potentialMapPath}'.", "OrangeRed", isDetail: false);
                            }
                        }
                        else
                        {
                            _currentMapImagePath = null;
                            AppendLog("Processed GRD key is NULL or EMPTY after trim. Cannot derive map image or lookup defaults.", "OrangeRed", isDetail: false);
                        }
                    }
                    else
                    {
                        _currentMapImagePath = null;
                        AppendLog("Path.GetDirectoryName for the GRD file returned NULL or EMPTY. Cannot derive map image or lookup defaults.", "OrangeRed", isDetail: false);
                    }

                    AppendLog($"Checking for default transforms with key: '{processedGrdKey ?? "NULL_KEY"}'", "Magenta", isDetail: false);
                    if (!string.IsNullOrEmpty(processedGrdKey))
                    {
                        bool keyFound = _mapDefaultTransforms.TryGetValue(processedGrdKey, out var defaults);
                        AppendLog($"_mapDefaultTransforms.TryGetValue result for key '{processedGrdKey}': {keyFound}", "Magenta", isDetail: false);

                        if (keyFound)
                        {
                            AppendLog($"SUCCESS: Default transforms FOUND for key '{processedGrdKey}'. Applying values: X={defaults.OffsetX}, Y={defaults.OffsetY}, Scale={defaults.Scale}, RotIdx={defaults.RotationIndex}", "Purple", isDetail: false);
                            _mapOffsetXNumeric.Value = Math.Max(_mapOffsetXNumeric.Minimum, Math.Min(_mapOffsetXNumeric.Maximum, defaults.OffsetX));
                            _mapOffsetYNumeric.Value = Math.Max(_mapOffsetYNumeric.Minimum, Math.Min(_mapOffsetYNumeric.Maximum, defaults.OffsetY));
                            _mapScaleNumeric.Value = Math.Max(_mapScaleNumeric.Minimum, Math.Min(_mapScaleNumeric.Maximum, defaults.Scale));

                            if (defaults.RotationIndex >= 0 && defaults.RotationIndex < _mapRotationBox.Items.Count)
                            {
                                _mapRotationBox.SelectedIndex = defaults.RotationIndex;
                                AppendLog($"Applied RotationIndex: {defaults.RotationIndex} to ComboBox.", "Purple", isDetail: true);
                            }
                            else
                            {
                                AppendLog($"Warning: Default RotationIndex {defaults.RotationIndex} for map key '{processedGrdKey}' is out of bounds for rotation ComboBox (item count: {_mapRotationBox.Items.Count}). Defaulting to 0 (None).", "Orange", isDetail: false);
                                _mapRotationBox.SelectedIndex = 0;
                            }
                            AppendLog($"Transform UI controls updated. OffsetX: {_mapOffsetXNumeric.Value}, OffsetY: {_mapOffsetYNumeric.Value}, Scale: {_mapScaleNumeric.Value}, Rotation: {_mapRotationBox.SelectedItem}", "Purple", isDetail: true);

                            if (!string.IsNullOrEmpty(_currentMapImagePath))
                            {
                                autoShowMap = true;
                                AppendLog($"Default transforms applied and map image exists. Flagging for auto-show.", "DarkViolet", isDetail: true);
                            }
                            else
                            {
                                AppendLog($"Default transforms applied BUT no map image path exists ('{_currentMapImagePath ?? "null"}'). Map will not auto-show.", "DarkViolet", isDetail: true);
                            }
                        }
                        else
                        {
                            AppendLog($"INFO: No default transforms found in dictionary for key '{processedGrdKey}'. UI controls will retain neutral values from ResetMapState.", "Gray", isDetail: false);
                        }
                    }
                    else
                    {
                        AppendLog("INFO: Processed GRD key for default transform lookup was NULL or EMPTY. Using neutral transform values.", "Gray", isDetail: false);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to load arena '{ci.Display}':\n{ex.Message}",
                        "Grid Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    AppendLog($"ERROR during LoadSelectedArena for '{ci.Display}': {ex.Message}\nStackTrace: {ex.StackTrace}", "Red", isDetail: false);
                    CurrentGrid = new GridFile();
                    _currentArenaPath = null;
                    _btnSave.Enabled = false;
                    _currentMapImagePath = null;
                    processedGrdKey = null;
                }
            }

            if (autoShowMap)
            {
                AppendLog("Attempting to auto-show map...", "DarkViolet", isDetail: false);
                if (!string.IsNullOrEmpty(_currentMapImagePath) && File.Exists(_currentMapImagePath))
                {
                    try
                    {
                        _currentArenaMapImage?.Dispose();
                        using (FileStream fs = new FileStream(_currentMapImagePath, FileMode.Open, FileAccess.Read))
                        {
                            _currentArenaMapImage = Image.FromStream(fs);
                        }
                        AppendLog($"Map image auto-loaded: {_currentMapImagePath}", "Green", isDetail: true);
                        _btnToggleMap.Text = "Hide Map";
                        SetMapControlsVisibility(true);
                    }
                    catch (Exception imgEx)
                    {
                        AppendLog($"Error auto-loading map image from {_currentMapImagePath}: {imgEx.Message}", "Red", isDetail: false);
                        _currentArenaMapImage = null;
                        _btnToggleMap.Text = "Show Map";
                        SetMapControlsVisibility(false);
                    }
                }
                else
                {
                    AppendLog($"Auto-show map skipped: Map image path ('{_currentMapImagePath ?? "null"}') not valid or file does not exist.", "OrangeRed", isDetail: false);
                    _btnToggleMap.Text = "Show Map";
                    SetMapControlsVisibility(false);
                }
            }

            AppendLog("Calling MapTransformControls_ValueChanged to update IGridContext and invalidate panel.", "Teal", isDetail: true);
            MapTransformControls_ValueChanged(null, EventArgs.Empty);
            AppendLog("Calling UpdatePaintSlotBox.", "Teal", isDetail: true);
            UpdatePaintSlotBox();
            AppendLog("LoadSelectedArena finished.", "Teal", isDetail: false);
        }


        private void SaveArena()
        {
            if (_currentArenaPath == null || CurrentGrid == null) return;
            try
            {
                CurrentGrid.Save(_currentArenaPath);
                AppendLog($"Arena saved: {_currentArenaPath}", "Green", isDetail: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed:\n{ex.Message}", "Grid Builder",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"Save failed for {_currentArenaPath}: {ex.Message}", "Red", isDetail: false);
            }
        }

        private void Panel_MouseMove(object? sender, MouseEventArgs e)
        {
            if (CurrentGrid == null) return;
            int x = e.X / GridPanel.CellSize;
            int y = e.Y / GridPanel.CellSize;

            if (x < 0 || x >= GridFile.ArenaSize || y < 0 || y >= GridFile.ArenaSize)
            {
                _hoverLbl.Text = "";
                return;
            }

            int cellValue = CurrentGrid.GetValue(x, y);
            string tagsInCell = CurrentGrid.GetInfoForValue(cellValue);

            string currentPaintSlotName = _paintSlotBox.SelectedIndex >= 0 ? GetFriendlyNameForBit(_paintSlotBox.SelectedIndex) : "None";
            string paintMaskInfo = $"Paint: {currentPaintSlotName} (0x{CurrentDrawMask:X})";

            _hoverLbl.Text = $"Cell ({x},{y}): 0x{cellValue:X8} [{tagsInCell}] | {paintMaskInfo}";
        }

        private sealed record ComboItem(string Display, string FullPath)
        {
            public override string ToString() => Display;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _currentArenaMapImage?.Dispose();
                _currentArenaMapImage = null;

                // Components added to Controls collections are typically disposed by their parent.
                // However, explicit disposal here for owned components is safer if their lifecycle is complex.
                _internalLogRtb?.Dispose();
                _commandsHelpRtb?.Dispose(); // Dispose new RichTextBox
                _lowerSplitContainer?.Dispose(); // Dispose new SplitContainer
                _mainSplitContainer?.Dispose();
                _chkVerboseLoggingHost?.Dispose();

                // Other ToolStrip items are components and should be handled by ToolStrip disposal
                _paintSlotBox?.Dispose();
                _arenaBox?.Dispose();
                _mapOffsetXHost?.Dispose();
                _mapOffsetYHost?.Dispose();
                _mapScaleHost?.Dispose();
                _mapRotationBox?.Dispose();

            }
            base.Dispose(disposing);
        }
    }
}
