using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D; // Required for InterpolationMode and RotateFlipType

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
        public Image? CurrentMapArenaImage => _currentArenaMapImage; // This remains the source for GridPanel
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
        private ToolStripButton _btnToggleMap; // Button to show/hide map

        // Changed from ToolStripCheckBox to CheckBox
        private CheckBox _chkVerboseLoggingRegular;
        private ToolStripControlHost _chkVerboseLoggingHost; // Host for the regular CheckBox


        // UI Controls for map manipulation
        private NumericUpDown _mapOffsetXNumeric = new NumericUpDown();
        private NumericUpDown _mapOffsetYNumeric = new NumericUpDown();
        private NumericUpDown _mapScaleNumeric = new NumericUpDown();
        private ToolStripComboBox _mapRotationBox = new ToolStripComboBox(); // For rotation

        // References to ToolStripControlHosts and Labels for map manipulation controls visibility
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


        /* ───── state ────────────────────────────────────────────────── */
        private string? _projectRoot;
        private string? _currentArenaPath;
        private Image? _currentArenaMapImage = null; // Holds the actual loaded image for display
        private string? _currentMapImagePath = null; // Stores the path to the map image for on-demand loading

        public GridBuilderView()
        {
            InitializeComponent();
            BuildUi();
            UpdatePaintSlotBox();
        }

        // Updated AppendLog to include isDetail parameter
        private void AppendLog(string message, string colorName, bool isDetail = true)
        {
            // If the message is detailed and verbose logging is not checked, skip logging.
            // Access Checked property via the hosted control
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
                catch (Exception) { messageColor = Color.Black; } // Fallback color

                _internalLogRtb.SelectionColor = messageColor;
                _internalLogRtb.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                _internalLogRtb.SelectionColor = _internalLogRtb.ForeColor; // Reset to default
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

            // Toggle Map Button
            bar.Items.Add(new ToolStripSeparator());
            _btnToggleMap = new ToolStripButton("Show Map");
            _btnToggleMap.Click += ToggleMap_Click;
            bar.Items.Add(_btnToggleMap);

            // Separator for map controls (will be shown/hidden with controls)
            _mapControlsSeparator = new ToolStripSeparator();
            bar.Items.Add(_mapControlsSeparator);

            // Map Manipulation Controls - store references to labels and hosts
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
            _mapRotationBox.SelectedIndex = 0; // Default to None
            _mapRotationBox.Width = 70;
            _mapRotationBox.SelectedIndexChanged += MapTransformControls_ValueChanged;
            bar.Items.Add(_mapRotationBox); // Add ToolStripComboBox directly

            // Verbose Logging Checkbox (using regular CheckBox hosted in ToolStripControlHost)
            bar.Items.Add(new ToolStripSeparator());
            _chkVerboseLoggingRegular = new CheckBox // Instantiate regular CheckBox
            {
                Text = "Verbose Logging",
                Checked = false // Default to not verbose
            };
            _chkVerboseLoggingHost = new ToolStripControlHost(_chkVerboseLoggingRegular); // Host it
            bar.Items.Add(_chkVerboseLoggingHost); // Add the host to the ToolStrip

            bar.Dock = DockStyle.Top;

            // GridPanel setup
            _panel.Initialise(this);
            _panel.MouseMove += Panel_MouseMove; // Ensure this is connected

            // Internal RichTextBox for logging setup
            _internalLogRtb.Dock = DockStyle.Fill;
            _internalLogRtb.ReadOnly = true;
            _internalLogRtb.WordWrap = true;
            _internalLogRtb.ScrollBars = RichTextBoxScrollBars.Vertical;
            _internalLogRtb.BackColor = Color.FromArgb(240, 240, 240);
            _internalLogRtb.Font = new Font("Consolas", 9f);

            // SplitContainer setup
            _mainSplitContainer.Dock = DockStyle.Fill;
            _mainSplitContainer.Orientation = Orientation.Vertical;
            _mainSplitContainer.Panel1.Controls.Add(_panel);
            _mainSplitContainer.Panel2.Controls.Add(_internalLogRtb);
            _mainSplitContainer.SplitterDistance = (int)(this.Height * 0.75);

            Controls.Add(_mainSplitContainer);
            Controls.Add(bar);

            Dock = DockStyle.Fill;

            // Initially hide map controls and set button text
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
            if (_currentArenaMapImage == null) // Map is currently hidden, try to show it
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
            else // Map is currently shown, hide it
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
            _currentArenaMapImage?.Dispose();
            _currentArenaMapImage = null;
            _currentMapImagePath = null;

            _mapOffsetXNumeric.Value = 0;
            _mapOffsetYNumeric.Value = 0;
            _mapScaleNumeric.Value = 1.0M;
            _mapRotationBox.SelectedIndex = 0;

            if (_btnToggleMap != null) _btnToggleMap.Text = "Show Map";
            SetMapControlsVisibility(false);
            _panel.Invalidate();
        }

        private void PopulateArenas()
        {
            _arenaBox.Items.Clear();
            _currentArenaPath = null;
            _btnSave.Enabled = false;

            ResetMapState();

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
            ResetMapState();

            if (_arenaBox.SelectedItem is not ComboItem ci)
            {
                CurrentGrid = new GridFile();
                _currentArenaPath = null;
                _btnSave.Enabled = false;
                AppendLog("No arena selected or list empty, loaded default grid.", "Black", isDetail: false);
            }
            else
            {
                AppendLog($"Loading arena: {ci.Display} from {ci.FullPath}", "Blue", isDetail: false);
                try
                {
                    CurrentGrid = GridFile.Load(ci.FullPath);
                    _currentArenaPath = ci.FullPath;
                    _btnSave.Enabled = true;

                    string? grdFileDirectoryName = Path.GetFileName(Path.GetDirectoryName(ci.FullPath));
                    AppendLog($"GRD File Directory Name: {grdFileDirectoryName ?? "N/A"}", "DarkGray", isDetail: true);

                    if (!string.IsNullOrEmpty(grdFileDirectoryName))
                    {
                        string mapImageFileName = $"map_{grdFileDirectoryName}.png";
                        string appDir = GetAppDirectory();
                        string mapDir = Path.Combine(appDir, "maps");
                        string potentialMapPath = Path.Combine(mapDir, mapImageFileName);

                        AppendLog($"App Directory: {appDir}", "DarkGray", isDetail: true);
                        AppendLog($"Map Directory Base: {mapDir}", "DarkGray", isDetail: true);
                        AppendLog($"Derived Map Image File Name: {mapImageFileName}", "DarkGray", isDetail: true);
                        AppendLog($"Potential map image path: {potentialMapPath}", "Black", isDetail: true);

                        if (File.Exists(potentialMapPath))
                        {
                            _currentMapImagePath = potentialMapPath;
                            AppendLog($"Map image path found and stored: {_currentMapImagePath}", "Green", isDetail: true);
                        }
                        else
                        {
                            _currentMapImagePath = null;
                            AppendLog("Map image file not found at derived path.", "OrangeRed", isDetail: false);
                        }
                    }
                    else
                    {
                        _currentMapImagePath = null;
                        AppendLog("Could not determine GRD file directory name to derive map image name.", "OrangeRed", isDetail: false);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to load arena '{ci.Display}':\n{ex.Message}",
                        "Grid Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    AppendLog($"Failed to load arena '{ci.Display}': {ex.Message}", "Red", isDetail: false);
                    CurrentGrid = new GridFile();
                    _currentArenaPath = null;
                    _btnSave.Enabled = false;
                    _currentMapImagePath = null;
                }
            }
            UpdatePaintSlotBox();
        }

        private void SaveArena()
        {
            if (_currentArenaPath == null || CurrentGrid == null) return;
            try
            {
                CurrentGrid.Save(_currentArenaPath);
                //MessageBox.Show("Arena saved.", "Grid Builder",
                //                MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                _internalLogRtb?.Dispose();
                _mainSplitContainer?.Dispose();
                // Dispose the host if it's not null
                _chkVerboseLoggingHost?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
