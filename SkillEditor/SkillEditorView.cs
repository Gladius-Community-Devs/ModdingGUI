namespace ModdingGUI;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModdingGUI.Core.Api;
using ModdingGUI.Core.Skills;

/// <summary>
/// Self-contained UserControl for browsing and editing <c>skills.tok</c>
/// records. Backed by <see cref="ISkillsApi"/>; all writes go through the API
/// (project lock + timestamped backup + write).
/// </summary>
/// <remarks>
/// The SKILL Box is the source of truth for Save: the user may edit it
/// directly, or use the "Add Field" helper panel to append catalog-driven
/// lines. Either way, Save parses the box verbatim and calls
/// <see cref="ISkillsApi.Update"/> or <see cref="ISkillsApi.Add"/>.
/// </remarks>
public sealed partial class SkillEditorView : UserControl
{
    // ---- state ------------------------------------------------------------

    private readonly ISkillsApi _api = new SkillsApi();
    private string   _projectFolder = string.Empty;
    private SkillDto? _current;
    private bool     _isNew;

    /// <summary>Shipped field/value catalog (loaded from <c>&lt;app&gt;/skills/</c>).</summary>
    private SkillsCatalog _catalog = SkillsCatalog.Empty;

    /// <summary>Per-project context (meter names, lookup text); reset on project change.</summary>
    private SkillsContext? _skillsContext;

    // Tracks a pending HandleCreated subscription so it can be replaced if
    // InitializeEditor is called again before the tab is first shown.
    private EventHandler? _pendingLoadHandler;

    // ---- controls ---------------------------------------------------------

