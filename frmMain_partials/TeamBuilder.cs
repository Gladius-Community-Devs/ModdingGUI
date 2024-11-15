using System.Text.RegularExpressions;

namespace ModdingGUI
{
    /// <summary>
    /// Partial class containing all Team Builder related functionalities.
    /// </summary>
    public partial class frmMain
    {
        #region Data Models
        private List<ClassDefinition> classDefinitions = new List<ClassDefinition>();
        /// <summary>
        /// Represents a unit class with its properties.
        /// </summary>
        public class ClassDefinition
        {
            public string ClassName { get; set; }
            public List<string> Attributes { get; set; } = new List<string>();
            public string Affinity { get; set; } // Optional

            // Future Properties: Description, Mesh, etc.
        }

        #endregion

        #region Class Parser

        /// <summary>
        /// Parses the classdefs.tok file to extract class definitions.
        /// </summary>
        public class ClassParser
        {
            /// <summary>
            /// Parses the classdefs.tok file and returns a list of ClassDefinition objects.
            /// </summary>
            /// <param name="filePath">Path to the classdefs.tok file.</param>
            /// <returns>List of ClassDefinition objects.</returns>
            public List<ClassDefinition> ParseClassDefs(string filePath)
            {
                var classDefinitions = new List<ClassDefinition>();

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"The file '{filePath}' does not exist.");
                }

                var lines = File.ReadAllLines(filePath);
                ClassDefinition currentClass = null;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    // Check for the start of a new class definition
                    if (trimmedLine.StartsWith("CREATECLASS:", StringComparison.OrdinalIgnoreCase))
                    {
                        // If there's an existing class being parsed, add it to the list
                        if (currentClass != null)
                        {
                            classDefinitions.Add(currentClass);
                        }

                        // Initialize a new ClassDefinition object
                        currentClass = new ClassDefinition();

                        // Extract the class name
                        var match = Regex.Match(trimmedLine, @"CREATECLASS:\s*(\w+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            currentClass.ClassName = match.Groups[1].Value.Trim();
                        }
                        else
                        {
                            // If class name is not found, skip this class definition
                            currentClass = null;
                        }

                        continue;
                    }

                    // If currently parsing a class, extract relevant fields
                    if (currentClass != null)
                    {
                        // Extract ATTRIBUTE lines
                        if (trimmedLine.StartsWith("ATTRIBUTE:", StringComparison.OrdinalIgnoreCase))
                        {
                            var attrMatch = Regex.Match(trimmedLine, @"ATTRIBUTE:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                            if (attrMatch.Success)
                            {
                                currentClass.Attributes.Add(attrMatch.Groups[1].Value.Trim());
                            }
                        }

                        // Extract AFFINITY line (assuming only one per class)
                        if (trimmedLine.StartsWith("AFFINITY:", StringComparison.OrdinalIgnoreCase))
                        {
                            var affMatch = Regex.Match(trimmedLine, @"AFFINITY:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                            if (affMatch.Success)
                            {
                                currentClass.Affinity = affMatch.Groups[1].Value.Trim();
                            }
                        }

                        // Future Parsing: Description, Mesh, etc.
                    }
                }

                // After the loop, add the last parsed class if it exists
                if (currentClass != null)
                {
                    classDefinitions.Add(currentClass);
                }

                return classDefinitions;
            }
        }

        #endregion

        #region Team Builder Tab Activation

