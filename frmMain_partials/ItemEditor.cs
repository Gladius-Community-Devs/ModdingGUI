using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.ComponentModel; // Required for CurrencyManager

namespace ModdingGUI
{
    //==================================================
    // ParsedItemFile Class (Helper for ItemParser)
    //==================================================
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

    //==================================================
    // Item data model
    //==================================================
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

            if (Cost > 0 || Type == "Gold") sb.AppendLine($"\tITEMCOST: {Cost}"); // Assuming gold might have 0 cost but still show line
            if (MinLevel > 0) sb.AppendLine($"\tITEMMINLEVEL: {MinLevel}");

            foreach (var attr in Attributes)
            {
                if (!string.IsNullOrEmpty(attr))
                {
                    sb.AppendLine($"\tITEMATTRIBUTE: \"{attr}\"");
                }
            }
            if (!string.IsNullOrEmpty(Mesh)) sb.AppendLine($"\tITEMMESH: \"{Mesh}\"");
            if (!string.IsNullOrEmpty(Mesh2)) sb.AppendLine($"\tITEMMESH2: \"{Mesh2}\"");
            if (!string.IsNullOrEmpty(Material)) sb.AppendLine($"\tITEMMATERIAL: \"{Material}\"");

            foreach (var hs in HideSets) sb.AppendLine($"\tITEMHIDESET: \"{hs}\"");
            foreach (var ss in ShowSets) sb.AppendLine($"\tITEMSHOWSET: \"{ss}\"");

            foreach (var aff in Affinities) sb.AppendLine($"\tITEMAFFINITY: {aff.Key} , {aff.Value}");

            var skillsToAdd = Skills.Where(s => !string.IsNullOrEmpty(s)).ToList();
            foreach (var sk in skillsToAdd) sb.AppendLine($"\tITEMSKILL: \"{sk}\"");

            foreach (var sm in StatMods) sb.AppendLine($"\tITEMSTATMOD: {sm.Key}, {sm.Value}");

