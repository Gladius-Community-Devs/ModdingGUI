using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModdingGUI.Core.Api;

namespace ModdingGUI
{
    /// <summary>
    /// items.tok editor. Reads and writes exclusively through
    /// <see cref="IItemsApi"/>; every save goes through the Core API, which
    /// locks the project, writes a timestamped backup into
    /// <c>&lt;project&gt;/.mod-history/</c>, and rewrites the file byte-for-byte
    /// via the schema layer (no more legacy <c>ToTokFormatString</c>).
    /// </summary>
    /// <remarks>
    /// The lookup-text side (<c>lookuptext_eng.txt</c>) is a plain txt file and
    /// is updated through <see cref="ILookupTextApi"/> so Item Editor saves
    /// still get Core locking and timestamped backups.
    /// </remarks>
    public partial class frmMain : Form
    {
        /// <summary>
        /// Lightweight row model for the item dropdown. Carries the stable id
        /// (<see cref="Name"/>, used for API lookups) and a resolved
        /// <see cref="DisplayName"/> for the combobox label.
        /// </summary>
        private sealed class ItemRow
        {
            public string Name { get; init; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
        }

        // Core API singleton for this editor. Stateless; every call opens
        // items.tok fresh, mutates, and writes back with backup.
        private readonly IItemsApi _itemsApi = new ItemsApi();
        private readonly ILookupTextApi _lookupTextApi = new LookupTextApi();

        private Button? _btnItemEditorAdd;
        private Button? _btnItemEditorRename;
        private Button? _btnItemEditorRemove;

        // All items in the current project, sorted by display name.
        private BindingList<ItemRow> _bindingListOfItems = new BindingList<ItemRow>();

        // DTO cache: populated by GetAll on project load and after every reload.
        // Eliminates per-selection file reads — SelectedIndexChanged hits this
        // dictionary instead of re-reading items.tok from disk.
        private Dictionary<string, ItemDto> _itemDtoCache = new Dictionary<string, ItemDto>(StringComparer.Ordinal);

        // Item-equippable skills — populated in background during project load.
        private List<SkillInfo> _itemOnlySkills = new List<SkillInfo>();

        private readonly List<string> _affinityTypes = new List<string> { "NONE", "fire", "earth", "water", "air", "dark", "light" };
        private readonly List<string> _validStatModTypes = new List<string> { "initiative", "defense", "accuracy" };
        private const int MaxStatModRows = 3;

        private sealed class StatModUIRow
        {
            public ComboBox StatTypeComboBox { get; set; } = new ComboBox();
            public TextBox StatValueTextBox { get; set; } = new TextBox();
        }
        private List<StatModUIRow> _dynamicStatModControls = new List<StatModUIRow>();

        // Paths for the active project. The Core API's "projectPath" is the
        // BEC folder (i.e. the one that contains data/config/items.tok);
        // history/backup files land under <becFolder>/.mod-history/.
        private string _becFolder = string.Empty;
        private Dictionary<int, string> _lookupTable = new Dictionary<int, string>();

        // Currently selected item's full DTO, cached so Save can diff against it.
        private ItemDto? _currentDto;

        private void NumericOnlyTextBox_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        }

        /// <summary>
        /// Entry point called by <c>tvwProjects_AfterSelect</c>. Fires the async
        /// load immediately and returns so the UI thread is never blocked.
        /// </summary>
        public void InitialiseItemEditor(string projectPath)
        {
            _ = InitialiseItemEditorAsync(projectPath);
        }