        /// <summary>
        /// Loads classes into the ddlTeamClasses ComboBox.
        /// </summary>
        private void LoadClassesIntoDropdown()
        {
            // Verify if a project is selected by checking txtPackPath
            string projectPath = txtPackPath.Text.Trim();
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                MessageBox.Show("No project selected. Please unpack or select a project first.", "Project Not Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                AppendLog("Attempted to access Team Builder without selecting a valid project.", ErrorColor, true);

                // Switch back to tabPacking
                foreach (TabPage tab in tabContainer.TabPages)
                {
                    if (tab.Name == "tabPacking")
                    {
                        tabContainer.SelectedTab = tab;
                        break;
                    }
                }

                tabContainer.TabPages.Remove(tabTeamBuilder);
                teamBuilderToolStripMenuItem.Checked = false;
                return;
            }

            // Step 1: Trim trailing directory separators
            projectPath = projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Step 2: Extract the project folder name
            string projectFolderName = Path.GetFileName(projectPath);

            // Step 3: Construct the path to classdefs.tok dynamically
            string classDefsPath = Path.Combine(projectPath, $"{projectFolderName}_BEC", "data", "config", "classdefs.tok");

            // Optional Step: Verify that classDefsPath exists
            if (!File.Exists(classDefsPath))
            {
                MessageBox.Show($"classdefs.tok not found at '{classDefsPath}'. Please ensure the project is correctly unpacked.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"classdefs.tok not found at '{classDefsPath}'.", ErrorColor, true);

                // Optionally, switch back to tabPacking
                foreach (TabPage tab in tabContainer.TabPages)
                {
                    if (tab.Name == "tabPacking")
                    {
                        tabContainer.SelectedTab = tab;
                        break;
                    }
                }

                tabContainer.TabPages.Remove(tabTeamBuilder);
                teamBuilderToolStripMenuItem.Checked = false;
                return;
            }

            // Proceed with loading class definitions using classDefsPath
            try
            {
                var parser = new ClassParser();
                classDefinitions = parser.ParseClassDefs(classDefsPath);

                ddlTeamClasses.Items.Clear();
                foreach (var classDef in classDefinitions)
                {
                    ddlTeamClasses.Items.Add(classDef.ClassName);
                }

                if (ddlTeamClasses.Items.Count > 0)
                {
                    ddlTeamClasses.SelectedIndex = 0; // Select the first class by default
                }
                else
                {
                    MessageBox.Show("No classes found in the selected project.", "No Classes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    AppendLog("No classes found in classdefs.tok.", InfoColor, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading classes: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"Error loading classes: {ex.Message}", ErrorColor, true);

                // Optionally, switch back to tabPacking
                foreach (TabPage tab in tabContainer.TabPages)
                {
                    if (tab.Name == "tabPacking")
                    {
                        tabContainer.SelectedTab = tab;
                        break;
                    }
                }

                tabContainer.TabPages.Remove(tabTeamBuilder);
                teamBuilderToolStripMenuItem.Checked = false;
            }

        }

        #endregion

        #region Preview Update

        /// <summary>
        /// Handles the SelectedIndexChanged event of ddlTeamClasses to update the preview.
        /// </summary>
        private void ddlTeamClasses_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        /// <summary>
        /// Handles the TextChanged event of txtTeamUnitName to update the preview.
        /// </summary>
        private void txtTeamUnitName_TextChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        /// <summary>
        /// Updates the grpPreview with the current unit's information.
        /// </summary>
        private void UpdatePreview()
        {
            string unitName = txtTeamUnitName.Text.Trim();
            string selectedClassName = ddlTeamClasses.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(selectedClassName))
            {
                // Clear the preview if no class is selected
                ClearPreview();
                return;
            }

            var selectedClass = classDefinitions.FirstOrDefault(c => c.ClassName.Equals(selectedClassName, StringComparison.OrdinalIgnoreCase));

            if (selectedClass == null)
            {
                // Clear the preview if class not found
                ClearPreview();
                return;
            }

            // Update grpPreview controls
            UpdateUnitPreview(unitName, selectedClass);
        }

        /// <summary>
        /// Clears the grpPreview controls.
        /// </summary>
        private void ClearPreview()
        {
            txtPreviewUnitName.Text = string.Empty;
            txtPreviewAttributes.Text = string.Empty;
            txtPreviewAffinity.Text = string.Empty;
        }

        /// <summary>
        /// Updates the grpPreview with the selected unit's information.
        /// </summary>
        /// <param name="unitName">Name of the unit.</param>
        /// <param name="classDef">ClassDefinition object containing class details.</param>
        private void UpdateUnitPreview(string unitName, ClassDefinition classDef)
        {
            txtPreviewUnitName.Text = string.IsNullOrEmpty(unitName) ? "N/A" : unitName;
            txtPreviewAttributes.Text = string.Join(Environment.NewLine, classDef.Attributes);
            txtPreviewAffinity.Text = string.IsNullOrEmpty(classDef.Affinity) ? "None" : classDef.Affinity;

            // Future: Update additional preview fields if added
            // txtPreviewDescription.Text = classDef.Description;
            // txtPreviewMesh.Text = classDef.Mesh;
        }

        #endregion

        #region Add Unit to Team

        /// <summary>
        /// Handles the Click event of btnTeamAddUnit to add a unit to the tvwTeam TreeView.
        /// </summary>
        private void btnTeamAddUnit_Click(object sender, EventArgs e)
        {
            string unitName = txtTeamUnitName.Text.Trim();
            string selectedClassName = ddlTeamClasses.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(unitName))
            {
                MessageBox.Show("Please enter a unit name.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(selectedClassName))
            {
                MessageBox.Show("Please select a class for the unit.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedClass = classDefinitions.FirstOrDefault(c => c.ClassName.Equals(selectedClassName, StringComparison.OrdinalIgnoreCase));

            if (selectedClass == null)
            {
                MessageBox.Show("Selected class is invalid.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog("Attempted to add a unit with an invalid class.", ErrorColor, true);
                return;
            }

            // Check if unit already exists in tvwTeam
            if (tvwTeam.Nodes.Cast<TreeNode>().Any(n => n.Text.Equals(unitName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("A unit with this name already exists.", "Duplicate Unit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                AppendLog($"Attempted to add duplicate unit '{unitName}'.", WarningColor, true);
                return;
            }

            // Create the parent node for the unit
            TreeNode unitNode = new TreeNode(unitName)
            {
                Tag = selectedClass, // Store the ClassDefinition object for future reference
                // Optionally, set an image key if using images
            };

            // Create sub-nodes
            TreeNode attributesNode = new TreeNode("Class Attributes");
            foreach (var attribute in selectedClass.Attributes)
            {
                attributesNode.Nodes.Add(new TreeNode(attribute));
            }

            TreeNode equippedItemsNode = new TreeNode("Equipped Items");
            // Future Implementation: Populate equipped items here

            TreeNode learnedSkillsNode = new TreeNode("Learned Skills");
            // Future Implementation: Populate learned skills here

            // Add sub-nodes to the unit node
            unitNode.Nodes.Add(attributesNode);
            unitNode.Nodes.Add(equippedItemsNode);
            unitNode.Nodes.Add(learnedSkillsNode);

            // Add the unit node to tvwTeam
            tvwTeam.Nodes.Add(unitNode);

            // Expand the unit node for visibility
            unitNode.Expand();

            // Optional: Clear input fields after adding
            txtTeamUnitName.Clear();
            ddlTeamClasses.SelectedIndex = 0;

            // Log the addition
            AppendLog($"Added unit '{unitName}' with class '{selectedClassName}'.", SuccessColor, true);
        }

        #endregion

        #region Context Menu for ddlTeamClasses

        #endregion
    }
}