            string result = sb.ToString().TrimEnd('\r', '\n');
            return result;
        }
    }

    //==================================================
    // Lightweight skill DTO
    //==================================================
    public class SkillInfo
    {
        public string InternalName { get; init; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsItemSkill => InternalName.StartsWith("Item ", StringComparison.Ordinal);
        public override string ToString() => DisplayName;
    }

    //==================================================
    // Parser utility – ITEMS
    //==================================================
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
                header.Add("// Items.tok");
                header.Add($"// Saved by ModdingGUI");
                header.Add(string.Empty);
                header.Add("NUMENTRIES: 0");
                return new ParsedItemFile(header, itemsList);
            }

            var allFileLines = File.ReadAllLines(itemsTokPath).ToList();
            int firstItemLineIndex = -1;

            for (int i = 0; i < allFileLines.Count; i++)
            {
                if (allFileLines[i].TrimStart().StartsWith("ITEMCREATE:", StringComparison.Ordinal))
                {
                    firstItemLineIndex = i;
                    break;
                }
                header.Add(allFileLines[i]);
            }

            var itemContentLines = new List<string>();
            if (firstItemLineIndex != -1)
            {
                for (int i = firstItemLineIndex; i < allFileLines.Count; i++)
                {
                    itemContentLines.Add(allFileLines[i]);
                }
            }

            Item? current = null;
            var rawBlockBuilder = new StringBuilder();

            foreach (var rawLineOriginal in itemContentLines)
            {
                var line = rawLineOriginal.TrimEnd(); // Trim only trailing whitespace for processing
                if (string.IsNullOrWhiteSpace(rawLineOriginal)) // Check original line for pure whitespace to delimit items
                {
                    if (current != null)
                    {
                        current.RawText = rawBlockBuilder.ToString().TrimEnd('\r', '\n');
                        itemsList.Add(current);
                        current = null;
                        rawBlockBuilder.Clear();
                    }
                    // Add the blank line itself to the raw text if it's between items, or handle spacing differently
                    // For now, assuming item blocks are separated by one or more truly empty/whitespace lines.
                    // If rawBlockBuilder is not empty, it means the blank line is part of the previous item's block end.
                    if (rawBlockBuilder.Length > 0 && current == null) // current just got set to null
                    {
                        // This was the end of an item block, and the blank line was its terminator.
                        // The next non-blank line will start a new rawBlockBuilder or a new ITEMCREATE.
                    }
                    continue;
                }

                string trimmedLine = line.TrimStart(); // For tag checking

                if (trimmedLine.StartsWith("ITEMCREATE:", StringComparison.Ordinal))
                {
                    if (current != null) // End previous item if a new ITEMCREATE starts immediately
                    {
                        current.RawText = rawBlockBuilder.ToString().TrimEnd('\r', '\n');
                        itemsList.Add(current);
                        rawBlockBuilder.Clear();
                    }

                    var m = _itemHeaderRegex.Match(trimmedLine);
                    if (!m.Success) { logAction?.Invoke($"Invalid ITEMCREATE line: {line}", Color.Red); current = null; rawBlockBuilder.Clear(); continue; }
                    current = new Item
                    {
                        Name = m.Groups["name"].Value,
                        Type = m.Groups["type"].Value,
                        SubType = m.Groups["subtype"].Value,
                        Style = m.Groups["style"].Value,
                        StarRating = int.Parse(m.Groups["stars"].Value)
                    };
                    current.DisplayName = current.Name;
                }

                if (current != null)
                {
                    rawBlockBuilder.AppendLine(rawLineOriginal); // Append the original line to preserve formatting
                    if (!trimmedLine.StartsWith("ITEMCREATE:", StringComparison.Ordinal))
                    {
                        string tag = GetTag(trimmedLine);
                        switch (tag)
                        {
                            case "ITEMDESCRIPTIONID": current.Description = LookupHelper.ResolveLookupValue(trimmedLine, lookup, out int? descId); current.DescriptionId = descId; break;
                            case "ITEMDISPLAYNAMEID": current.DisplayName = LookupHelper.ResolveLookupValue(trimmedLine, lookup, out int? dispId, current.Name); current.DisplayNameId = dispId; break;
                            case "ITEMCOST": current.Cost = int.Parse(GetValue(trimmedLine)); break;
                            case "ITEMMINLEVEL": current.MinLevel = int.Parse(GetValue(trimmedLine)); break;
                            case "ITEMATTRIBUTE": current.Attributes.Add(ExtractText(trimmedLine)); break;
                            case "ITEMMESH": current.SetMesh(ExtractText(trimmedLine)); break;
                            case "ITEMMESH2": current.SetMesh2(ExtractText(trimmedLine)); break;
                            case "ITEMMATERIAL": current.SetMaterial(ExtractText(trimmedLine)); break;
                            case "ITEMHIDESET": current.HideSets.Add(ExtractText(trimmedLine)); break;
                            case "ITEMSHOWSET": current.ShowSets.Add(ExtractText(trimmedLine)); break;
                            case "ITEMAFFINITY": ParseAffinity(trimmedLine, current); break;
                            case "ITEMSTATMOD": ParseStatMod(trimmedLine, current); break;
                            case "ITEMSKILL": current.Skills.Add(ExtractText(trimmedLine)); break;
                        }
                    }
                }
            }
            if (current != null) // Add the last item
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
                string headerLine = headerLines[i];
                if (headerLine.TrimStart().StartsWith("NUMENTRIES:", StringComparison.OrdinalIgnoreCase))
                {
                    allOutputText.AppendLine($"NUMENTRIES: {items.Count()}");
                }
                else
                {
                    allOutputText.AppendLine(headerLine);
                }
            }

            foreach (var item in items)
            {
                allOutputText.AppendLine(item.ToTokFormatString());
                allOutputText.AppendLine(); // Ensures a blank line between item entries
            }
            // Ensure the file ends with a single newline, and not multiple blank lines from the loop.
            string output = allOutputText.ToString();
            if (output.EndsWith(Environment.NewLine + Environment.NewLine))
            {
                output = output.Substring(0, output.Length - Environment.NewLine.Length);
            }
            else if (!output.EndsWith(Environment.NewLine))
            {
                output += Environment.NewLine;
            }
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
        private static string GetTag(string l)
        {
            int colonIndex = l.IndexOf(':');
            return colonIndex > 0 ? l.Substring(0, colonIndex).Trim() : string.Empty;
        }
        private static string GetValue(string l)
        {
            var parts = l.Split(':', 2);
            return parts.Length > 1 ? parts[1].Trim() : string.Empty;
        }
        private static string ExtractText(string l)
        {
            var m = _quotedRegex.Match(l);
            return m.Success ? m.Groups["text"].Value : GetValue(l);
        }
    }

    //==================================================
    // Parser utility – SKILLS
    //==================================================
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

            foreach (var raw in File.ReadLines(skillsTokPath))
            {
                var line = raw.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (current != null) { list.Add(current); current = null; }
                    continue;
                }
                if (line.StartsWith("SKILLCREATE:", StringComparison.Ordinal))
                {
                    if (current != null) list.Add(current);
                    var m = _skillCreateRegex.Match(line);
                    if (!m.Success) { logAction?.Invoke($"Invalid SKILLCREATE line: {line}", Color.Orange); current = null; continue; }
                    current = new SkillInfo { InternalName = m.Groups["name"].Value };
                    current.DisplayName = current.InternalName;
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

    //==================================================
    // Shared lookup utilities
    //==================================================
    internal static class LookupHelper
    {
        public static Dictionary<int, string> LoadLookupTable(string path)
        {
            var d = new Dictionary<int, string>();
            if (!File.Exists(path)) return d;
            foreach (var ln in File.ReadLines(path))
            {
                var parts = ln.Split('^', 2);
                if (parts.Length < 2 || !int.TryParse(parts[0], out var id)) continue;
                d[id] = parts[1].Split('^').FirstOrDefault()?.Trim() ?? string.Empty;
            }
            return d;
        }

        public static string ResolveLookupValue(string line, Dictionary<int, string> lookup, out int? foundId, string fallback = "")
        {
            foundId = null;
            var valuePart = line.Split(new[] { ':' }, 2);
            if (valuePart.Length < 2) { return fallback; }

            var valStr = valuePart[1].Trim();
            if (int.TryParse(valStr, out var id))
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
                    if (parts.Length > 2)
                    {
                        newParts.AddRange(parts.Skip(2));
                    }
                    lines[i] = string.Join("^", newParts);
                    updated = true;
                    break;
                }
            }
            if (updated) File.WriteAllLines(lookupTextPath, lines);
            return updated;
        }
    }

    //==================================================
    // WinForms presentation (partial of frmMain)
    //==================================================
    public partial class frmMain : Form
    {
        private List<Item> _items = new List<Item>(); // Stores items in original file order
        private BindingList<Item> _bindingListOfItems; // For UI display, sorted by DisplayName
        private List<SkillInfo> _skills = new List<SkillInfo>();
        private List<SkillInfo> _itemOnlySkills = new List<SkillInfo>();
        private List<string> _currentItemFileHeaderLines = new List<string>();

        private readonly List<string> _affinityTypes =
            new List<string> { "NONE", "fire", "earth", "water", "air", "dark", "light" };

        private readonly List<string> _validStatModTypes = new List<string> { "initiative", "defense", "accuracy" };
        private const int MaxStatModRows = 3;

        private class StatModUIRow
        {
            public ComboBox StatTypeComboBox { get; set; }
            public TextBox StatValueTextBox { get; set; }
        }
        private List<StatModUIRow> _dynamicStatModControls = new List<StatModUIRow>();

        private string _itemsTokPath = string.Empty;
        private string _lookupTextPath = string.Empty;
        private Dictionary<int, string> _lookupTable = new Dictionary<int, string>();
        private string _currentProjectPath = string.Empty;

        public void InitialiseItemEditor(string projectPath)
        {
            _currentProjectPath = projectPath;
            AppendLog("Initializing Item Editor...", Color.DarkGreen, rtbItemEditor);
            LoadDataForEditor(projectPath); // _items will be in original file order

            var sortedForDisplay = _items.OrderBy(i => i.DisplayName).ToList();
            _bindingListOfItems = new BindingList<Item>(sortedForDisplay); // Use sorted list for UI

            BindItemControls(); // Binds to _bindingListOfItems
            BindAffinityDropdown();

            if (flpStatMods != null)
            {
                ClearAndRepopulateStatModControls(null);
            }
            else
            {
                AppendLog("FlowLayoutPanel 'flpStatMods' NOT FOUND. StatMod UI will not function.", Color.Red, rtbItemEditor);
            }

            this.btnItemEditorMoreStats.Click -= btnItemEditorMoreStats_Click;
            this.btnItemEditorMoreStats.Click += btnItemEditorMoreStats_Click;

            this.btnItemEditorNewSave.Click -= btnItemEditorNewSave_Click;
            this.btnItemEditorNewSave.Click += btnItemEditorNewSave_Click;

            if (ddlItemEditorAllItems.Items.Count > 0)
            {
                ddlItemEditorAllItems.SelectedIndex = 0;
            }
            else
            {
                AppendLog("Initialized. No items loaded.", Color.Orange, rtbItemEditor);
                ClearEditorFields();
            }
        }

        private void LoadDataForEditor(string projectPath)
        {
            AppendLog($"LoadDataForEditor: {projectPath}", Color.Blue, rtbItemEditor);

            projectPath = projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var becFolder = Path.Combine(projectPath, $"{Path.GetFileName(projectPath)}_BEC");
            var cfg = Path.Combine(becFolder, "data", "config");
            _itemsTokPath = Path.Combine(cfg, "items.tok");
            _lookupTextPath = Path.Combine(cfg, "lookuptext_eng.txt");
            string skillsTokPath = Path.Combine(cfg, "skills.tok");

            _lookupTable = LookupHelper.LoadLookupTable(_lookupTextPath);
            AppendLog($"Lookup table: {_lookupTable.Count} entries.", Color.Blue, rtbItemEditor);

            Action<string, Color?> logActionForParsers = (msg, col) => AppendLog(msg, col, rtbItemEditor);

            ParsedItemFile parsedFile = ItemParser.ParseItemsTok(_itemsTokPath, _lookupTextPath, logActionForParsers);
            _items = parsedFile.Items.ToList(); // Keep original file order
            _currentItemFileHeaderLines = parsedFile.HeaderLines;

            _skills = SkillParser.ParseSkillsTok(skillsTokPath, _lookupTextPath, logActionForParsers).ToList();
            AppendLog($"Total skills parsed: {_skills.Count}", null, rtbItemEditor);

            _itemOnlySkills = _skills.Where(s => s.IsItemSkill).OrderBy(s => s.DisplayName).ToList();
            AppendLog($"Item-only skills: {_itemOnlySkills.Count}", null, rtbItemEditor);
        }

        private void ClearEditorFields()
        {
            AppendLog("ClearEditorFields called.", Color.DarkGray, rtbItemEditor);
            txtItemEditorOriginalName.Text = string.Empty;
            txtItemEditorOriginalDescription.Text = string.Empty;
            txtItemEditorOriginalDetails.Text = string.Empty;

            txtItemEditorNewName.Text = string.Empty; txtItemEditorNewName.Enabled = false;
            txtItemEditorNewDescription.Text = string.Empty; txtItemEditorNewDescription.Enabled = false;
            txtItemEditorNewCost.Text = string.Empty;
            txtItemEditorNewLevel.Text = string.Empty;

            txtItemEditorNewMesh.Text = string.Empty; txtItemEditorNewMesh.ReadOnly = true;
            txtItemEditorNewMesh2.Text = string.Empty; txtItemEditorNewMesh2.ReadOnly = true;
            txtItemEditorNewMaterial.Text = string.Empty; txtItemEditorNewMaterial.ReadOnly = true;

            var noneSkillList = new List<SkillInfo> { new SkillInfo { InternalName = string.Empty, DisplayName = "NONE" } };
            ddlItemEditorNewSkill1.DataSource = null; ddlItemEditorNewSkill1.DisplayMember = nameof(SkillInfo.DisplayName); ddlItemEditorNewSkill1.ValueMember = nameof(SkillInfo.InternalName); ddlItemEditorNewSkill1.DataSource = new List<SkillInfo>(noneSkillList); if (ddlItemEditorNewSkill1.Items.Count > 0) ddlItemEditorNewSkill1.SelectedIndex = 0;
            ddlItemEditorNewSkill2.DataSource = null; ddlItemEditorNewSkill2.DisplayMember = nameof(SkillInfo.DisplayName); ddlItemEditorNewSkill2.ValueMember = nameof(SkillInfo.InternalName); ddlItemEditorNewSkill2.DataSource = new List<SkillInfo>(noneSkillList); if (ddlItemEditorNewSkill2.Items.Count > 0) ddlItemEditorNewSkill2.SelectedIndex = 0;

            if (ddlItemEditorNewAffinity != null && ddlItemEditorNewAffinity.Items.Count > 0) { ddlItemEditorNewAffinity.SelectedIndex = 0; }
            if (txtItemEditorNewAffinity != null) { txtItemEditorNewAffinity.Text = string.Empty; txtItemEditorNewAffinity.Enabled = false; }

            ClearAndRepopulateStatModControls(null);
        }

        private void BindItemControls()
        {
            AppendLog("BindItemControls.", Color.Blue, rtbItemEditor);
            // _bindingListOfItems is already sorted for display
            ddlItemEditorAllItems.DataSource = null;
            ddlItemEditorAllItems.DisplayMember = nameof(Item.DisplayName);
            ddlItemEditorAllItems.ValueMember = nameof(Item.Name); // Still use Name for value to find in original _items list if needed
            ddlItemEditorAllItems.DataSource = _bindingListOfItems;
            AppendLog($"ddlItemEditorAllItems DataSource set ({_bindingListOfItems?.Count ?? 0} items).", Color.Blue, rtbItemEditor);

            this.ddlItemEditorAllItems.SelectedIndexChanged -= ddlItemEditorAllItems_SelectedIndexChanged;
            this.ddlItemEditorAllItems.SelectedIndexChanged += ddlItemEditorAllItems_SelectedIndexChanged;
            AppendLog("ddlItemEditorAllItems_SelectedIndexChanged attached.", Color.Green, rtbItemEditor);
        }

        private void BindAffinityDropdown()
        {
            AppendLog("BindAffinityDropdown.", Color.Blue, rtbItemEditor);
            if (ddlItemEditorNewAffinity != null)
            {
                ddlItemEditorNewAffinity.DataSource = new List<string>(_affinityTypes);
                this.ddlItemEditorNewAffinity.SelectedIndexChanged -= ddlItemEditorNewAffinity_SelectedIndexChanged;
                this.ddlItemEditorNewAffinity.SelectedIndexChanged += ddlItemEditorNewAffinity_SelectedIndexChanged;
                if (ddlItemEditorNewAffinity.Items.Count > 0) ddlItemEditorNewAffinity.SelectedIndex = 0;
                if (txtItemEditorNewAffinity != null) { txtItemEditorNewAffinity.Text = string.Empty; txtItemEditorNewAffinity.Enabled = false; }
            }
        }

        private void ddlItemEditorNewAffinity_SelectedIndexChanged(object? sender, EventArgs e)
        {
            Item? currentItem = ddlItemEditorAllItems.SelectedItem as Item; // This item is from the sorted _bindingListOfItems
            if (ddlItemEditorNewAffinity == null || txtItemEditorNewAffinity == null) return;

            string selAffType = ddlItemEditorNewAffinity.SelectedItem as string ?? "NONE";

            txtItemEditorNewAffinity.Enabled = selAffType != "NONE" && currentItem != null;
            if (currentItem != null && selAffType != "NONE")
            {
                if (currentItem.Affinities.TryGetValue(selAffType, out int val))
                {
                    txtItemEditorNewAffinity.Text = val.ToString();
                }
                else
                {
                    txtItemEditorNewAffinity.Text = string.Empty;
                }
            }
            else
            {
                txtItemEditorNewAffinity.Text = string.Empty;
            }
        }

        private void AddStatModRow(string statName = "", int statValue = 0)
        {
            if (flpStatMods == null)
            {
                AppendLog("flpStatMods is null! Cannot add StatMod row.", Color.Red, rtbItemEditor);
                return;
            }
            if (_dynamicStatModControls.Count >= MaxStatModRows)
            {
                if (btnItemEditorMoreStats != null) btnItemEditorMoreStats.Enabled = false;
                return;
            }

            Panel mainRowPanel = new Panel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 3) };
            FlowLayoutPanel horizontalFlowPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false };

            TextBox txtStatVal = new TextBox { Width = 60, Text = statValue.ToString(), Margin = new Padding(3) };
            ComboBox cmbStat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Margin = new Padding(3) };

            string noneOption = "";
            cmbStat.Items.Add(noneOption);
            cmbStat.Items.AddRange(_validStatModTypes.ToArray());
            cmbStat.SelectedItem = cmbStat.Items.OfType<string>().FirstOrDefault(item => item.Equals(statName, StringComparison.OrdinalIgnoreCase)) ?? noneOption;
            cmbStat.Tag = cmbStat.SelectedItem;
            cmbStat.SelectionChangeCommitted += StatTypeComboBox_SelectionChangeCommitted;

            horizontalFlowPanel.Controls.Add(txtStatVal);
            horizontalFlowPanel.Controls.Add(cmbStat);
            mainRowPanel.Controls.Add(horizontalFlowPanel);

            flpStatMods.Controls.Add(mainRowPanel);
            _dynamicStatModControls.Add(new StatModUIRow { StatTypeComboBox = cmbStat, StatValueTextBox = txtStatVal });

            if (btnItemEditorMoreStats != null)
            {
                btnItemEditorMoreStats.Enabled = _dynamicStatModControls.Count < MaxStatModRows;
            }
        }

        private void StatTypeComboBox_SelectionChangeCommitted(object? sender, EventArgs e)
        {
            ComboBox currentComboBox = sender as ComboBox;
            if (currentComboBox == null) return;
            string selectedStat = currentComboBox.SelectedItem as string;

            if (string.IsNullOrEmpty(selectedStat))
            {
                currentComboBox.Tag = currentComboBox.SelectedItem;
                return;
            }

            foreach (var rowUI in _dynamicStatModControls)
            {
                if (rowUI.StatTypeComboBox != currentComboBox &&
                    rowUI.StatTypeComboBox.SelectedItem is string otherStat &&
                    otherStat.Equals(selectedStat, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"The stat '{selectedStat}' is already selected. Please choose a different stat or 'NONE'.",
                                      "Duplicate Stat Modifier", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    currentComboBox.SelectedItem = currentComboBox.Tag;
                    return;
                }
            }
            currentComboBox.Tag = currentComboBox.SelectedItem;
        }

        private void ClearAndRepopulateStatModControls(Item? itm)
        {
            if (flpStatMods == null) { AppendLog("flpStatMods is null during ClearAndRepopulate!", Color.Red, rtbItemEditor); return; }

            foreach (var uiRow in _dynamicStatModControls)
            {
                if (uiRow.StatTypeComboBox != null)
                {
                    uiRow.StatTypeComboBox.SelectionChangeCommitted -= StatTypeComboBox_SelectionChangeCommitted;
                    uiRow.StatTypeComboBox.Dispose();
                }
                uiRow.StatValueTextBox?.Dispose();
            }
            _dynamicStatModControls.Clear();

            for (int i = flpStatMods.Controls.Count - 1; i >= 0; i--)
            {
                flpStatMods.Controls[i].Dispose();
            }

            if (btnItemEditorMoreStats != null) btnItemEditorMoreStats.Enabled = true;

            if (itm != null && itm.StatMods.Any())
            {
                foreach (var statModPair in itm.StatMods.Take(MaxStatModRows))
                {
                    AddStatModRow(statModPair.Key, statModPair.Value);
                }
                if (itm.StatMods.Count > MaxStatModRows)
                {
                    AppendLog($"Item '{itm.Name}' has {itm.StatMods.Count} stat mods. Displaying only the first {MaxStatModRows}.", Color.Orange, rtbItemEditor);
                }
            }

            if (_dynamicStatModControls.Count == 0 && MaxStatModRows > 0)
            {
                AddStatModRow();
            }

            if (btnItemEditorMoreStats != null)
            {
                btnItemEditorMoreStats.Enabled = _dynamicStatModControls.Count < MaxStatModRows;
            }
            flpStatMods.PerformLayout();
        }

        private void btnItemEditorMoreStats_Click(object? sender, EventArgs e)
        {
            AppendLog("Add More Stats button clicked.", Color.Teal, rtbItemEditor);
            if (_dynamicStatModControls.Count < MaxStatModRows)
            {
                AddStatModRow();
            }
            else
            {
                AppendLog("Cannot add more stat mods. Maximum reached.", Color.Orange, rtbItemEditor);
            }
            if (btnItemEditorMoreStats != null) btnItemEditorMoreStats.Enabled = _dynamicStatModControls.Count < MaxStatModRows;
            if (flpStatMods != null) { flpStatMods.PerformLayout(); }
        }

        private void ddlItemEditorAllItems_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (ddlItemEditorAllItems.SelectedItem is not Item itm) // itm is from the sorted _bindingListOfItems
            { ClearEditorFields(); return; }

            AppendLog($"Populating fields for: '{itm.DisplayName}' (Internal: {itm.Name})", Color.Green, rtbItemEditor);
            try
            {
                txtItemEditorOriginalName.Text = $"{itm.Name} / {itm.DisplayName}";
                txtItemEditorOriginalDescription.Text = itm.Description;
                txtItemEditorOriginalDetails.Text = itm.RawText; // RawText is from the original load

                txtItemEditorNewName.Text = itm.DisplayName;
                txtItemEditorNewName.Enabled = itm.DisplayNameId.HasValue;
                txtItemEditorNewDescription.Text = itm.Description;
                txtItemEditorNewDescription.Enabled = itm.DescriptionId.HasValue;

                txtItemEditorNewCost.Text = itm.Cost.ToString();
                txtItemEditorNewLevel.Text = itm.MinLevel.ToString();

                txtItemEditorNewMesh.Text = itm.Mesh ?? string.Empty;
                txtItemEditorNewMesh.ReadOnly = true;
                txtItemEditorNewMesh2.Text = itm.Mesh2 ?? string.Empty;
                txtItemEditorNewMesh2.ReadOnly = true;
                txtItemEditorNewMaterial.Text = itm.Material ?? string.Empty;
                txtItemEditorNewMaterial.ReadOnly = true;

                UpdateSkillDropDowns(itm);

                if (ddlItemEditorNewAffinity != null && ddlItemEditorNewAffinity.Items.Count > 0)
                {
                    if (itm.Affinities.Any())
                    {
                        var firstAffinity = itm.Affinities.First();
                        string matchedAffinityType = _affinityTypes.FirstOrDefault(at => at.Equals(firstAffinity.Key, StringComparison.OrdinalIgnoreCase));
                        ddlItemEditorNewAffinity.SelectedItem = matchedAffinityType ?? _affinityTypes.First(a => a == "NONE");
                    }
                    else
                    {
                        ddlItemEditorNewAffinity.SelectedItem = _affinityTypes.First(a => a == "NONE");
                    }
                    ddlItemEditorNewAffinity_SelectedIndexChanged(ddlItemEditorNewAffinity, EventArgs.Empty);
                }
                ClearAndRepopulateStatModControls(itm);
            }
            catch (Exception ex) { AppendLog($"EXCEPTION during field population: {ex.Message} {ex.StackTrace}", Color.Red, rtbItemEditor); }
        }

        private void UpdateSkillDropDowns(Item itm)
        {
            var noneOpt = new SkillInfo { InternalName = string.Empty, DisplayName = "NONE" };
            List<SkillInfo> s1DataSource = new List<SkillInfo> { noneOpt }; s1DataSource.AddRange(_itemOnlySkills);
            List<SkillInfo> s2DataSource = new List<SkillInfo> { noneOpt }; s2DataSource.AddRange(_itemOnlySkills);

            ddlItemEditorNewSkill1.DataSource = null;
            ddlItemEditorNewSkill1.DisplayMember = nameof(SkillInfo.DisplayName);
            ddlItemEditorNewSkill1.ValueMember = nameof(SkillInfo.InternalName);
            ddlItemEditorNewSkill1.DataSource = s1DataSource;
            string fSkill = itm.Skills.ElementAtOrDefault(0) ?? string.Empty;
            ddlItemEditorNewSkill1.SelectedValue = s1DataSource.FirstOrDefault(s => s.InternalName.Equals(fSkill, StringComparison.OrdinalIgnoreCase))?.InternalName ?? string.Empty;

            ddlItemEditorNewSkill2.DataSource = null;
            ddlItemEditorNewSkill2.DisplayMember = nameof(SkillInfo.DisplayName);
            ddlItemEditorNewSkill2.ValueMember = nameof(SkillInfo.InternalName);
            ddlItemEditorNewSkill2.DataSource = s2DataSource;
            string secSkill = itm.Skills.ElementAtOrDefault(1) ?? string.Empty;
            ddlItemEditorNewSkill2.SelectedValue = s2DataSource.FirstOrDefault(s => s.InternalName.Equals(secSkill, StringComparison.OrdinalIgnoreCase))?.InternalName ?? string.Empty;
        }

        private void btnItemEditorNewSave_Click(object? sender, EventArgs e)
        {
            // currentItem is from the (sorted) _bindingListOfItems
            if (ddlItemEditorAllItems.SelectedItem is not Item currentItemFromDropDown)
            { MessageBox.Show("No item selected to save.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            // Find the actual item in the original-order _items list to modify it
            Item currentItemToModify = _items.FirstOrDefault(i => i.Name == currentItemFromDropDown.Name);
            if (currentItemToModify == null)
            {
                MessageBox.Show("Selected item not found in the master list. Cannot save.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            AppendLog($"Saving item: {currentItemToModify.DisplayName} (Internal: {currentItemToModify.Name})", Color.DarkBlue, rtbItemEditor);
            bool itemChanged = false;
            bool lookupChanged = false;

            // 1. StatMods
            Dictionary<string, int> collectedStatMods = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var rowUI in _dynamicStatModControls)
            {
                if (rowUI.StatTypeComboBox.SelectedItem is string statName && !string.IsNullOrWhiteSpace(statName))
                {
                    if (!int.TryParse(rowUI.StatValueTextBox.Text, out int statValue))
                    { MessageBox.Show($"Invalid value for stat '{statName}': '{rowUI.StatValueTextBox.Text}'. Please correct.", "Invalid Stat Value", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                    if (collectedStatMods.ContainsKey(statName))
                    { MessageBox.Show($"Duplicate stat type '{statName}' detected. Please correct.", "Duplicate Stat Type", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                    collectedStatMods[statName] = statValue;
                }
            }
            if (currentItemToModify.StatMods.Count != collectedStatMods.Count || currentItemToModify.StatMods.Any(kvp => !collectedStatMods.TryGetValue(kvp.Key, out int newVal) || newVal != kvp.Value))
            { currentItemToModify.StatMods.Clear(); foreach (var kvp in collectedStatMods) { currentItemToModify.StatMods.Add(kvp.Key, kvp.Value); } itemChanged = true; AppendLog("StatMods updated.", Color.Green, rtbItemEditor); }

            // 2. Affinity
            if (ddlItemEditorNewAffinity != null && ddlItemEditorNewAffinity.SelectedItem is string selAffType)
            {
                bool affChangedThisTime = false;
                if (selAffType != "NONE")
                {
                    if (txtItemEditorNewAffinity != null && int.TryParse(txtItemEditorNewAffinity.Text, out int parsedAffVal))
                    {
                        if (currentItemToModify.Affinities.Count != 1 || !currentItemToModify.Affinities.ContainsKey(selAffType) || currentItemToModify.Affinities[selAffType] != parsedAffVal)
                        { currentItemToModify.Affinities.Clear(); currentItemToModify.Affinities[selAffType] = parsedAffVal; affChangedThisTime = true; }
                    }
                    else { MessageBox.Show($"Invalid affinity value for '{selAffType}'. Affinity changes not saved.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                }
                else
                {
                    if (currentItemToModify.Affinities.Any()) { currentItemToModify.Affinities.Clear(); affChangedThisTime = true; }
                }
                if (affChangedThisTime) { itemChanged = true; AppendLog("Affinity updated.", Color.Green, rtbItemEditor); }
            }

            // 3. Description (Lookup Text)
            if (currentItemToModify.DescriptionId.HasValue && txtItemEditorNewDescription.Text != currentItemToModify.Description)
            {
                if (LookupHelper.UpdateLookupEntry(_lookupTextPath, currentItemToModify.DescriptionId.Value, txtItemEditorNewDescription.Text))
                { currentItemToModify.Description = txtItemEditorNewDescription.Text; _lookupTable[currentItemToModify.DescriptionId.Value] = txtItemEditorNewDescription.Text; itemChanged = true; lookupChanged = true; AppendLog("Description (lookup) updated.", Color.Green, rtbItemEditor); }
                else { AppendLog($"Failed to update lookup text for Description ID: {currentItemToModify.DescriptionId.Value}", Color.Red, rtbItemEditor); }
            }

            // 4. DisplayName (Lookup Text)
            if (currentItemToModify.DisplayNameId.HasValue && txtItemEditorNewName.Text != currentItemToModify.DisplayName)
            {
                if (LookupHelper.UpdateLookupEntry(_lookupTextPath, currentItemToModify.DisplayNameId.Value, txtItemEditorNewName.Text))
                { currentItemToModify.DisplayName = txtItemEditorNewName.Text; _lookupTable[currentItemToModify.DisplayNameId.Value] = txtItemEditorNewName.Text; itemChanged = true; lookupChanged = true; AppendLog("Display Name (lookup) updated.", Color.Green, rtbItemEditor); }
                else { AppendLog($"Failed to update lookup text for DisplayName ID: {currentItemToModify.DisplayNameId.Value}", Color.Red, rtbItemEditor); }
            }

            // 5. Cost
            if (int.TryParse(txtItemEditorNewCost.Text, out int costVal))
            { if (currentItemToModify.Cost != costVal) { currentItemToModify.Cost = costVal; itemChanged = true; AppendLog("Cost updated.", Color.Green, rtbItemEditor); } }
            else { MessageBox.Show("Invalid Cost value. Not saved.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            // 6. MinLevel
            if (int.TryParse(txtItemEditorNewLevel.Text, out int lvlVal))
            { if (currentItemToModify.MinLevel != lvlVal) { currentItemToModify.MinLevel = lvlVal; itemChanged = true; AppendLog("MinLevel updated.", Color.Green, rtbItemEditor); } }
            else { MessageBox.Show("Invalid MinLevel value. Not saved.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            // 7. Skills
            var skillsFromUI = new List<string>();
            string? sk1_val = ddlItemEditorNewSkill1.SelectedValue as string;
            string? sk2_val = ddlItemEditorNewSkill2.SelectedValue as string;

            if (!string.IsNullOrEmpty(sk1_val)) skillsFromUI.Add(sk1_val);
            if (!string.IsNullOrEmpty(sk2_val) && (string.IsNullOrEmpty(sk1_val) || sk1_val != sk2_val))
            {
                if (!skillsFromUI.Contains(sk2_val)) skillsFromUI.Add(sk2_val);
            }

            if (!currentItemToModify.Skills.SequenceEqual(skillsFromUI))
            {
                currentItemToModify.Skills.Clear();
                currentItemToModify.Skills.AddRange(skillsFromUI);
                itemChanged = true; AppendLog("Skills updated.", Color.Green, rtbItemEditor);
            }

            if (itemChanged) // Item properties changed (not just lookup text directly on the item object)
            {
                try
                {
                    currentItemToModify.RawText = currentItemToModify.ToTokFormatString(); // Update RawText of the item in _items
                    // _items list already contains currentItemToModify with its updated properties
                    ItemParser.WriteAllItemsToTok(_itemsTokPath, _currentItemFileHeaderLines, _items); // Write _items (original order)
                    txtItemEditorOriginalDetails.Text = currentItemToModify.RawText; // Update UI for currently viewed item
                    AppendLog("Item changes written to .tok file.", Color.DarkGreen, rtbItemEditor);
                }
                catch (Exception ex) { MessageBox.Show($"Error writing items.tok: {ex.Message}", "File Write Error", MessageBoxButtons.OK, MessageBoxIcon.Error); AppendLog($"ERROR writing items.tok: {ex.Message}", Color.Red, rtbItemEditor); return; }
            }

            if (itemChanged || lookupChanged)
            {
                MessageBox.Show("Item saved successfully!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                string originalInternalName = currentItemToModify.Name;

                AppendLog("Re-initializing item editor after save...", Color.DarkMagenta, rtbItemEditor);
                LoadDataForEditor(_currentProjectPath); // Reloads _items in original file order

                var sortedForDisplayAfterSave = _items.OrderBy(i => i.DisplayName).ToList();
                _bindingListOfItems = new BindingList<Item>(sortedForDisplayAfterSave);

                this.ddlItemEditorAllItems.SelectedIndexChanged -= ddlItemEditorAllItems_SelectedIndexChanged;
                ddlItemEditorAllItems.DataSource = null;
                ddlItemEditorAllItems.DisplayMember = nameof(Item.DisplayName);
                ddlItemEditorAllItems.ValueMember = nameof(Item.Name);
                ddlItemEditorAllItems.DataSource = _bindingListOfItems;

                var reSelectedItem = _bindingListOfItems.FirstOrDefault(i => i.Name == originalInternalName);
                if (reSelectedItem != null)
                {
                    ddlItemEditorAllItems.SelectedItem = reSelectedItem;
                }
                else if (_bindingListOfItems.Any())
                {
                    ddlItemEditorAllItems.SelectedIndex = 0;
                }

                this.ddlItemEditorAllItems.SelectedIndexChanged += ddlItemEditorAllItems_SelectedIndexChanged;

                if (ddlItemEditorAllItems.SelectedItem != null)
                {
                    ddlItemEditorAllItems_SelectedIndexChanged(ddlItemEditorAllItems, EventArgs.Empty);
                }
                else
                {
                    ClearEditorFields();
                    BindAffinityDropdown();
                    ClearAndRepopulateStatModControls(null);
                }
                AppendLog("Item editor re-initialized.", Color.DarkMagenta, rtbItemEditor);
            }
            else
            {
                MessageBox.Show("No changes were detected to save.", "No Changes", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}