        private async Task InitialiseItemEditorAsync(string projectPath)
        {
            // ---- Path setup (no I/O) ----------------------------------------
            projectPath = projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            _becFolder = Path.Combine(projectPath, $"{Path.GetFileName(projectPath)}_BEC");
            var cfg = Path.Combine(_becFolder, "data", "config");
            string skillsTokPath = Path.Combine(cfg, "skills.tok");

            // Capture for lambdas (fields may change if user switches project quickly)
            string becFolder = _becFolder;

            // ---- Disable editor while loading --------------------------------
            ddlItemEditorAllItems.Enabled = false;
            ddlItemEditorAllItems.DataSource = null;
            btnItemEditorNewSave.Enabled = false;
            AppendLog("Initializing Item Editor...", Color.DarkGreen);

            // ---- Background I/O: two parallel tasks --------------------------
            //   Task A: load lookuptext_eng.txt, then parse skills.tok
            //   Task B: load items.tok once via GetAll
            // Task A and B read different files so they run in true parallel.
            Dictionary<int, string> lookupTable;
            IReadOnlyList<ItemDto> allDtos;
            List<SkillInfo> itemSkills;

            try
            {
                var lookupAndSkillsTask = Task.Run(() =>
                {
                    var lt = new Dictionary<int, string>(_lookupTextApi.Load(becFolder));
                    // Pass pre-loaded lookup — no second read of lookuptext_eng.txt.
                    // Pass null logAction — safe to call from background thread.
                    var sk = SkillParser.ParseSkillsTok(skillsTokPath, lt, null)
                        .Where(s => s.IsItemSkill)
                        .OrderBy(s => s.DisplayName)
                        .ToList();
                    return (lt, sk);
                });

                var itemsTask = Task.Run(() => _itemsApi.GetAll(becFolder));

                await Task.WhenAll(lookupAndSkillsTask, itemsTask);

                (lookupTable, itemSkills) = lookupAndSkillsTask.Result;
                allDtos = itemsTask.Result;
            }
            catch (Exception ex)
            {
                AppendLog($"Failed to initialize Item Editor: {ex.Message}", Color.Red);
                ddlItemEditorAllItems.Enabled = true;
                btnItemEditorNewSave.Enabled = true;
                return;
            }

            // ---- UI thread: update all state from loaded data ----------------
            _lookupTable = lookupTable;
            _itemOnlySkills = itemSkills;

            // Build DTO cache and dropdown rows in one pass.
            _itemDtoCache = new Dictionary<string, ItemDto>(allDtos.Count, StringComparer.Ordinal);
            var rows = new List<ItemRow>(allDtos.Count);
            foreach (var dto in allDtos)
            {
                _itemDtoCache[dto.Name] = dto;
                rows.Add(new ItemRow { Name = dto.Name, DisplayName = ResolveDisplayName(dto) });
            }
            rows.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            _bindingListOfItems = new BindingList<ItemRow>(rows);

            AppendLog($"Loaded {allDtos.Count} items, {itemSkills.Count} item skills.", Color.DarkGreen);

            BindItemControls();
            BindAffinityDropdown();

            txtItemEditorNewAffinity.KeyPress -= NumericOnlyTextBox_KeyPress;
            txtItemEditorNewAffinity.KeyPress += NumericOnlyTextBox_KeyPress;
            txtItemEditorNewCost.KeyPress -= NumericOnlyTextBox_KeyPress;
            txtItemEditorNewCost.KeyPress += NumericOnlyTextBox_KeyPress;
            txtItemEditorNewLevel.KeyPress -= NumericOnlyTextBox_KeyPress;
            txtItemEditorNewLevel.KeyPress += NumericOnlyTextBox_KeyPress;

            if (flpStatMods != null) ClearAndRepopulateStatModControls(null);
            else AppendLog("FlowLayoutPanel 'flpStatMods' NOT FOUND.", Color.Red);

            this.btnItemEditorMoreStats.Click -= btnItemEditorMoreStats_Click;
            this.btnItemEditorMoreStats.Click += btnItemEditorMoreStats_Click;
            this.btnItemEditorNewSave.Click -= btnItemEditorNewSave_Click;
            this.btnItemEditorNewSave.Click += btnItemEditorNewSave_Click;
            EnsureItemCrudButtons();

            ddlItemEditorAllItems.Enabled = true;
            btnItemEditorNewSave.Enabled = true;

            if (ddlItemEditorAllItems.Items.Count > 0) ddlItemEditorAllItems.SelectedIndex = 0;
            else { AppendLog("Initialized. No items loaded.", Color.Orange); ClearEditorFields(); }
        }

        /// <summary>
        /// Reload all items via a single <see cref="IItemsApi.GetAll"/> call and
        /// rebuild both the DTO cache and the dropdown binding list.
        /// Called after a successful save to refresh display names.
        /// </summary>
        private void LoadItemsFromApi()
        {
            var dtos = _itemsApi.GetAll(_becFolder);
            _itemDtoCache = new Dictionary<string, ItemDto>(dtos.Count, StringComparer.Ordinal);
            var rows = new List<ItemRow>(dtos.Count);
            foreach (var dto in dtos)
            {
                _itemDtoCache[dto.Name] = dto;
                rows.Add(new ItemRow { Name = dto.Name, DisplayName = ResolveDisplayName(dto) });
            }
            rows.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            _bindingListOfItems = new BindingList<ItemRow>(rows);
        }

        /// <summary>
        /// Resolve a display name for an item: lookup-table text if
        /// DisplayNameId is set, else fall back to the stable id.
        /// </summary>
        private string ResolveDisplayName(ItemDto dto)
        {
            if (dto.DisplayNameId.HasValue
                && _lookupTable.TryGetValue((int)dto.DisplayNameId.Value, out var text)
                && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
            return dto.Name;
        }

        private string ResolveDescription(ItemDto dto)
        {
            if (dto.DescriptionId.HasValue
                && _lookupTable.TryGetValue((int)dto.DescriptionId.Value, out var text))
            {
                return text ?? string.Empty;
            }
            return string.Empty;
        }

        private void ClearEditorFields()
        {
            txtItemEditorOriginalName.Text = txtItemEditorOriginalDescription.Text = txtItemEditorOriginalDetails.Text = string.Empty;
            txtItemEditorNewName.Text = txtItemEditorNewDescription.Text = txtItemEditorNewCost.Text = txtItemEditorNewLevel.Text = string.Empty;
            txtItemEditorNewMesh.Text = txtItemEditorNewMesh2.Text = txtItemEditorNewMaterial.Text = string.Empty;
            txtItemEditorNewName.Enabled = txtItemEditorNewDescription.Enabled = false;
            txtItemEditorNewMesh.ReadOnly = txtItemEditorNewMesh2.ReadOnly = txtItemEditorNewMaterial.ReadOnly = true;

            var noneSkillList = new List<SkillInfo> { new SkillInfo { InternalName = string.Empty, DisplayName = "NONE" } };
            ddlItemEditorNewSkill1.DataSource = new List<SkillInfo>(noneSkillList); if (ddlItemEditorNewSkill1.Items.Count > 0) ddlItemEditorNewSkill1.SelectedIndex = 0;
            ddlItemEditorNewSkill2.DataSource = new List<SkillInfo>(noneSkillList); if (ddlItemEditorNewSkill2.Items.Count > 0) ddlItemEditorNewSkill2.SelectedIndex = 0;
            if (ddlItemEditorNewAffinity.Items.Count > 0) ddlItemEditorNewAffinity.SelectedIndex = 0;
            txtItemEditorNewAffinity.Text = string.Empty; txtItemEditorNewAffinity.Enabled = false;
            ClearAndRepopulateStatModControls(null);
            _currentDto = null;
        }

        private void BindItemControls()
        {
            ddlItemEditorAllItems.DataSource = null;
            ddlItemEditorAllItems.DisplayMember = nameof(ItemRow.DisplayName);
            ddlItemEditorAllItems.ValueMember = nameof(ItemRow.Name);
            ddlItemEditorAllItems.DataSource = _bindingListOfItems;
            ddlItemEditorAllItems.SelectedIndexChanged -= ddlItemEditorAllItems_SelectedIndexChanged;
            ddlItemEditorAllItems.SelectedIndexChanged += ddlItemEditorAllItems_SelectedIndexChanged;
        }

        private void BindAffinityDropdown()
        {
            ddlItemEditorNewAffinity.DataSource = new List<string>(_affinityTypes);
            ddlItemEditorNewAffinity.SelectedIndexChanged -= ddlItemEditorNewAffinity_SelectedIndexChanged;
            ddlItemEditorNewAffinity.SelectedIndexChanged += ddlItemEditorNewAffinity_SelectedIndexChanged;
            if (ddlItemEditorNewAffinity.Items.Count > 0) ddlItemEditorNewAffinity.SelectedIndex = 0;
            txtItemEditorNewAffinity.Text = string.Empty; txtItemEditorNewAffinity.Enabled = false;
        }

        private void ddlItemEditorNewAffinity_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_currentDto is null) return;
            string selAffType = ddlItemEditorNewAffinity.SelectedItem as string ?? "NONE";
            txtItemEditorNewAffinity.Enabled = selAffType != "NONE";