    // Top bar — skill picker
    private readonly ComboBox   _cmbSkills   = new() { Name = "cmbSkillEditorSkills", DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly Button     _btnNewSkill = new() { Name = "btnSkillEditorNew", Text = "New Skill", Width = 90 };
    private readonly Button     _btnReload   = new() { Name = "btnSkillEditorReload", Text = "Reload",    Width = 70 };

    // Identity panel
    private readonly TextBox   _txtName    = new() { Name = "txtSkillEditorName", Width = 180 };
    private readonly ComboBox  _cmbType    = new() { Name = "cmbSkillEditorType", DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly ComboBox  _cmbSubType = new() { Name = "cmbSkillEditorSubType", DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };

    // Skill Box
    private readonly TextBox _txtSkillBox = new()
    {
        Name        = "txtSkillEditorBody",
        Multiline   = true,
        ScrollBars  = ScrollBars.Vertical,
        Font        = new Font("Consolas", 9f),
        Dock        = DockStyle.Fill,
        AcceptsReturn = true,
        WordWrap    = false,
    };

    // Add-field helper panel
    private readonly ComboBox         _cmbField    = new() { Name = "cmbSkillEditorField", DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly FlowLayoutPanel  _flpSlots    = new() { AutoSize = false, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly Button           _btnAddField = new() { Name = "btnSkillEditorAddField", Text = "Add Field", Width = 90 };

    // Bottom bar
    private readonly Button _btnSave   = new() { Name = "btnSkillEditorSave", Text = "Save to Project", Width = 120, Enabled = false };
    private readonly Button _btnDelete = new() { Name = "btnSkillEditorDelete", Text = "Delete Skill",    Width = 100, Enabled = false };
    private readonly Button _btnValidate = new() { Name = "btnSkillEditorValidate", Text = "Validate",      Width = 80,  Enabled = false };
    private readonly Label  _lblStatus = new() { Name = "lblSkillEditorStatus", AutoSize = true };

    // ---- fixed values -----------------------------------------------------

    private static readonly string[] SkillTypes =
    {
        "Attack", "Command", "Innate", "Secondary", "Support"
    };

    private static readonly string[] SkillSubTypes =
    {
        "Breakout", "Combo", "Continue", "Crit Cause", "Death Effect", "Defend",
        "Effect", "Hit Cause", "Hit Return", "Miss Return", "One Time", "Ongoing",
        "Pass", "Standard", "Status", "Surprise"
    };

    // -----------------------------------------------------------------------

    public SkillEditorView()
    {
        InitializeComponent();
        BuildUi();
        LoadCatalog();
    }

    // ---- public API -------------------------------------------------------

    /// <summary>The project this view is bound to, or empty when none is loaded.</summary>
    public string ProjectFolder => _projectFolder;

    /// <summary>
    /// Drop everything tied to a project. Called when that project is deleted, so Save cannot target a folder that
    /// no longer exists.
    /// </summary>
    public void ReleaseProject()
    {
        _projectFolder = string.Empty;
        _skillsContext = null;
        ClearEditor();
        _cmbSkills.Items.Clear();
        _btnSave.Enabled = false;
        _btnDelete.Enabled = false;
        SetStatus("No project selected.", isError: false);
    }

    /// <summary>
    /// Point the editor at a project. <paramref name="projectRootPath"/> is the
    /// top-level project folder (the one containing <c>&lt;name&gt;_BEC</c>).
    /// Triggers an async load of the skills list.
    /// </summary>
    public void InitializeEditor(string projectRootPath)
    {
        // A project can be deleted while the tree is being rebuilt; do not start a load against a dead folder.
        if (!Directory.Exists(projectRootPath))
        {
            ReleaseProject();
            SetStatus($"Project '{Path.GetFileName(projectRootPath)}' is no longer on disk.", isError: true);
            return;
        }

        _projectFolder = projectRootPath.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _skillsContext = null;   // Invalidate per-project cache on project change

        ClearEditor();
        _cmbSkills.Items.Clear();
        _btnSave.Enabled   = false;
        _btnDelete.Enabled = false;
        SetStatus("Loading skills…", isError: false);

        _ = LoadSkillsSafeAsync();
    }

    // ---- UI build ---------------------------------------------------------

    private void BuildUi()
    {
        this.Dock = DockStyle.Fill;

        // ── Top bar ─────────────────────────────────────────────────────────
        var pnlTop = new Panel { Dock = DockStyle.Top, Height = 38 };
        var flwTop = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            Padding       = new Padding(4, 6, 0, 0),
        };
        flwTop.Controls.Add(new Label { Text = "Skill:", AutoSize = true, Margin = new Padding(0, 4, 4, 0) });
        _cmbSkills.Margin = new Padding(0, 0, 6, 0);
        flwTop.Controls.Add(_cmbSkills);
        _btnNewSkill.Margin = new Padding(0, 0, 5, 0);
        flwTop.Controls.Add(_btnNewSkill);
        _btnReload.Margin = new Padding(0, 0, 5, 0);
        flwTop.Controls.Add(_btnReload);
        pnlTop.Controls.Add(flwTop);

        // ── Bottom bar ───────────────────────────────────────────────────────
        var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 38 };
        var flwBottom = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding       = new Padding(4, 6, 0, 0),
        };
        _btnSave.Margin     = new Padding(0, 0, 6, 0);
        _btnDelete.Margin   = new Padding(0, 0, 6, 0);
        _btnValidate.Margin = new Padding(0, 0, 10, 0);
        _lblStatus.Margin   = new Padding(0, 4, 0, 0);
        flwBottom.Controls.AddRange(new Control[] { _btnSave, _btnDelete, _btnValidate, _lblStatus });
        pnlBottom.Controls.Add(flwBottom);

        // ── Left sidebar (fixed width, Dock=Left) ────────────────────────────
        // Uses Panel+Dock instead of SplitContainer so widths resolve at render
        // time rather than construction time (avoids SplitterDistance clamping).
        var pnlLeft = new Panel { Dock = DockStyle.Left, Width = 260 };

        // Identity GroupBox — fixed height at top of left panel
        var grpId = new GroupBox
        {
            Text    = "Skill Identity",
            Dock    = DockStyle.Top,
            Height  = 145,
            Padding = new Padding(6),
        };
        var tblId = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            RowCount    = 3,
            Padding     = new Padding(2),
        };
        tblId.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
        tblId.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tblId.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tblId.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tblId.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        tblId.Controls.Add(MakeLabel("Name:"),    0, 0);
        _txtName.Dock = DockStyle.Fill;
        tblId.Controls.Add(_txtName, 1, 0);

        tblId.Controls.Add(MakeLabel("Type:"),    0, 1);
        _cmbType.Dock = DockStyle.Fill;
        foreach (var t in SkillTypes)    _cmbType.Items.Add(t);
        tblId.Controls.Add(_cmbType, 1, 1);

        tblId.Controls.Add(MakeLabel("SubType:"), 0, 2);
        _cmbSubType.Dock = DockStyle.Fill;
        foreach (var s in SkillSubTypes) _cmbSubType.Items.Add(s);
        tblId.Controls.Add(_cmbSubType, 1, 2);

        grpId.Controls.Add(tblId);

        // Add-field GroupBox — fills remainder of left panel
        var grpAdd = new GroupBox { Text = "Add Field", Dock = DockStyle.Fill, Padding = new Padding(6) };
        var tblAdd = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 3,
        };
        tblAdd.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));   // field picker
        tblAdd.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // slot controls
        tblAdd.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));   // button

        _cmbField.Dock = DockStyle.Fill;
        tblAdd.Controls.Add(_cmbField, 0, 0);

        _flpSlots.Dock = DockStyle.Fill;
        tblAdd.Controls.Add(_flpSlots, 0, 1);

        var flwAddBtn = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        flwAddBtn.Controls.Add(_btnAddField);
        tblAdd.Controls.Add(flwAddBtn, 0, 2);

        grpAdd.Controls.Add(tblAdd);

        // Controls must be added Fill-first, then Top (WinForms layout order).
        pnlLeft.Controls.Add(grpAdd);   // Dock=Fill — add first
        pnlLeft.Controls.Add(grpId);    // Dock=Top  — add second (renders above Fill)

        // ── Right area: label + SKILL Box ────────────────────────────────────
        var pnlRight = new Panel { Dock = DockStyle.Fill };
        var lblBox = new Label
        {
            Text    = "Body Fields (SKILL Box) — edit directly or use Add Field:",
            Dock    = DockStyle.Top,
            Height  = 22,
            Padding = new Padding(2, 4, 0, 0),
        };
        pnlRight.Controls.Add(_txtSkillBox);   // Dock=Fill — add first
        pnlRight.Controls.Add(lblBox);          // Dock=Top  — add second

        // ── Assemble (Fill before Left/Bottom/Top) ───────────────────────────
        this.Controls.Add(pnlRight);    // Fill
        this.Controls.Add(pnlLeft);     // Left
        this.Controls.Add(pnlBottom);   // Bottom
        this.Controls.Add(pnlTop);      // Top

        // ── Wire events ─────────────────────────────────────────────────────
        _cmbSkills.SelectedIndexChanged += CmbSkills_SelectedIndexChanged;
        _btnNewSkill.Click              += BtnNewSkill_Click;
        _btnReload.Click                += BtnReload_Click;
        _cmbField.SelectedIndexChanged  += CmbField_SelectedIndexChanged;
        _btnAddField.Click              += BtnAddField_Click;
        _btnSave.Click                  += BtnSave_Click;
        _btnDelete.Click                += BtnDelete_Click;
        _btnValidate.Click              += BtnValidate_Click;
    }

    private static Label MakeLabel(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 6, 4, 0) };

    // ---- catalog loading --------------------------------------------------

    private void LoadCatalog()
    {
        // Core owns catalog parsing; a missing/broken catalog degrades to an
        // empty lookup so the editor stays usable.
        _catalog = SkillsCatalog.FindForProject(null, AppContext.BaseDirectory) ?? SkillsCatalog.Empty;

        _cmbField.Items.Clear();
        foreach (string fieldName in _catalog.FieldNames)
            _cmbField.Items.Add(fieldName);

        if (_cmbField.Items.Count > 0)
            _cmbField.SelectedIndex = 0;
    }

    // ---- async skill list load --------------------------------------------

    /// <summary>Fire-and-forget load that reports a failure instead of leaving an unobserved task exception.</summary>
    private async Task LoadSkillsSafeAsync()
    {
        try
        {
            await LoadSkillsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Loading skills failed: {ex.Message}", isError: true);
        }
    }

    private async Task LoadSkillsAsync()
    {
        if (string.IsNullOrEmpty(_projectFolder)) return;

        List<string>? names = null;
        Exception?    error = null;

        try
        {
            string bec = ProjectPaths.BecFolder(_projectFolder);

            // The folder may have been deleted between the guard above and this call.
            if (!Directory.Exists(bec))
            {
                error = new DirectoryNotFoundException("The project folder no longer exists.");
            }
            else
            {
                names = await Task.Run(() => _api.List(bec).ToList());
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }

        void apply()
        {
            if (error is not null)
            {
                SetStatus($"Failed to load skills: {error.Message}", isError: true);
                return;
            }
            PopulateSkillSelector(names!);
            SetStatus($"Loaded {names!.Count} skills.", isError: false);
        }

        if (this.IsHandleCreated)
        {
            // Tab is visible — update immediately on the UI thread.
            this.BeginInvoke((MethodInvoker)apply);
        }
        else
        {
            // Tab has never been shown yet; WinForms hasn't created the window
            // handle for controls on inactive tabs.  Queue the update for the
            // moment the handle is first created (i.e. when the user opens the
            // Editors tab).  Cancel any previous pending handler first so that
            // selecting multiple projects before opening the tab only applies
            // the latest result.
            if (_pendingLoadHandler is not null)
                this.HandleCreated -= _pendingLoadHandler;

            EventHandler? handler = null;
            handler = (_, _) =>
            {
                this.HandleCreated -= handler;
                _pendingLoadHandler = null;
                this.BeginInvoke((MethodInvoker)apply);
            };
            _pendingLoadHandler = handler;
            this.HandleCreated += handler;
        }
    }

    private void PopulateSkillSelector(List<string> names, bool selectFirst = true)
    {
        _cmbSkills.SelectedIndexChanged -= CmbSkills_SelectedIndexChanged;
        _cmbSkills.Items.Clear();
        var sorted = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var n in sorted)
            _cmbSkills.Items.Add(n);
        _cmbSkills.SelectedIndexChanged += CmbSkills_SelectedIndexChanged;

        if (selectFirst && _cmbSkills.Items.Count > 0) _cmbSkills.SelectedIndex = 0;
    }

    // ---- event handlers ---------------------------------------------------

    private void CmbSkills_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_cmbSkills.SelectedItem is not string name) return;
        LoadSkill(name);
    }

    private void BtnNewSkill_Click(object? sender, EventArgs e)
    {
        _cmbSkills.SelectedIndexChanged -= CmbSkills_SelectedIndexChanged;
        _cmbSkills.SelectedIndex = -1;
        _cmbSkills.SelectedIndexChanged += CmbSkills_SelectedIndexChanged;

        ClearEditor();
        _txtName.Text = string.Empty;
        _isNew = true;
        _current = null;
        _btnSave.Enabled   = true;
        _btnDelete.Enabled = false;
        _btnValidate.Enabled = true;
        SetStatus("Enter skill details, then click Save to Project.", isError: false);
    }

    private void BtnReload_Click(object? sender, EventArgs e)
    {
        SetStatus("Reloading…", isError: false);
        _ = LoadSkillsSafeAsync();
    }

    private void CmbField_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _flpSlots.Controls.Clear();
        if (_cmbField.SelectedItem is not string fieldName) return;

        // Catalog entry may be absent for dynamically-handled fields.
        var field = _catalog.Find(fieldName);

        // Show a one-line summary; the full derivation lives behind a details toggle.
        if (field is not null && !field.Enumerate && !string.IsNullOrWhiteSpace(field.Note))
            AddFieldNote(field);

        // ── Special dynamic dropdowns ─────────────────────────────────────────

        // SKILLMETER: IDs parsed from skillmeter.tok at runtime.
        if (fieldName.Equals("SKILLMETER", StringComparison.OrdinalIgnoreCase))
        {
            BuildMeterSlots();
            return;
        }

        // Animation fields: names resolved from classdefs + character folders.
        if (fieldName.Equals("SKILLANIM",      StringComparison.OrdinalIgnoreCase) ||
            fieldName.Equals("SKILLANIMSPEED", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Equals("SKILLANIMTIME",  StringComparison.OrdinalIgnoreCase))
        {
            BuildAnimSlots(fieldName);
            return;
        }

        // ── Normal catalog-driven slots ───────────────────────────────────────

        if (field is null) return;

        foreach (var slot in field.Slots)
        {
            bool hasValues = slot.Values.Count > 0;
            bool enumerate = field.Enumerate || (slot.Enumerate == true);

            Control ctrl;
            if (hasValues && enumerate)
            {
                var cmb = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Width = 220,
                    Margin = new Padding(0, 0, 0, 4),
                };
                foreach (var v in slot.Values) cmb.Items.Add(v);
                if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
                ctrl = cmb;
            }
            else
            {
                ctrl = new TextBox
                {
                    Width  = 220,
                    Margin = new Padding(0, 0, 0, 4),
                };
            }
            _flpSlots.Controls.Add(ctrl);
        }
    }

    private void BtnAddField_Click(object? sender, EventArgs e)
    {
        if (_cmbField.SelectedItem is not string fieldName) return;
        var field = _catalog.Find(fieldName);

        var parts     = new List<string>();
        int slotIndex = 0;

        foreach (Control ctrl in _flpSlots.Controls)
        {
            // Labels are display-only info notes — skip them.
            if (ctrl is Label) continue;

            string rawVal = ctrl switch
            {
                ComboBox cmb => cmb.SelectedItem as string ?? cmb.Text.Trim(),
                TextBox  txt => txt.Text.Trim(),
                _            => string.Empty,
            };

            rawVal = ApplySlotQuoting(fieldName, field, slotIndex, rawVal);

            parts.Add(rawVal);
            slotIndex++;
        }

        if (parts.Count == 0)
        {
            // No input controls (only labels, or no slots) — add blank.
            parts.Add(string.Empty);
        }

        string line = $"{fieldName}: {string.Join(", ", parts)}";

        // If the field already exists in the box, overwrite that line.
        // Otherwise append so new fields are added at the end.
        string prefix = fieldName + ":";
        var lines = _txtSkillBox.Text
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .ToList();
        int existing = lines.FindIndex(l => l.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            lines[existing] = line;
            _txtSkillBox.Text = string.Join(Environment.NewLine, lines);
        }
        else
        {
            string current = _txtSkillBox.Text.TrimEnd('\r', '\n');
            _txtSkillBox.Text = current.Length > 0
                ? current + Environment.NewLine + line
                : line;
        }
        _txtSkillBox.SelectionStart  = _txtSkillBox.Text.Length;
        _txtSkillBox.SelectionLength = 0;
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        var dto = BuildDtoFromUi();
        if (dto is null) return;

        _btnSave.Enabled = false;
        SetStatus("Saving…", isError: false);

        SkillMutationResult? outcome = null;
        Exception? error = null;
        bool wasNew = _isNew;
        string? oldName = _current?.Name;

        try
        {
            // Validation is advisory (PLAN rule 7): Warn mode always writes and
            // hands the findings back so the status bar can report them.
            string bec = ProjectPaths.BecFolder(_projectFolder);
            var catalog = _catalog;
            var context = ContextForProject();
            if (wasNew)
                outcome = await Task.Run(() => _api.Add(bec, dto, SkillValidationMode.Warn, catalog, context));
            else
                outcome = await Task.Run(() => _api.Update(bec, oldName!, dto, SkillValidationMode.Warn, catalog, context));
        }
        catch (Exception ex)
        {
            error = ex;
        }

        _btnSave.Enabled = true;

        if (error is not null)
        {
            // Reported where the result of every other save goes, not in a modal.
            SetStatus($"Save failed: {error.Message}", isError: true);
            return;
        }

        _current = outcome?.Skill;
        _isNew   = false;
        _btnValidate.Enabled = true;

        int warnings = outcome?.Issues.Count(i => i.Severity == SkillValidationSeverity.Warning) ?? 0;
        int notes    = (outcome?.Issues.Count ?? 0) - warnings;
        SetStatus(warnings > 0
                ? $"Skill '{dto.Name}' saved — validation: {warnings} warning(s). Click Validate for details."
                : $"Skill '{dto.Name}' saved." + (notes > 0 ? $" (validation: {notes} note(s))" : string.Empty),
            isError: false);

        // Reload the dropdown and re-select the saved skill.
        List<string>? names = null;
        try { names = await Task.Run(() => _api.List(ProjectPaths.BecFolder(_projectFolder)).ToList()); }
        catch { /* keep existing list */ }

        if (names is not null)
        {
            string target = dto.Name;
            PopulateSkillSelector(names, selectFirst: false);
            int idx = _cmbSkills.Items.IndexOf(target);
            if (idx >= 0)
                _cmbSkills.SelectedIndex = idx;   // handler is attached → LoadSkill fires for the saved skill
            _btnDelete.Enabled = true;
        }
    }

    private void BtnValidate_Click(object? sender, EventArgs e)
    {
        var dto = BuildDtoFromUi();
        if (dto is null) return;

        IReadOnlyList<SkillValidationIssue> issues;
        try
        {
            issues = _api.Validate(ProjectPaths.BecFolder(_projectFolder), dto, _catalog, ContextForProject());
        }
        catch (Exception ex)
        {
            SetStatus($"Validation failed: {ex.Message}", isError: true);
            return;
        }

        ReportValidation(dto.Name, issues);
    }

    /// <summary>
    /// Surfaces advisory findings: summary in the status bar, full list in a
    /// dialog. Warnings are reported but never block a save (PLAN rule 7).
    /// </summary>
    private void ReportValidation(string skillName, IReadOnlyList<SkillValidationIssue> issues)
    {
        int warnings = issues.Count(i => i.Severity == SkillValidationSeverity.Warning);
        int notes    = issues.Count - warnings;

        SetStatus(
            issues.Count == 0
                ? $"Validation: no issues in '{skillName}'."
                : $"Validation: {warnings} warning(s), {notes} note(s) — click Validate for details.",
            isError: warnings > 0);

        if (issues.Count == 0)
        {
            MessageBox.Show($"No validation issues found in '{skillName}'.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var report = new StringBuilder();
        foreach (var issue in issues)
            report.AppendLine($"[{issue.Severity}] {issue.Field} — {issue.Message}");
        report.AppendLine();
        report.AppendLine("Warnings are advisory: saving is never blocked.");

        MessageBox.Show(report.ToString(), $"Validation — {skillName}",
            MessageBoxButtons.OK,
            warnings > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_current is null) return;

        var confirm = MessageBox.Show(
            $"Delete skill '{_current.Name}'? This cannot be undone (a backup will be created).",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        string name = _current.Name;
        _btnDelete.Enabled = false;
        _btnSave.Enabled   = false;
        SetStatus("Deleting…", isError: false);

        Exception? error = null;
        try { await Task.Run(() => _api.Remove(ProjectPaths.BecFolder(_projectFolder), name)); }
        catch (Exception ex) { error = ex; }

        if (error is not null)
        {
            SetStatus($"Delete failed: {error.Message}", isError: true);
            _btnDelete.Enabled = true;
            _btnSave.Enabled   = true;
            return;
        }

        SetStatus($"Skill '{name}' deleted.", isError: false);
        ClearEditor();
        _current = null;

        List<string>? names = null;
        try { names = await Task.Run(() => _api.List(ProjectPaths.BecFolder(_projectFolder)).ToList()); }
        catch { /* keep existing list */ }
        if (names is not null) PopulateSkillSelector(names);
    }

    // ---- helpers ----------------------------------------------------------

    private void LoadSkill(string name)
    {
        if (string.IsNullOrEmpty(_projectFolder)) return;

        SkillDto? dto;
        try   { dto = _api.Get(ProjectPaths.BecFolder(_projectFolder), name); }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", isError: true);
            return;
        }

        if (dto is null)
        {
            SetStatus($"Skill '{name}' not found.", isError: true);
            return;
        }

        _current = dto;
        _isNew   = false;

        PopulateIdentity(dto);
        PopulateSkillBox(dto);

        _btnSave.Enabled   = true;
        _btnDelete.Enabled = true;
        _btnValidate.Enabled = true;
        SetStatus(string.Empty);
    }

    private void PopulateIdentity(SkillDto dto)
    {
        _txtName.Text = dto.Name;

        _cmbType.SelectedItem = dto.SkillType is not null
            ? _cmbType.Items.Cast<string>().FirstOrDefault(
                t => t.Equals(dto.SkillType, StringComparison.OrdinalIgnoreCase))
            : null;

        _cmbSubType.SelectedItem = dto.SkillSubType is not null
            ? _cmbSubType.Items.Cast<string>().FirstOrDefault(
                t => t.Equals(dto.SkillSubType, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private void PopulateSkillBox(SkillDto dto)
    {
        var sb = new StringBuilder();
        foreach (var f in dto.Fields)
        {
            if (sb.Length > 0) sb.Append(Environment.NewLine);
            sb.Append(f.FieldName).Append(": ").Append(f.RawValue);
        }
        _txtSkillBox.Text = sb.ToString();
    }

    private void ClearEditor()
    {
        _txtName.Text     = string.Empty;
        _cmbType.SelectedIndex    = -1;
        _cmbSubType.SelectedIndex = -1;
        _txtSkillBox.Text = string.Empty;
        _isNew   = false;
        _current = null;
    }

    /// <summary>
    /// Build a <see cref="SkillDto"/> from the current UI state.
    /// Returns <c>null</c> and shows an error if validation fails.
    /// </summary>
    private SkillDto? BuildDtoFromUi()
    {
        string name = _txtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            // The status bar is right under the button: a modal for one empty field only stole focus from the field.
            SetStatus("Skill name is required.", isError: true);
            _txtName.Focus();
            return null;
        }

        string type    = _cmbType.SelectedItem    as string ?? string.Empty;
        string subType = _cmbSubType.SelectedItem as string ?? string.Empty;

        var fields = ParseSkillBox(_txtSkillBox.Text);

        return new SkillDto
        {
            Name        = name,
            SkillType   = string.IsNullOrEmpty(type)    ? null : type,
            SkillSubType = string.IsNullOrEmpty(subType) ? null : subType,
            Fields      = fields,
        };
    }

    private static List<SkillFieldEntry> ParseSkillBox(string text)
    {
        var result = new List<SkillFieldEntry>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (var rawLine in text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            int colon = line.IndexOf(':');
            if (colon <= 0) continue;

            string fieldName = line.Substring(0, colon).Trim();
            string rawValue  = (colon + 1 < line.Length)
                ? line.Substring(colon + 1).Trim()
                : string.Empty;

            if (!string.IsNullOrEmpty(fieldName))
                result.Add(new SkillFieldEntry { FieldName = fieldName, RawValue = rawValue });
        }
        return result;
    }

    // ---- dynamic slot builders --------------------------------------------

    /// <summary>
    /// Adds an italic informational (or error) label to <see cref="_flpSlots"/>.
    /// </summary>
    private void AddInfoLabel(string text, bool isError = false)
    {
        var lbl = new Label
        {
            Text        = text,
            AutoSize    = true,
            MaximumSize = new Size(218, 0),
            ForeColor   = isError ? Color.DarkRed : Color.FromArgb(80, 80, 140),
            Font        = new Font(SystemFonts.DefaultFont, FontStyle.Italic),
            Margin      = new Padding(0, 2, 0, 6),
        };
        _flpSlots.Controls.Add(lbl);
    }

    /// <summary>
    /// Field guidance for the Add Field panel: the catalog summary on one line, with the full note
    /// (how the value is derived — engine lookups, file chains) behind a "Full details" toggle. The long
    /// text is developer knowledge, not something a modder needs in the middle of the form.
    /// </summary>
    private void AddFieldNote(SkillFieldSpec field)
    {
        string summary = string.IsNullOrWhiteSpace(field.Summary) ? field.Note! : field.Summary!;

        var lblSummary = new Label
        {
            Text        = summary,
            AutoSize    = true,
            MaximumSize = new Size(218, 0),
            ForeColor   = Color.FromArgb(80, 80, 140),
            Font        = new Font(SystemFonts.DefaultFont, FontStyle.Italic),
            Margin      = new Padding(0, 2, 0, 2),
        };
        _flpSlots.Controls.Add(lblSummary);

        if (string.Equals(summary, field.Note, StringComparison.Ordinal)) return;

        var lblDetails = new Label
        {
            Text        = field.Note!,
            AutoSize    = true,
            MaximumSize = new Size(218, 0),
            ForeColor   = Color.FromArgb(90, 90, 90),
            Font        = new Font(SystemFonts.DefaultFont, FontStyle.Regular),
            Margin      = new Padding(0, 0, 0, 6),
            Visible     = false,
        };

        var lnkDetails = new LinkLabel
        {
            Text     = "Full details",
            AutoSize = true,
            Margin   = new Padding(0, 0, 0, 4),
            Font     = new Font(SystemFonts.DefaultFont, FontStyle.Regular),
        };
        lnkDetails.LinkClicked += (_, _) =>
        {
            lblDetails.Visible = !lblDetails.Visible;
            lnkDetails.Text = lblDetails.Visible ? "Hide details" : "Full details";
        };

        _flpSlots.Controls.Add(lnkDetails);
        _flpSlots.Controls.Add(lblDetails);
    }

    /// <summary>
    /// Builds the slot controls for the SKILLMETER field: a ComboBox populated
    /// with IDs from <c>skillmeter.tok</c>, or an error label + TextBox fallback.
    /// </summary>
    private void BuildMeterSlots()
    {
        var ids = LoadMeterIds();
        if (ids.Count == 0)
        {
            AddInfoLabel("No meter IDs found — is a project loaded?", isError: true);
            _flpSlots.Controls.Add(new TextBox { Width = 220, Margin = new Padding(0, 0, 0, 4) });
            return;
        }

        var cmb = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown,   // allow custom IDs too
            Width         = 220,
            Margin        = new Padding(0, 0, 0, 4),
        };
        foreach (var id in ids) cmb.Items.Add(id);
        if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        _flpSlots.Controls.Add(cmb);
    }

    /// <summary>
    /// Builds slot controls for SKILLANIM / SKILLANIMSPEED / SKILLANIMTIME.
    /// Slot 0 is an animation-name ComboBox (resolved from mesh .pan files);
    /// SKILLANIMSPEED and SKILLANIMTIME get an extra numeric TextBox.
    /// </summary>
    private void BuildAnimSlots(string fieldName)
    {
        var anims = ResolveAnimationNames(out string? errorMsg);
        if (errorMsg is not null)
            AddInfoLabel(errorMsg, isError: true);

        if (anims.Count > 0)
        {
            var cmb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                Width         = 220,
                Margin        = new Padding(0, 0, 0, 4),
            };
            foreach (var a in anims) cmb.Items.Add(a);
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
            _flpSlots.Controls.Add(cmb);
        }
        else
        {
            // Fall back to a plain text box for the animation name.
            _flpSlots.Controls.Add(new TextBox { Width = 220, Margin = new Padding(0, 0, 0, 4) });
        }

        // Extra numeric arg for SKILLANIMSPEED / SKILLANIMTIME.
        if (fieldName.Equals("SKILLANIMSPEED", StringComparison.OrdinalIgnoreCase))
        {
            AddInfoLabel("Speed multiplier: 0.5 = half speed, higher = faster", isError: false);
            _flpSlots.Controls.Add(new TextBox { Width = 220, Margin = new Padding(0, 0, 0, 4) });
        }
        else if (fieldName.Equals("SKILLANIMTIME", StringComparison.OrdinalIgnoreCase))
        {
            AddInfoLabel("Duration in seconds: 0.5 s regardless of natural length", isError: false);
            _flpSlots.Controls.Add(new TextBox { Width = 220, Margin = new Padding(0, 0, 0, 4) });
        }
    }

    // ---- quoting & data helpers -------------------------------------------

    /// <summary>
    /// Applies the slot-quoting rule for a given field and slot index.
    /// Slot 0 of SKILLMETER / SKILLANIM* fields is always quoted.
    /// All other slots follow the catalog <c>Types</c> declaration.
    /// </summary>
    private static string ApplySlotQuoting(
        string fieldName, SkillFieldSpec? field, int slotIndex, string rawVal)
    {
        bool alwaysQuote =
            slotIndex == 0 &&
            (fieldName.Equals("SKILLMETER",     StringComparison.OrdinalIgnoreCase) ||
             fieldName.Equals("SKILLANIM",      StringComparison.OrdinalIgnoreCase) ||
             fieldName.Equals("SKILLANIMSPEED", StringComparison.OrdinalIgnoreCase) ||
             fieldName.Equals("SKILLANIMTIME",  StringComparison.OrdinalIgnoreCase));

        bool isStringSlot = alwaysQuote ||
            (field is not null &&
             slotIndex < field.Slots.Count &&
             field.Slots[slotIndex].Types.Contains("string", StringComparer.OrdinalIgnoreCase));

        if (isStringSlot && !rawVal.StartsWith("\""))
            return $"\"{rawVal}\"";

        return rawVal;
    }

    /// <summary>
    /// Meter names for the <c>SKILLMETER</c> dropdown, resolved by Core from
    /// <c>data/config/swingmeter.tok</c> plus the values observed in this project's
    /// own <c>skills.tok</c>. Result is cached until <see cref="InitializeEditor"/>
    /// is called again.
    /// </summary>
    private IReadOnlyList<string> LoadMeterIds()
    {
        if (string.IsNullOrEmpty(_projectFolder)) return Array.Empty<string>();
        return ContextForProject().MeterIds;
    }

    /// <summary>
    /// Per-project Core context (meter names + lookup text). Built once per
    /// project and invalidated by <see cref="InitializeEditor"/>.
    /// </summary>
    private SkillsContext ContextForProject()
    {
        if (_skillsContext is not null) return _skillsContext;

        try
        {
            _skillsContext = SkillsContext.Load(ProjectPaths.BecFolder(_projectFolder));
        }
        catch (Exception)
        {
            // Unreadable project data: fall back to an empty context so the
            // editor still works (dropdowns degrade to free-text).
            _skillsContext = SkillsContext.Empty;
        }

        return _skillsContext;
    }

    /// <summary>
    /// Resolves the set of valid animation names for the skill currently in
    /// the SKILL Box by walking: SKILLUSECLASS → classdefs MESH → *.pan files.
    /// Returns a sorted, intersected list (names valid across ALL referenced meshes).
    /// </summary>
    private List<string> ResolveAnimationNames(out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrEmpty(_projectFolder))
        {
            errorMessage = "No project loaded — animation names unavailable.";
            return new List<string>();
        }

        var classNames = ParseSkillBox(_txtSkillBox.Text)
            .Where(f => f.FieldName.Equals("SKILLUSECLASS", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.RawValue.Trim('"', ' '))
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        if (classNames.Count == 0)
        {
            errorMessage = "Add SKILLUSECLASS lines first; animation names are resolved from the associated character meshes.";
            return new List<string>();
        }

        // Class → mesh → *.pan resolution lives in Core so the MCP server resolves the
        // same names the editor shows. Core APIs take the BEC folder, not the outer
        // project root — every other call in this view wraps the path the same way.
        var result = SkillAnimationResolver.Resolve(
            ProjectPaths.BecFolder(_projectFolder), classNames);
        if (result.Error is not null) errorMessage = result.Error;
        return result.AnimationNames.ToList();
    }

    /// <summary>Returns the first double-quoted substring in <paramref name="line"/>,
    /// or <c>null</c> if none is found.</summary>
    private static string? ExtractFirstQuoted(string line)
    {
        int start = line.IndexOf('"');
        if (start < 0) return null;
        int end = line.IndexOf('"', start + 1);
        if (end < 0) return null;
        return line.Substring(start + 1, end - start - 1);
    }

    private void SetStatus(string msg, bool isError = false)
    {
        _lblStatus.Text      = msg;
        _lblStatus.ForeColor = isError ? Color.Red : Color.DarkGreen;
    }
}
