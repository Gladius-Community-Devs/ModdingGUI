using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ModdingGUI
{
    public class ParsedItemFile
    {
        public List<string> HeaderLines { get; }
        public IReadOnlyList<Item> Items { get; }

        public ParsedItemFile(List<string> headerLines, IReadOnlyList<Item> items)
        {
            HeaderLines = headerLines ?? new List<string>();
            Items = items ?? new List<Item>();
        }
    }

    public class Item
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string SubType { get; set; } = string.Empty;
        public string Style { get; set; } = string.Empty;
        public int StarRating { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? DisplayNameId { get; set; }
        public int? DescriptionId { get; set; }
        public int Cost { get; set; }
        public int MinLevel { get; set; }
        public List<string> Attributes { get; } = new List<string>();
        public string? Mesh { get; private set; }
        public string? Mesh2 { get; private set; }
        public string? Material { get; private set; }
        public List<string> HideSets { get; } = new();
        public List<string> ShowSets { get; } = new();
        public Dictionary<string, int> Affinities { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> StatMods { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Skills { get; } = new();
        public string RawText { get; internal set; } = string.Empty;

        internal void SetMesh(string v) => Mesh = v;
        internal void SetMesh2(string v) => Mesh2 = v;
        internal void SetMaterial(string v) => Material = v;

        public string ToTokFormatString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ITEMCREATE: \"{Name}\", \"{Type}\", \"{SubType}\", \"{Style}\", {StarRating}");

            if (DescriptionId.HasValue) sb.AppendLine($"\tITEMDESCRIPTIONID: {DescriptionId.Value}");
            if (DisplayNameId.HasValue) sb.AppendLine($"\tITEMDISPLAYNAMEID: {DisplayNameId.Value}");
            if (Cost > 0 || Type == "Gold") sb.AppendLine($"\tITEMCOST: {Cost}");
            if (MinLevel > 0) sb.AppendLine($"\tITEMMINLEVEL: {MinLevel}");

            foreach (var attr in Attributes.Where(a => !string.IsNullOrEmpty(a))) sb.AppendLine($"\tITEMATTRIBUTE: \"{attr}\"");
            if (!string.IsNullOrEmpty(Mesh)) sb.AppendLine($"\tITEMMESH: \"{Mesh}\"");
            if (!string.IsNullOrEmpty(Mesh2)) sb.AppendLine($"\tITEMMESH2: \"{Mesh2}\"");
            if (!string.IsNullOrEmpty(Material)) sb.AppendLine($"\tITEMMATERIAL: \"{Material}\"");
            foreach (var hs in HideSets) sb.AppendLine($"\tITEMHIDESET: \"{hs}\"");
            foreach (var ss in ShowSets) sb.AppendLine($"\tITEMSHOWSET: \"{ss}\"");
            foreach (var aff in Affinities) sb.AppendLine($"\tITEMAFFINITY: {aff.Key} , {aff.Value}");
            foreach (var sk in Skills.Where(s => !string.IsNullOrEmpty(s))) sb.AppendLine($"\tITEMSKILL: \"{sk}\"");
            foreach (var sm in StatMods) sb.AppendLine($"\tITEMSTATMOD: {sm.Key}, {sm.Value}");

            return sb.ToString().TrimEnd('\r', '\n');
        }
    }

    public class SkillInfo
    {
        public string InternalName { get; init; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsItemSkill => InternalName.StartsWith("Item ", StringComparison.Ordinal);
        public override string ToString() => DisplayName;
    }

    public static class ItemParser
    {
        private static readonly Regex _itemHeaderRegex = new(@"ITEMCREATE:\s*""(?<name>[^""]+)""\s*,\s*""(?<type>[^""]+)""\s*,\s*""(?<subtype>[^""]*)""\s*,\s*""(?<style>[^""]*)""\s*,\s*(?<stars>\d+)", RegexOptions.Compiled);
        private static readonly Regex _quotedRegex = new("\"(?<text>[^\"]+)\"", RegexOptions.Compiled);

        public static ParsedItemFile ParseItemsTok(string itemsTokPath, string lookupTextPath, Action<string, Color?> logAction)
        {
            var header = new List<string>();
            var itemsList = new List<Item>();
            var lookup = LookupHelper.LoadLookupTable(lookupTextPath);

            if (!File.Exists(itemsTokPath))
            {
                logAction?.Invoke($"Item file not found: {itemsTokPath}. Creating default header.", Color.Red);
                header.AddRange(new[] { "// Items.tok", $"// Saved by ModdingGUI", string.Empty, "NUMENTRIES: 0" });
                return new ParsedItemFile(header, itemsList);
            }

            var allFileLines = File.ReadAllLines(itemsTokPath).ToList();
            int firstItemLineIndex = allFileLines.FindIndex(line => line.TrimStart().StartsWith("ITEMCREATE:", StringComparison.Ordinal));

            if (firstItemLineIndex == -1) header.AddRange(allFileLines);
            else header.AddRange(allFileLines.Take(firstItemLineIndex));

            var itemContentLines = (firstItemLineIndex != -1) ? allFileLines.Skip(firstItemLineIndex).ToList() : new List<string>();
            Item? current = null;
            var rawBlockBuilder = new StringBuilder();

            foreach (var rawLineOriginal in itemContentLines)
            {
                var line = rawLineOriginal.TrimEnd();
                if (string.IsNullOrWhiteSpace(rawLineOriginal))
                {
                    if (current != null)
                    {
                        current.RawText = rawBlockBuilder.ToString().TrimEnd('\r', '\n');
                        itemsList.Add(current);
                        current = null;
                        rawBlockBuilder.Clear();
                    }
                    continue;
                }

                string trimmedLine = line.TrimStart();
                if (trimmedLine.StartsWith("ITEMCREATE:", StringComparison.Ordinal))
                {
                    if (current != null)
                    {
                        current.RawText = rawBlockBuilder.ToString().TrimEnd('\r', '\n');
                        itemsList.Add(current);
                        rawBlockBuilder.Clear();
                    }
                    var m = _itemHeaderRegex.Match(trimmedLine);
                    if (!m.Success) { logAction?.Invoke($"Invalid ITEMCREATE line: {line}", Color.Red); current = null; continue; }
                    current = new Item
                    {
                        Name = m.Groups["name"].Value,
                        Type = m.Groups["type"].Value,
                        SubType = m.Groups["subtype"].Value,
                        Style = m.Groups["style"].Value,
                        StarRating = int.Parse(m.Groups["stars"].Value)
                    };
                    current.DisplayName = current.Name; // Default DisplayName
                    current.Description = string.Empty; // Default Description
                }

                if (current == null) continue;

                rawBlockBuilder.AppendLine(rawLineOriginal);
                if (trimmedLine.StartsWith("ITEMCREATE:", StringComparison.Ordinal)) continue;

                string tag = GetTag(trimmedLine);
                string value = GetValue(trimmedLine);
                string textValue = ExtractText(trimmedLine);

                switch (tag)
                {
                    case "ITEMDESCRIPTIONID": current.Description = LookupHelper.ResolveLookupValue(trimmedLine, lookup, out int? descId); current.DescriptionId = descId; break;
                    case "ITEMDISPLAYNAMEID": current.DisplayName = LookupHelper.ResolveLookupValue(trimmedLine, lookup, out int? dispId, current.Name); current.DisplayNameId = dispId; break;
                    case "ITEMCOST": if (int.TryParse(value, out int cost)) current.Cost = cost; break;
                    case "ITEMMINLEVEL": if (int.TryParse(value, out int lvl)) current.MinLevel = lvl; break;
                    case "ITEMATTRIBUTE": current.Attributes.Add(textValue); break;
                    case "ITEMMESH": current.SetMesh(textValue); break;
                    case "ITEMMESH2": current.SetMesh2(textValue); break;
                    case "ITEMMATERIAL": current.SetMaterial(textValue); break;
                    case "ITEMHIDESET": current.HideSets.Add(textValue); break;
                    case "ITEMSHOWSET": current.ShowSets.Add(textValue); break;
                    case "ITEMAFFINITY": ParseAffinity(trimmedLine, current); break;
                    case "ITEMSTATMOD": ParseStatMod(trimmedLine, current); break;
                    case "ITEMSKILL": current.Skills.Add(textValue); break;
                }
            }
            if (current != null)
            {
                current.RawText = rawBlockBuilder.ToString().TrimEnd('\r', '\n');
                itemsList.Add(current);
            }
            logAction?.Invoke($"Item parsing complete. Header lines: {header.Count}, Items loaded: {itemsList.Count}", Color.Green);
            return new ParsedItemFile(header, itemsList);
        }

        public static void WriteAllItemsToTok(string itemsTokPath, List<string> headerLines, IEnumerable<Item> items)
        {
            var allOutputText = new StringBuilder();
            for (int i = 0; i < headerLines.Count; i++)
            {
                allOutputText.AppendLine(headerLines[i].TrimStart().StartsWith("NUMENTRIES:", StringComparison.OrdinalIgnoreCase)
                    ? $"NUMENTRIES: {items.Count()}"
                    : headerLines[i]);
            }
            foreach (var item in items)
            {
                allOutputText.AppendLine(item.ToTokFormatString());
                allOutputText.AppendLine();
            }
            string output = allOutputText.ToString().TrimEnd('\r', '\n') + Environment.NewLine;
            File.WriteAllText(itemsTokPath, output);
        }

        private static void ParseAffinity(string line, Item item)
        {
            var p = GetValue(line).Split(new[] { ',' }, 2, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
            if (p.Length == 2 && int.TryParse(p[1], out var val)) item.Affinities[p[0]] = val;
        }
        private static void ParseStatMod(string line, Item item)
        {
            var p = GetValue(line).Split(new[] { ',' }, 2, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
            if (p.Length == 2 && int.TryParse(p[1], out var val)) item.StatMods[p[0]] = val;
        }
        private static string GetTag(string l) => l.Split(':')[0].Trim();
        private static string GetValue(string l) => l.Contains(':') ? l.Substring(l.IndexOf(':') + 1).Trim() : string.Empty;
        private static string ExtractText(string l) => _quotedRegex.Match(l) is { Success: true } m ? m.Groups["text"].Value : GetValue(l);
    }

    public static class SkillParser
    {
        private static readonly Regex _skillCreateRegex = new(@"SKILLCREATE:\s*""(?<name>[^""]+)""", RegexOptions.Compiled);
        public static IReadOnlyList<SkillInfo> ParseSkillsTok(string skillsTokPath, string lookupTextPath, Action<string, Color?> logAction)
        {
            if (!File.Exists(skillsTokPath))
            {
                logAction?.Invoke($"Skills.tok file not found at: {skillsTokPath}", Color.Red);
                return new List<SkillInfo>();
            }
            var lookup = LookupHelper.LoadLookupTable(lookupTextPath);
            var list = new List<SkillInfo>();
            SkillInfo? current = null;

            foreach (var line in File.ReadLines(skillsTokPath).Select(raw => raw.TrimEnd()))
            {
                if (string.IsNullOrWhiteSpace(line)) { if (current != null) { list.Add(current); current = null; } continue; }
                if (line.StartsWith("SKILLCREATE:", StringComparison.Ordinal))
                {
                    if (current != null) list.Add(current);
                    var m = _skillCreateRegex.Match(line);
                    if (!m.Success) { logAction?.Invoke($"Invalid SKILLCREATE line: {line}", Color.Orange); current = null; continue; }
                    current = new SkillInfo { InternalName = m.Groups["name"].Value };
                    current.DisplayName = current.InternalName; // Default
                    continue;
                }
                if (current == null) continue;
                if (line.StartsWith("SKILLDISPLAYNAMEID:", StringComparison.Ordinal))
                {
                    current.DisplayName = LookupHelper.ResolveLookupValue(line, lookup, out _, current.InternalName);
                }
            }
            if (current != null) list.Add(current);
            logAction?.Invoke($"Skill Parsing complete. {list.Count} skills loaded.", Color.Green);
            return list;
        }
    }

    internal static class LookupHelper
    {
        public static Dictionary<int, string> LoadLookupTable(string path)
        {
            var d = new Dictionary<int, string>();
            if (!File.Exists(path)) return d;
            foreach (var ln in File.ReadLines(path))
            {
                var parts = ln.Split('^', 2);
                if (parts.Length == 2 && int.TryParse(parts[0], out var id))
                {
                    d[id] = parts[1].Split('^').FirstOrDefault()?.Trim() ?? string.Empty;
                }
            }
            return d;
        }

        public static string ResolveLookupValue(string line, Dictionary<int, string> lookup, out int? foundId, string fallback = "")
        {
            foundId = null;
            var valuePart = line.Split(new[] { ':' }, 2);
            if (valuePart.Length < 2) return fallback;
            if (int.TryParse(valuePart[1].Trim(), out var id))
            {
                foundId = id;
                return lookup.TryGetValue(id, out var txt) ? txt : fallback;
            }
            return fallback;
        }

        public static bool UpdateLookupEntry(string lookupTextPath, int idToUpdate, string newText)
        {
            if (!File.Exists(lookupTextPath)) return false;
            var lines = File.ReadAllLines(lookupTextPath).ToList();
            bool updated = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var parts = lines[i].Split('^');
                if (parts.Length > 0 && int.TryParse(parts[0], out var currentId) && currentId == idToUpdate)
                {
                    var newParts = new List<string> { parts[0], newText };
                    if (parts.Length > 2) newParts.AddRange(parts.Skip(2));
                    lines[i] = string.Join("^", newParts);
                    updated = true;
                    break;
                }
            }
            if (updated) File.WriteAllLines(lookupTextPath, lines);
            return updated;
        }

        public static int GetNextAvailableLookupId(Dictionary<int, string> lookupTable)
        {
            if (lookupTable == null || !lookupTable.Any()) return 1;
            return lookupTable.Keys.Max() + 1;
        }

        public static int? AddNewLookupEntry(string lookupTextPath, string newText, Dictionary<int, string> lookupTable, Action<string, Color?>? logAction = null)
        {
            if (string.IsNullOrWhiteSpace(newText))
            {
                logAction?.Invoke("Cannot add empty text to lookup.", Color.Orange);
                return null;
            }
            try
            {
                int newId = GetNextAvailableLookupId(lookupTable);
                string newLine = $"{newId}^{newText}";
                File.AppendAllText(lookupTextPath, newLine + Environment.NewLine);
                lookupTable[newId] = newText;
                logAction?.Invoke($"Added new lookup entry: ID {newId}, Text: \"{newText}\"", Color.Green);
                return newId;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error adding new lookup entry: {ex.Message}", Color.Red);
                return null;
            }
        }
    }

    public partial class frmMain : Form
    {
        private List<Item> _items = new List<Item>();
        private BindingList<Item> _bindingListOfItems = new BindingList<Item>();
        private List<SkillInfo> _skills = new List<SkillInfo>();
        private List<SkillInfo> _itemOnlySkills = new List<SkillInfo>();
        private List<string> _currentItemFileHeaderLines = new List<string>();
        private readonly List<string> _affinityTypes = new List<string> { "NONE", "fire", "earth", "water", "air", "dark", "light" };
        private readonly List<string> _validStatModTypes = new List<string> { "initiative", "defense", "accuracy" };
        private const int MaxStatModRows = 3;
        private class StatModUIRow { public ComboBox StatTypeComboBox { get; set; } = new ComboBox(); public TextBox StatValueTextBox { get; set; } = new TextBox(); }
        private List<StatModUIRow> _dynamicStatModControls = new List<StatModUIRow>();
        private string _itemsTokPath = string.Empty;
        private string _lookupTextPath = string.Empty;
        private Dictionary<int, string> _lookupTable = new Dictionary<int, string>();
        private string _currentProjectPath = string.Empty;

        private void NumericOnlyTextBox_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        }

        public void InitialiseItemEditor(string projectPath)
        {
            _currentProjectPath = projectPath;
            AppendLog("Initializing Item Editor...", Color.DarkGreen);
            LoadDataForEditor(projectPath);
            _bindingListOfItems = new BindingList<Item>(_items.OrderBy(i => i.DisplayName).ToList());
            BindItemControls();
            BindAffinityDropdown();

            txtItemEditorNewAffinity.KeyPress += NumericOnlyTextBox_KeyPress;
            txtItemEditorNewCost.KeyPress += NumericOnlyTextBox_KeyPress;
            txtItemEditorNewLevel.KeyPress += NumericOnlyTextBox_KeyPress;

            if (flpStatMods != null) ClearAndRepopulateStatModControls(null);
            else AppendLog("FlowLayoutPanel 'flpStatMods' NOT FOUND.", Color.Red);

            this.btnItemEditorMoreStats.Click -= btnItemEditorMoreStats_Click;
            this.btnItemEditorMoreStats.Click += btnItemEditorMoreStats_Click;
            this.btnItemEditorNewSave.Click -= btnItemEditorNewSave_Click;
            this.btnItemEditorNewSave.Click += btnItemEditorNewSave_Click;

            if (ddlItemEditorAllItems.Items.Count > 0) ddlItemEditorAllItems.SelectedIndex = 0;
            else { AppendLog("Initialized. No items loaded.", Color.Orange); ClearEditorFields(); }
        }

        private void LoadDataForEditor(string projectPath)
        {
            projectPath = projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var becFolder = Path.Combine(projectPath, $"{Path.GetFileName(projectPath)}_BEC");
            var cfg = Path.Combine(becFolder, "data", "config");
            _itemsTokPath = Path.Combine(cfg, "items.tok");
            _lookupTextPath = Path.Combine(cfg, "lookuptext_eng.txt");
            string skillsTokPath = Path.Combine(cfg, "skills.tok");

            _lookupTable = LookupHelper.LoadLookupTable(_lookupTextPath);
            Action<string, Color?> logAction = (msg, col) => AppendLog(msg, col);
            ParsedItemFile parsedFile = ItemParser.ParseItemsTok(_itemsTokPath, _lookupTextPath, logAction);
            _items = parsedFile.Items.ToList();
            _currentItemFileHeaderLines = parsedFile.HeaderLines;
            _skills = SkillParser.ParseSkillsTok(skillsTokPath, _lookupTextPath, logAction).ToList();
            _itemOnlySkills = _skills.Where(s => s.IsItemSkill).OrderBy(s => s.DisplayName).ToList();
        }

        private void ClearEditorFields()
        {
            txtItemEditorOriginalName.Text = txtItemEditorOriginalDescription.Text = txtItemEditorOriginalDetails.Text = string.Empty;
            txtItemEditorNewName.Text = txtItemEditorNewDescription.Text = txtItemEditorNewCost.Text = txtItemEditorNewLevel.Text = string.Empty;
            txtItemEditorNewMesh.Text = txtItemEditorNewMesh2.Text = txtItemEditorNewMaterial.Text = string.Empty;
            txtItemEditorNewName.Enabled = txtItemEditorNewDescription.Enabled = false; // Will be re-enabled in SelectedIndexChanged
            txtItemEditorNewMesh.ReadOnly = txtItemEditorNewMesh2.ReadOnly = txtItemEditorNewMaterial.ReadOnly = true;

            var noneSkillList = new List<SkillInfo> { new SkillInfo { InternalName = string.Empty, DisplayName = "NONE" } };
            ddlItemEditorNewSkill1.DataSource = new List<SkillInfo>(noneSkillList); if (ddlItemEditorNewSkill1.Items.Count > 0) ddlItemEditorNewSkill1.SelectedIndex = 0;
            ddlItemEditorNewSkill2.DataSource = new List<SkillInfo>(noneSkillList); if (ddlItemEditorNewSkill2.Items.Count > 0) ddlItemEditorNewSkill2.SelectedIndex = 0;
            if (ddlItemEditorNewAffinity.Items.Count > 0) ddlItemEditorNewAffinity.SelectedIndex = 0;
            txtItemEditorNewAffinity.Text = string.Empty; txtItemEditorNewAffinity.Enabled = false;
            ClearAndRepopulateStatModControls(null);
        }

        private void BindItemControls()
        {
            ddlItemEditorAllItems.DataSource = null;
            ddlItemEditorAllItems.DisplayMember = nameof(Item.DisplayName);
            ddlItemEditorAllItems.ValueMember = nameof(Item.Name);
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
            if (ddlItemEditorAllItems.SelectedItem is not Item currentItem) return;
            string selAffType = ddlItemEditorNewAffinity.SelectedItem as string ?? "NONE";
            txtItemEditorNewAffinity.Enabled = selAffType != "NONE";
            txtItemEditorNewAffinity.Text = (selAffType != "NONE" && currentItem.Affinities.TryGetValue(selAffType, out int val)) ? val.ToString() : string.Empty;
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

        private void ClearAndRepopulateStatModControls(Item? itm)
        {
            if (flpStatMods == null) return;
            foreach (var uiRow in _dynamicStatModControls)
            {
                uiRow.StatTypeComboBox.SelectionChangeCommitted -= StatTypeComboBox_SelectionChangeCommitted; uiRow.StatTypeComboBox.Dispose();
                uiRow.StatValueTextBox.KeyPress -= NumericOnlyTextBox_KeyPress; uiRow.StatValueTextBox.Dispose();
            }
            _dynamicStatModControls.Clear();
            flpStatMods.Controls.Clear(); // Simpler than disposing children one by one if recreating panel
            if (btnItemEditorMoreStats != null) btnItemEditorMoreStats.Enabled = true;

            if (itm?.StatMods.Any() == true)
            {
                foreach (var statModPair in itm.StatMods.Take(MaxStatModRows)) AddStatModRow(statModPair.Key, statModPair.Value);
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
            if (ddlItemEditorAllItems.SelectedItem is not Item itm) { ClearEditorFields(); return; }

            txtItemEditorOriginalName.Text = $"{itm.Name} / {itm.DisplayName}";
            txtItemEditorOriginalDescription.Text = itm.Description;
            txtItemEditorOriginalDetails.Text = itm.RawText;

            txtItemEditorNewName.Text = itm.DisplayName;
            txtItemEditorNewName.Enabled = itm.DisplayNameId.HasValue;
            txtItemEditorNewDescription.Text = itm.Description;
            txtItemEditorNewDescription.Enabled = true; // Always allow editing description

            txtItemEditorNewCost.Text = itm.Cost.ToString();
            txtItemEditorNewLevel.Text = itm.MinLevel.ToString();
            txtItemEditorNewMesh.Text = itm.Mesh ?? string.Empty;
            txtItemEditorNewMesh2.Text = itm.Mesh2 ?? string.Empty;
            txtItemEditorNewMaterial.Text = itm.Material ?? string.Empty;

            UpdateSkillDropDowns(itm);

            if (ddlItemEditorNewAffinity.Items.Count > 0)
            {
                var firstAff = itm.Affinities.FirstOrDefault();
                ddlItemEditorNewAffinity.SelectedItem = !string.IsNullOrEmpty(firstAff.Key) ? _affinityTypes.FirstOrDefault(at => at.Equals(firstAff.Key, StringComparison.OrdinalIgnoreCase)) ?? "NONE" : "NONE";
                ddlItemEditorNewAffinity_SelectedIndexChanged(ddlItemEditorNewAffinity, EventArgs.Empty);
            }
            ClearAndRepopulateStatModControls(itm);
        }

        private void UpdateSkillDropDowns(Item itm)
        {
            var noneOpt = new SkillInfo { InternalName = string.Empty, DisplayName = "NONE" };
            List<SkillInfo> skillDataSource = new List<SkillInfo> { noneOpt }; skillDataSource.AddRange(_itemOnlySkills);

            ddlItemEditorNewSkill1.DataSource = new List<SkillInfo>(skillDataSource);
            ddlItemEditorNewSkill1.DisplayMember = nameof(SkillInfo.DisplayName); ddlItemEditorNewSkill1.ValueMember = nameof(SkillInfo.InternalName);
            ddlItemEditorNewSkill1.SelectedValue = skillDataSource.FirstOrDefault(s => s.InternalName.Equals(itm.Skills.ElementAtOrDefault(0) ?? "", StringComparison.OrdinalIgnoreCase))?.InternalName ?? string.Empty;

            ddlItemEditorNewSkill2.DataSource = new List<SkillInfo>(skillDataSource);
            ddlItemEditorNewSkill2.DisplayMember = nameof(SkillInfo.DisplayName); ddlItemEditorNewSkill2.ValueMember = nameof(SkillInfo.InternalName);
            ddlItemEditorNewSkill2.SelectedValue = skillDataSource.FirstOrDefault(s => s.InternalName.Equals(itm.Skills.ElementAtOrDefault(1) ?? "", StringComparison.OrdinalIgnoreCase))?.InternalName ?? string.Empty;
        }

        private void btnItemEditorNewSave_Click(object? sender, EventArgs e)
        {
            if (ddlItemEditorAllItems.SelectedItem is not Item currentItemFromDropDown) { MessageBox.Show("No item selected."); return; }
            Item? currentItemToModify = _items.FirstOrDefault(i => i.Name == currentItemFromDropDown.Name);
            if (currentItemToModify == null) { MessageBox.Show("Selected item not found in master list."); return; }

            bool itemChanged = false;
            bool lookupChanged = false;

            // StatMods
            var collectedStatMods = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var rowUI in _dynamicStatModControls)
            {
                if (rowUI.StatTypeComboBox.SelectedItem is string statName && !string.IsNullOrWhiteSpace(statName) && int.TryParse(rowUI.StatValueTextBox.Text, out int statVal))
                    collectedStatMods[statName] = statVal;
                else if (rowUI.StatTypeComboBox.SelectedItem is string sn && !string.IsNullOrWhiteSpace(sn)) { MessageBox.Show($"Invalid value for stat '{sn}'."); return; }
            }
            if (currentItemToModify.StatMods.Count != collectedStatMods.Count || currentItemToModify.StatMods.Any(kvp => !collectedStatMods.TryGetValue(kvp.Key, out int newVal) || newVal != kvp.Value))
            { currentItemToModify.StatMods.Clear(); foreach (var kvp in collectedStatMods) currentItemToModify.StatMods.Add(kvp.Key, kvp.Value); itemChanged = true; }

            // Affinity
            if (ddlItemEditorNewAffinity.SelectedItem is string selAffType)
            {
                bool affUpdated = false;
                if (selAffType != "NONE")
                {
                    if (int.TryParse(txtItemEditorNewAffinity.Text, out int parsedAffVal))
                    {
                        if (currentItemToModify.Affinities.Count != 1 || !currentItemToModify.Affinities.ContainsKey(selAffType) || currentItemToModify.Affinities[selAffType] != parsedAffVal)
                        { currentItemToModify.Affinities.Clear(); currentItemToModify.Affinities[selAffType] = parsedAffVal; affUpdated = true; }
                    }
                    else { MessageBox.Show($"Invalid affinity value for '{selAffType}'."); }
                }
                else if (currentItemToModify.Affinities.Any()) { currentItemToModify.Affinities.Clear(); affUpdated = true; }
                if (affUpdated) itemChanged = true;
            }

            // Description
            if (!currentItemToModify.DescriptionId.HasValue && !string.IsNullOrWhiteSpace(txtItemEditorNewDescription.Text))
            {
                int? newDescId = LookupHelper.AddNewLookupEntry(_lookupTextPath, txtItemEditorNewDescription.Text, _lookupTable, (m, c) => AppendLog(m, c));
                if (newDescId.HasValue)
                {
                    currentItemToModify.DescriptionId = newDescId.Value;
                    currentItemToModify.Description = txtItemEditorNewDescription.Text;
                    itemChanged = true; lookupChanged = true;
                }
            }
            else if (currentItemToModify.DescriptionId.HasValue && txtItemEditorNewDescription.Text != currentItemToModify.Description)
            {
                if (LookupHelper.UpdateLookupEntry(_lookupTextPath, currentItemToModify.DescriptionId.Value, txtItemEditorNewDescription.Text))
                {
                    currentItemToModify.Description = txtItemEditorNewDescription.Text;
                    _lookupTable[currentItemToModify.DescriptionId.Value] = txtItemEditorNewDescription.Text;
                    itemChanged = true; lookupChanged = true;
                }
            }

            // DisplayName
            if (currentItemToModify.DisplayNameId.HasValue && txtItemEditorNewName.Text != currentItemToModify.DisplayName)
            {
                if (LookupHelper.UpdateLookupEntry(_lookupTextPath, currentItemToModify.DisplayNameId.Value, txtItemEditorNewName.Text))
                {
                    currentItemToModify.DisplayName = txtItemEditorNewName.Text;
                    _lookupTable[currentItemToModify.DisplayNameId.Value] = txtItemEditorNewName.Text;
                    itemChanged = true; lookupChanged = true;
                }
            }

            // Cost
            if (int.TryParse(txtItemEditorNewCost.Text, out int costVal)) { if (currentItemToModify.Cost != costVal) { currentItemToModify.Cost = costVal; itemChanged = true; } }
            else { MessageBox.Show("Invalid Cost value."); return; }
            // MinLevel
            if (int.TryParse(txtItemEditorNewLevel.Text, out int lvlVal)) { if (currentItemToModify.MinLevel != lvlVal) { currentItemToModify.MinLevel = lvlVal; itemChanged = true; } }
            else { MessageBox.Show("Invalid MinLevel value."); return; }

            // Skills
            var skillsFromUI = new List<string>();
            string? sk1 = null; // Initialize sk1 to null (or string.Empty)
            if (ddlItemEditorNewSkill1.SelectedValue is string selectedSk1 && !string.IsNullOrEmpty(selectedSk1))
            {
                sk1 = selectedSk1; // Assign if valid
                skillsFromUI.Add(sk1);
            }

            // Now sk1 is guaranteed to be assigned (either to the selected skill or null/empty)
            if (ddlItemEditorNewSkill2.SelectedValue is string sk2 && !string.IsNullOrEmpty(sk2))
            {
                // Ensure sk2 is different from sk1 (if sk1 was assigned) and not already added
                if ((sk1 == null || sk1 != sk2) && !skillsFromUI.Contains(sk2))
                {
                    skillsFromUI.Add(sk2);
                }
                // If sk1 was null/empty and sk2 is valid, it will be added.
                // If sk1 was valid, sk2 will be added if it's different and not already there.
            }

            if (!currentItemToModify.Skills.SequenceEqual(skillsFromUI))
            {
                currentItemToModify.Skills.Clear();
                currentItemToModify.Skills.AddRange(skillsFromUI);
                itemChanged = true;
                AppendLog("Skills updated.", Color.Green, rtbItemEditor); // Added logging
            }

            if (itemChanged)
            {
                currentItemToModify.RawText = currentItemToModify.ToTokFormatString();
                ItemParser.WriteAllItemsToTok(_itemsTokPath, _currentItemFileHeaderLines, _items);
                txtItemEditorOriginalDetails.Text = currentItemToModify.RawText;
            }

            if (itemChanged || lookupChanged)
            {
                MessageBox.Show("Item saved successfully!", "Saved");
                string originalInternalName = currentItemToModify.Name;
                LoadDataForEditor(_currentProjectPath); // Reloads _items and _lookupTable
                _bindingListOfItems = new BindingList<Item>(_items.OrderBy(i => i.DisplayName).ToList());

                ddlItemEditorAllItems.SelectedIndexChanged -= ddlItemEditorAllItems_SelectedIndexChanged;
                ddlItemEditorAllItems.DataSource = _bindingListOfItems; // Rebind

                var reSelectedItem = _bindingListOfItems.FirstOrDefault(i => i.Name == originalInternalName);
                if (reSelectedItem != null) ddlItemEditorAllItems.SelectedItem = reSelectedItem;
                else if (_bindingListOfItems.Any()) ddlItemEditorAllItems.SelectedIndex = 0;

                ddlItemEditorAllItems.SelectedIndexChanged += ddlItemEditorAllItems_SelectedIndexChanged;
                if (ddlItemEditorAllItems.SelectedItem != null) ddlItemEditorAllItems_SelectedIndexChanged(ddlItemEditorAllItems, EventArgs.Empty);
                else { ClearEditorFields(); BindAffinityDropdown(); ClearAndRepopulateStatModControls(null); }
            }
            else { MessageBox.Show("No changes detected to save.", "No Changes"); }
        }
    }
}