            if (selAffType != "NONE"
                && _currentDto.Affinity.HasValue
                && string.Equals(_currentDto.Affinity.Value.Element, selAffType, StringComparison.OrdinalIgnoreCase))
            {
                txtItemEditorNewAffinity.Text = _currentDto.Affinity.Value.Amount.ToString();
            }
            else
            {
                txtItemEditorNewAffinity.Text = string.Empty;
            }
        }

        private void AddStatModRow(string statName = "", int statValue = 0)
        {
            if (flpStatMods == null || _dynamicStatModControls.Count >= MaxStatModRows)
            {
                if (btnItemEditorMoreStats != null) btnItemEditorMoreStats.Enabled = (_dynamicStatModControls.Count < MaxStatModRows);
                return;
            }
            Panel mainRowPanel = new Panel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 3) };
            FlowLayoutPanel hFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false };
            TextBox txtVal = new TextBox { Width = 60, Text = statValue.ToString(), Margin = new Padding(3) };
            txtVal.KeyPress += NumericOnlyTextBox_KeyPress;
            ComboBox cmbStat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Margin = new Padding(3) };
            cmbStat.Items.Add("");
            cmbStat.Items.AddRange(_validStatModTypes.ToArray());
            cmbStat.SelectedItem = cmbStat.Items.OfType<string>().FirstOrDefault(item => item.Equals(statName, StringComparison.OrdinalIgnoreCase)) ?? "";
            cmbStat.Tag = cmbStat.SelectedItem;
            cmbStat.SelectionChangeCommitted += StatTypeComboBox_SelectionChangeCommitted;
            hFlow.Controls.AddRange(new Control[] { txtVal, cmbStat });
            mainRowPanel.Controls.Add(hFlow);
            flpStatMods.Controls.Add(mainRowPanel);
            _dynamicStatModControls.Add(new StatModUIRow { StatTypeComboBox = cmbStat, StatValueTextBox = txtVal });
            if (btnItemEditorMoreStats != null) btnItemEditorMoreStats.Enabled = _dynamicStatModControls.Count < MaxStatModRows;
        }

        private void StatTypeComboBox_SelectionChangeCommitted(object? sender, EventArgs e)
        {
            if (sender is not ComboBox currentComboBox) return;
            string selectedStat = currentComboBox.SelectedItem as string ?? "";
            if (string.IsNullOrEmpty(selectedStat)) { currentComboBox.Tag = selectedStat; return; }

            if (_dynamicStatModControls.Any(row => row.StatTypeComboBox != currentComboBox && (row.StatTypeComboBox.SelectedItem as string ?? "").Equals(selectedStat, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"Stat '{selectedStat}' already selected.", "Duplicate Stat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                currentComboBox.SelectedItem = currentComboBox.Tag;
                return;
            }
            currentComboBox.Tag = selectedStat;
        }

        private void ClearAndRepopulateStatModControls(ItemDto? dto)
        {
            if (flpStatMods == null) return;
            foreach (var uiRow in _dynamicStatModControls)
            {
                uiRow.StatTypeComboBox.SelectionChangeCommitted -= StatTypeComboBox_SelectionChangeCommitted; uiRow.StatTypeComboBox.Dispose();
                uiRow.StatValueTextBox.KeyPress -= NumericOnlyTextBox_KeyPress; uiRow.StatValueTextBox.Dispose();
            }
            _dynamicStatModControls.Clear();
            flpStatMods.Controls.Clear();
            if (btnItemEditorMoreStats != null) btnItemEditorMoreStats.Enabled = true;

            if (dto?.StatMods.Count > 0)
            {
                foreach (var sm in dto.StatMods.Take(MaxStatModRows))
                {
                    AddStatModRow(sm.Stat, (int)sm.Value);
                }
            }
            if (_dynamicStatModControls.Count == 0 && MaxStatModRows > 0) AddStatModRow();
            if (btnItemEditorMoreStats != null) btnItemEditorMoreStats.Enabled = _dynamicStatModControls.Count < MaxStatModRows;
            flpStatMods.PerformLayout();
        }

        private void btnItemEditorMoreStats_Click(object? sender, EventArgs e)
        {
            if (_dynamicStatModControls.Count < MaxStatModRows) AddStatModRow();
            if (btnItemEditorMoreStats != null) btnItemEditorMoreStats.Enabled = _dynamicStatModControls.Count < MaxStatModRows;
            flpStatMods?.PerformLayout();
        }

        private void ddlItemEditorAllItems_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (ddlItemEditorAllItems.SelectedItem is not ItemRow row) { ClearEditorFields(); return; }

            // Use the DTO cache — avoids re-reading items.tok on every selection.
            // Falls back to the API only on a cache miss (e.g. post-rename race).
            if (!_itemDtoCache.TryGetValue(row.Name, out var dto))
                dto = _itemsApi.Get(_becFolder, row.Name);

            if (dto is null) { ClearEditorFields(); return; }
            _currentDto = dto;

            string displayName = ResolveDisplayName(dto);
            string description = ResolveDescription(dto);

            txtItemEditorOriginalName.Text = $"{dto.Name} / {displayName}";
            txtItemEditorOriginalDescription.Text = description;
            txtItemEditorOriginalDetails.Text = BuildPreviewText(dto);

            txtItemEditorNewName.Text = displayName;
            txtItemEditorNewName.Enabled = dto.DisplayNameId.HasValue;
            txtItemEditorNewDescription.Text = description;
            txtItemEditorNewDescription.Enabled = true;

            txtItemEditorNewCost.Text = (dto.Cost ?? 0).ToString();
            txtItemEditorNewLevel.Text = (dto.MinLevel ?? 0).ToString();
            txtItemEditorNewMesh.Text = dto.Mesh ?? string.Empty;
            txtItemEditorNewMesh2.Text = dto.Mesh2 ?? string.Empty;
            txtItemEditorNewMaterial.Text = dto.Material ?? string.Empty;
            txtItemEditorNewMesh.ReadOnly = true;
            txtItemEditorNewMesh2.ReadOnly = true;
            txtItemEditorNewMaterial.ReadOnly = true;

            UpdateSkillDropDowns(dto);

            if (ddlItemEditorNewAffinity.Items.Count > 0)
            {
                string target = "NONE";
                if (dto.Affinity.HasValue)
                {
                    target = _affinityTypes.FirstOrDefault(at => at.Equals(dto.Affinity.Value.Element, StringComparison.OrdinalIgnoreCase)) ?? "NONE";
                }
                ddlItemEditorNewAffinity.SelectedItem = target;
                ddlItemEditorNewAffinity_SelectedIndexChanged(ddlItemEditorNewAffinity, EventArgs.Empty);
            }
            ClearAndRepopulateStatModControls(dto);
        }

        /// <summary>
        /// Build a compact preview string for the "original details" textbox.
        /// This is informational only; the authoritative round-trip is handled
        /// by the Core writer.
        /// </summary>
        private static string BuildPreviewText(ItemDto dto)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"ITEMCREATE: \"{dto.Name}\", \"{dto.Type}\", \"{dto.SubType}\", \"{dto.Style}\", {dto.StarRating ?? 0}");
            if (dto.DescriptionId.HasValue) sb.AppendLine($"\tITEMDESCRIPTIONID: {dto.DescriptionId}");
            if (dto.DisplayNameId.HasValue) sb.AppendLine($"\tITEMDISPLAYNAMEID: {dto.DisplayNameId}");
            if (dto.Cost.HasValue) sb.AppendLine($"\tITEMCOST: {dto.Cost}");
            if (dto.MinLevel.HasValue) sb.AppendLine($"\tITEMMINLEVEL: {dto.MinLevel}");
            if (dto.Mesh is not null) sb.AppendLine($"\tITEMMESH: \"{dto.Mesh}\"");
            if (dto.Mesh2 is not null) sb.AppendLine($"\tITEMMESH2: \"{dto.Mesh2}\"");
            if (dto.Material is not null) sb.AppendLine($"\tITEMMATERIAL: \"{dto.Material}\"");
            if (dto.Affinity.HasValue) sb.AppendLine($"\tITEMAFFINITY: {dto.Affinity.Value.Element}, {dto.Affinity.Value.Amount}");
            foreach (var s in dto.Skills) sb.AppendLine($"\tITEMSKILL: \"{s}\"");
            foreach (var a in dto.Attributes) sb.AppendLine($"\tITEMATTRIBUTE: \"{a}\"");
            foreach (var r in dto.Regions) sb.AppendLine($"\tITEMREGION: \"{r}\"");
            foreach (var h in dto.HideSets) sb.AppendLine($"\tITEMHIDESET: \"{h}\"");
            foreach (var sm in dto.StatMods) sb.AppendLine($"\tITEMSTATMOD: {sm.Stat}, {sm.Value}");
            return sb.ToString();
        }

        private void UpdateSkillDropDowns(ItemDto dto)
        {
            var noneOpt = new SkillInfo { InternalName = string.Empty, DisplayName = "NONE" };
            List<SkillInfo> skillDataSource = new List<SkillInfo> { noneOpt };
            skillDataSource.AddRange(_itemOnlySkills);

            ddlItemEditorNewSkill1.DataSource = new List<SkillInfo>(skillDataSource);
            ddlItemEditorNewSkill1.DisplayMember = nameof(SkillInfo.DisplayName);
            ddlItemEditorNewSkill1.ValueMember = nameof(SkillInfo.InternalName);
            string skill1 = dto.Skills.ElementAtOrDefault(0) ?? string.Empty;
            ddlItemEditorNewSkill1.SelectedValue = skillDataSource.FirstOrDefault(s => s.InternalName.Equals(skill1, StringComparison.OrdinalIgnoreCase))?.InternalName ?? string.Empty;

            ddlItemEditorNewSkill2.DataSource = new List<SkillInfo>(skillDataSource);
            ddlItemEditorNewSkill2.DisplayMember = nameof(SkillInfo.DisplayName);
            ddlItemEditorNewSkill2.ValueMember = nameof(SkillInfo.InternalName);
            string skill2 = dto.Skills.ElementAtOrDefault(1) ?? string.Empty;
            ddlItemEditorNewSkill2.SelectedValue = skillDataSource.FirstOrDefault(s => s.InternalName.Equals(skill2, StringComparison.OrdinalIgnoreCase))?.InternalName ?? string.Empty;
        }

        private async void btnItemEditorNewSave_Click(object? sender, EventArgs e)
        {
            if (_currentDto is null)
            {
                MessageBox.Show("No item selected.");
                return;
            }
            var dto = _currentDto;

            // ---- Build ItemPatch from UI state. Only include fields that changed. ----

            var patch = new ItemPatch();
            bool anyChange = false;

            // Cost
            if (!int.TryParse(txtItemEditorNewCost.Text, out int costVal))
            {
                MessageBox.Show("Invalid Cost value.");
                return;
            }
            if ((dto.Cost ?? 0) != costVal) { patch.Cost = costVal; anyChange = true; }

            // MinLevel
            if (!int.TryParse(txtItemEditorNewLevel.Text, out int lvlVal))
            {
                MessageBox.Show("Invalid MinLevel value.");
                return;
            }
            if ((dto.MinLevel ?? 0) != lvlVal) { patch.MinLevel = lvlVal; anyChange = true; }

            // Affinity
            if (ddlItemEditorNewAffinity.SelectedItem is string selAffType)
            {
                if (selAffType == "NONE")
                {
                    // If the item currently has an affinity, clear it.
                    if (dto.Affinity.HasValue) { patch.ClearAffinity = true; anyChange = true; }
                }
                else
                {
                    if (!int.TryParse(txtItemEditorNewAffinity.Text, out int parsedAffVal))
                    {
                        MessageBox.Show($"Invalid affinity value for '{selAffType}'.");
                        return;
                    }
                    bool same = dto.Affinity.HasValue
                                && dto.Affinity.Value.Element.Equals(selAffType, StringComparison.OrdinalIgnoreCase)
                                && dto.Affinity.Value.Amount == parsedAffVal;
                    if (!same) { patch.Affinity = (selAffType, parsedAffVal); anyChange = true; }
                }
            }

            // Skills (list field: compute diff, turn into Add/Remove verbs)
            var uiSkills = new List<string>();
            if (ddlItemEditorNewSkill1.SelectedValue is string sk1 && !string.IsNullOrEmpty(sk1)) uiSkills.Add(sk1);
            if (ddlItemEditorNewSkill2.SelectedValue is string sk2 && !string.IsNullOrEmpty(sk2)
                && !uiSkills.Contains(sk2, StringComparer.OrdinalIgnoreCase)) uiSkills.Add(sk2);

            var oldSkills = dto.Skills.ToList();
            var toRemove = oldSkills.Where(s => !uiSkills.Contains(s, StringComparer.OrdinalIgnoreCase)).ToList();
            var toAdd = uiSkills.Where(s => !oldSkills.Contains(s, StringComparer.OrdinalIgnoreCase)).ToList();
            if (toRemove.Count > 0) { patch.SkillsRemove = toRemove; anyChange = true; }
            if (toAdd.Count > 0) { patch.SkillsAdd = toAdd; anyChange = true; }

            // StatMods (list field with tuple values)
            var uiStats = new List<(string Stat, long Value)>();
            foreach (var rowUI in _dynamicStatModControls)
            {
                if (rowUI.StatTypeComboBox.SelectedItem is string statName
                    && !string.IsNullOrWhiteSpace(statName))
                {
                    if (!int.TryParse(rowUI.StatValueTextBox.Text, out int statVal))
                    {
                        MessageBox.Show($"Invalid value for stat '{statName}'.");
                        return;
                    }
                    uiStats.Add((statName, statVal));
                }
            }
            var oldStats = dto.StatMods.ToList();
            bool statsEqual = oldStats.Count == uiStats.Count
                              && oldStats.All(o => uiStats.Any(u => u.Stat.Equals(o.Stat, StringComparison.OrdinalIgnoreCase) && u.Value == o.Value));
            if (!statsEqual)
            {
                if (oldStats.Count > 0) patch.StatModsRemove = oldStats;
                if (uiStats.Count > 0) patch.StatModsAdd = uiStats;
                anyChange = true;
            }

            // ---- Lookup-table (txt) updates: DisplayName / Description text ----

            bool lookupChanged = false;
            string oldDisplayName = ResolveDisplayName(dto);
            string oldDescription = ResolveDescription(dto);

            try
            {
                if (dto.DisplayNameId.HasValue && txtItemEditorNewName.Text != oldDisplayName)
                {
                    if (!_lookupTextApi.Update(_becFolder, (int)dto.DisplayNameId.Value, txtItemEditorNewName.Text))
                    {
                        MessageBox.Show($"Display-name lookup id {dto.DisplayNameId.Value} was not found.", "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    _lookupTable[(int)dto.DisplayNameId.Value] = txtItemEditorNewName.Text;
                    lookupChanged = true;
                }

                if (!dto.DescriptionId.HasValue && !string.IsNullOrWhiteSpace(txtItemEditorNewDescription.Text))
                {
                    int newDescId = _lookupTextApi.Add(_becFolder, txtItemEditorNewDescription.Text);
                    _lookupTable[newDescId] = txtItemEditorNewDescription.Text;
                    patch.DescriptionId = newDescId;
                    anyChange = true;
                    lookupChanged = true;
                }
                else if (dto.DescriptionId.HasValue && txtItemEditorNewDescription.Text != oldDescription)
                {
                    if (!_lookupTextApi.Update(_becFolder, (int)dto.DescriptionId.Value, txtItemEditorNewDescription.Text))
                    {
                        MessageBox.Show($"Description lookup id {dto.DescriptionId.Value} was not found.", "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    _lookupTable[(int)dto.DescriptionId.Value] = txtItemEditorNewDescription.Text;
                    lookupChanged = true;
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Lookup text save failed: {ex.Message}", Color.Red, rtbItemEditor);
                MessageBox.Show($"Lookup text save failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!anyChange && !lookupChanged)
            {
                MessageBox.Show("No changes detected to save.", "No Changes");
                return;
            }

            // ---- Apply patch through the API (off the UI thread). ----

            if (anyChange)
            {
                try
                {
                    string stableId = dto.Name;
                    ItemDto updated = await Task.Run(() => _itemsApi.Patch(_becFolder, stableId, patch));
                    _currentDto = updated;
                    // Keep the cache entry fresh so subsequent selections don't
                    // read the old data until the full reload below completes.
                    _itemDtoCache[updated.Name] = updated;
                }
                catch (Exception ex)
                {
                    AppendLog($"Save failed: {ex.Message}", Color.Red, rtbItemEditor);
                    MessageBox.Show($"Save failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            AppendLog($"Item '{dto.Name}' saved.", Color.Green, rtbItemEditor);

            // Refresh list + re-select same item so the dropdown label updates
            // if the display name changed.
            string selected = dto.Name;
            try { LoadItemsFromApi(); }
            catch (Exception ex) { AppendLog($"Reload failed: {ex.Message}", Color.Red); return; }

            ddlItemEditorAllItems.SelectedIndexChanged -= ddlItemEditorAllItems_SelectedIndexChanged;
            ddlItemEditorAllItems.DataSource = _bindingListOfItems;
            var reSelected = _bindingListOfItems.FirstOrDefault(r => r.Name == selected);
            if (reSelected != null) ddlItemEditorAllItems.SelectedItem = reSelected;
            else if (_bindingListOfItems.Any()) ddlItemEditorAllItems.SelectedIndex = 0;
            ddlItemEditorAllItems.SelectedIndexChanged += ddlItemEditorAllItems_SelectedIndexChanged;

            if (ddlItemEditorAllItems.SelectedItem != null) ddlItemEditorAllItems_SelectedIndexChanged(ddlItemEditorAllItems, EventArgs.Empty);
            else { ClearEditorFields(); BindAffinityDropdown(); ClearAndRepopulateStatModControls(null); }
        }

        private void EnsureItemCrudButtons()
        {
            if (_btnItemEditorAdd != null) return;

            _btnItemEditorAdd = new Button { Text = "Add", Location = new Point(251, 5), Size = new Size(70, 25) };
            _btnItemEditorRename = new Button { Text = "Rename", Location = new Point(327, 5), Size = new Size(80, 25) };
            _btnItemEditorRemove = new Button { Text = "Remove", Location = new Point(413, 5), Size = new Size(80, 25) };

            _btnItemEditorAdd.Click += btnItemEditorAdd_Click;
            _btnItemEditorRename.Click += btnItemEditorRename_Click;
            _btnItemEditorRemove.Click += btnItemEditorRemove_Click;

            tabEditorItems.Controls.Add(_btnItemEditorAdd);
            tabEditorItems.Controls.Add(_btnItemEditorRename);
            tabEditorItems.Controls.Add(_btnItemEditorRemove);
        }

        private async void btnItemEditorAdd_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_becFolder)) { MessageBox.Show("Select a project first."); return; }

            using var dialog = new AddItemDialog(_affinityTypes, _itemOnlySkills);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var dto = dialog.BuildDto();
            try
            {
                if (_itemsApi.Get(_becFolder, dto.Name) is not null)
                {
                    MessageBox.Show($"Item '{dto.Name}' already exists.", "Duplicate Item", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(dialog.DisplayNameText))
                {
                    int id = await Task.Run(() => _lookupTextApi.Add(_becFolder, dialog.DisplayNameText));
                    _lookupTable[id] = dialog.DisplayNameText;
                    dto.DisplayNameId = id;
                }

                if (!string.IsNullOrWhiteSpace(dialog.DescriptionText))
                {
                    int id = await Task.Run(() => _lookupTextApi.Add(_becFolder, dialog.DescriptionText));
                    _lookupTable[id] = dialog.DescriptionText;
                    dto.DescriptionId = id;
                }

                await Task.Run(() => _itemsApi.Add(_becFolder, dto));
                AppendLog($"Item '{dto.Name}' added.", Color.Green, rtbItemEditor);
                ReloadAndSelect(dto.Name);
            }
            catch (Exception ex)
            {
                AppendLog($"Add item failed: {ex.Message}", Color.Red, rtbItemEditor);
                MessageBox.Show($"Add item failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnItemEditorRename_Click(object? sender, EventArgs e)
        {
            if (_currentDto is null) { MessageBox.Show("No item selected."); return; }

            string? newName = PromptForText("Rename Item", "New internal item name:", _currentDto.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == _currentDto.Name) return;

            try
            {
                await Task.Run(() => _itemsApi.Rename(_becFolder, _currentDto.Name, newName));
                AppendLog($"Item '{_currentDto.Name}' renamed to '{newName}'.", Color.Green, rtbItemEditor);
                ReloadAndSelect(newName);
            }
            catch (Exception ex)
            {
                AppendLog($"Rename failed: {ex.Message}", Color.Red, rtbItemEditor);
                MessageBox.Show($"Rename failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnItemEditorRemove_Click(object? sender, EventArgs e)
        {
            if (_currentDto is null) { MessageBox.Show("No item selected."); return; }

            string name = _currentDto.Name;
            var result = MessageBox.Show(
                $"Remove item '{name}' from items.tok? This creates a backup but may break references in itemsets, shops, or encounters.",
                "Remove Item",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            try
            {
                bool removed = await Task.Run(() => _itemsApi.Remove(_becFolder, name));
                if (!removed) { MessageBox.Show($"Item '{name}' was not found."); return; }

                AppendLog($"Item '{name}' removed.", Color.Green, rtbItemEditor);
                ReloadAndSelect(null);
            }
            catch (Exception ex)
            {
                AppendLog($"Remove failed: {ex.Message}", Color.Red, rtbItemEditor);
                MessageBox.Show($"Remove failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReloadAndSelect(string? itemName)
        {
            LoadItemsFromApi();
            ddlItemEditorAllItems.SelectedIndexChanged -= ddlItemEditorAllItems_SelectedIndexChanged;
            ddlItemEditorAllItems.DataSource = _bindingListOfItems;

            ItemRow? row = itemName is null ? null : _bindingListOfItems.FirstOrDefault(r => r.Name == itemName);
            if (row is not null) ddlItemEditorAllItems.SelectedItem = row;
            else if (_bindingListOfItems.Any()) ddlItemEditorAllItems.SelectedIndex = 0;

            ddlItemEditorAllItems.SelectedIndexChanged += ddlItemEditorAllItems_SelectedIndexChanged;

            if (ddlItemEditorAllItems.SelectedItem != null) ddlItemEditorAllItems_SelectedIndexChanged(ddlItemEditorAllItems, EventArgs.Empty);
            else { ClearEditorFields(); BindAffinityDropdown(); ClearAndRepopulateStatModControls(null); }
        }

        private static string? PromptForText(string title, string label, string initialValue)
        {
            using var form = new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(420, 120)
            };
            var lbl = new Label { Text = label, AutoSize = true, Location = new Point(12, 12) };
            var txt = new TextBox { Text = initialValue, Location = new Point(12, 35), Width = 395 };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(251, 78), Width = 75 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(332, 78), Width = 75 };
            form.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
            form.AcceptButton = ok;
            form.CancelButton = cancel;
            return form.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : null;
        }

        private sealed class AddItemDialog : Form
        {
            private readonly TextBox _name = new() { Width = 260 };
            private readonly TextBox _displayName = new() { Width = 260 };
            private readonly TextBox _description = new() { Width = 260, Multiline = true, Height = 60 };
            private readonly TextBox _type = new() { Width = 260 };
            private readonly TextBox _subType = new() { Width = 260 };
            private readonly TextBox _style = new() { Width = 260, Text = "plain" };
            private readonly NumericUpDown _stars = new() { Width = 80, Minimum = 0, Maximum = 99 };
            private readonly NumericUpDown _cost = new() { Width = 120, Minimum = 0, Maximum = 9999999 };
            private readonly NumericUpDown _level = new() { Width = 120, Minimum = 0, Maximum = 999 };
            private readonly TextBox _rarity = new() { Width = 260 };
            private readonly TextBox _mesh = new() { Width = 260 };
            private readonly TextBox _mesh2 = new() { Width = 260 };
            private readonly TextBox _material = new() { Width = 260 };
            private readonly ComboBox _affinity = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            private readonly NumericUpDown _affinityAmount = new() { Width = 120, Minimum = 0, Maximum = 999 };
            private readonly ComboBox _skill1 = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            private readonly ComboBox _skill2 = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            private readonly TextBox _regions = new() { Width = 260 };
            private readonly TextBox _attributes = new() { Width = 260 };
            private readonly TextBox _hideSets = new() { Width = 260 };
            private readonly TextBox _showSet = new() { Width = 260 };
            private readonly TextBox _statMods = new() { Width = 260, Multiline = true, Height = 50 };

            public string DisplayNameText => _displayName.Text.Trim();
            public string DescriptionText => _description.Text.Trim();

            public AddItemDialog(IEnumerable<string> affinityTypes, IReadOnlyList<SkillInfo> itemSkills)
            {
                Text = "Add Item";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MinimizeBox = false;
                MaximizeBox = false;
                ClientSize = new Size(610, 720);

                _affinity.Items.AddRange(affinityTypes.Cast<object>().ToArray());
                _affinity.SelectedItem = "NONE";

                var skills = new List<SkillInfo> { new SkillInfo { InternalName = string.Empty, DisplayName = "NONE" } };
                skills.AddRange(itemSkills);
                BindSkillCombo(_skill1, skills);
                BindSkillCombo(_skill2, skills);

                var panel = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoScroll = true,
                    ColumnCount = 2,
                    RowCount = 0,
                    Padding = new Padding(10),
                    Height = 660
                };
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                AddRow(panel, "Internal Name *", _name);
                AddRow(panel, "Display Name", _displayName);
                AddRow(panel, "Description", _description);
                AddRow(panel, "Type *", _type);
                AddRow(panel, "SubType *", _subType);
                AddRow(panel, "Style *", _style);
                AddRow(panel, "Star Rating", _stars);
                AddRow(panel, "Cost", _cost);
                AddRow(panel, "Min Level", _level);
                AddRow(panel, "Rarity", _rarity);
                AddRow(panel, "Mesh", _mesh);
                AddRow(panel, "Mesh2", _mesh2);
                AddRow(panel, "Material", _material);
                AddRow(panel, "Affinity", _affinity);
                AddRow(panel, "Affinity Amount", _affinityAmount);
                AddRow(panel, "Skill 1", _skill1);
                AddRow(panel, "Skill 2", _skill2);
                AddRow(panel, "Regions (comma separated)", _regions);
                AddRow(panel, "Attributes (comma separated)", _attributes);
                AddRow(panel, "HideSets (comma separated)", _hideSets);
                AddRow(panel, "ShowSet", _showSet);
                AddRow(panel, "StatMods (stat=value per line)", _statMods);

                var ok = new Button { Text = "Add Item", DialogResult = DialogResult.OK, Location = new Point(420, 680), Width = 80 };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(510, 680), Width = 80 };
                ok.Click += (_, _) => ValidateInputs();

                Controls.Add(panel);
                Controls.Add(ok);
                Controls.Add(cancel);
                AcceptButton = ok;
                CancelButton = cancel;
            }

            public ItemDto BuildDto()
            {
                var dto = new ItemDto
                {
                    Name = _name.Text.Trim(),
                    Type = _type.Text.Trim(),
                    SubType = _subType.Text.Trim(),
                    Style = _style.Text.Trim(),
                    StarRating = (long)_stars.Value,
                    Cost = (long)_cost.Value,
                    MinLevel = (long)_level.Value,
                    Rarity = EmptyToNull(_rarity.Text),
                    Mesh = EmptyToNull(_mesh.Text),
                    Mesh2 = EmptyToNull(_mesh2.Text),
                    Material = EmptyToNull(_material.Text),
                    ShowSet = EmptyToNull(_showSet.Text)
                };

                if (_affinity.SelectedItem is string aff && aff != "NONE")
                    dto.Affinity = (aff, (long)_affinityAmount.Value);

                AddSkill(dto, _skill1);
                AddSkill(dto, _skill2);
                dto.Regions.AddRange(SplitCsv(_regions.Text));
                dto.Attributes.AddRange(SplitCsv(_attributes.Text));
                dto.HideSets.AddRange(SplitCsv(_hideSets.Text));
                dto.StatMods.AddRange(ParseStatMods(_statMods.Text));
                return dto;
            }

            private bool ValidateInputs()
            {
                if (string.IsNullOrWhiteSpace(_name.Text)
                    || string.IsNullOrWhiteSpace(_type.Text)
                    || string.IsNullOrWhiteSpace(_subType.Text)
                    || string.IsNullOrWhiteSpace(_style.Text))
                {
                    MessageBox.Show("Internal Name, Type, SubType, and Style are required.", "Missing Fields", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return false;
                }

                try { ParseStatMods(_statMods.Text).ToList(); }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Invalid StatMods", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return false;
                }

                return true;
            }

            private static void AddRow(TableLayoutPanel panel, string label, Control control)
            {
                int row = panel.RowCount++;
                panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                panel.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
                panel.Controls.Add(control, 1, row);
            }

            private static void BindSkillCombo(ComboBox combo, List<SkillInfo> skills)
            {
                combo.DisplayMember = nameof(SkillInfo.DisplayName);
                combo.ValueMember = nameof(SkillInfo.InternalName);
                combo.DataSource = new List<SkillInfo>(skills);
            }

            private static void AddSkill(ItemDto dto, ComboBox combo)
            {
                if (combo.SelectedValue is string skill && !string.IsNullOrWhiteSpace(skill) && !dto.Skills.Contains(skill))
                    dto.Skills.Add(skill);
            }

            private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            private static IEnumerable<string> SplitCsv(string value) =>
                value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            private static IEnumerable<(string Stat, long Value)> ParseStatMods(string value)
            {
                foreach (var raw in value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var parts = raw.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || !long.TryParse(parts[1], out var amount))
                        throw new InvalidOperationException("StatMods must use one 'stat=value' pair per line.");
                    yield return (parts[0], amount);
                }
            }
        }
    }
}
