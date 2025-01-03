using System.Diagnostics;
using System.Security.Cryptography;

namespace ModdingGUI
{
    public partial class frmMain : Form
    {
        // Updated event handler for selecting the Patching Application ISO path
        private async void btnPatchingApplicationISOPath_Click(object sender, EventArgs e)
        {
            btnPatchingApplicationApply.Enabled = false;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "ISO files (*.iso;*.nkit.iso)|*.iso;*.nkit.iso|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtPatchingApplicationISOPath.Text = openFileDialog.FileName;
                }
            }

            string isoPath = txtPatchingApplicationISOPath.Text;

            // Check if the ISO filename contains parentheses
            string fileName = Path.GetFileName(isoPath);
            if (fileName.Contains('(') || fileName.Contains(')'))
            {
                var result = MessageBox.Show(
                    "The selected ISO file name contains parentheses. Do you want to create a copy with parentheses replaced by spaces?",
                    "Filename Issue",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        string directory = Path.GetDirectoryName(isoPath);
                        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
                        {
                            MessageBox.Show("Invalid ISO path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        // Replace parentheses with spaces in the filename
                        string newFilename = fileName.Replace("(", " ").Replace(")", "");
                        string newPath = Path.Combine(directory, newFilename);

                        // Check if the new file already exists
                        if (File.Exists(newPath))
                        {
                            var overwriteResult = MessageBox.Show(
                                $"A file named \"{newFilename}\" already exists. Do you want to overwrite it?",
                                "File Exists",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning
                            );

                            if (overwriteResult != DialogResult.Yes)
                            {
                                return;
                            }
                        }

                        // Copy the original file to the new path
                        File.Copy(isoPath, newPath, overwrite: true);

                        // Update the text box and isoPath variable to the new path
                        txtPatchingApplicationISOPath.Text = newPath;
                        isoPath = newPath;

                        MessageBox.Show(
                            $"File copied successfully to:\n{newPath}",
                            "Success",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                        AppendLog($"File copied successfully to: {newPath}", InfoColor, rtbPatchingApplicationOutput);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        MessageBox.Show($"Access denied: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        AppendLog($"Access denied: {ex.Message}", ErrorColor, rtbPatchingApplicationOutput);
                        return;
                    }
                    catch (PathTooLongException ex)
                    {
                        MessageBox.Show($"Path too long: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        AppendLog($"Path too long: {ex.Message}", ErrorColor, rtbPatchingApplicationOutput);
                        return;
                    }
                    catch (IOException ex)
                    {
                        MessageBox.Show($"I/O error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        AppendLog($"I/O error: {ex.Message}", ErrorColor, rtbPatchingApplicationOutput);
                        return;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        AppendLog($"Unexpected error: {ex.Message}", ErrorColor, rtbPatchingApplicationOutput);
                        return;
                    }
                }
                else
                {
                    // User chose not to create a copy
                    AppendLog("Please select a valid ISO file without parentheses.", ErrorColor, rtbPatchingApplicationOutput);
                    return;
                }
            }

            // Validate the entire file path for parentheses
            if (!ValidateFilePathNoParentheses(isoPath))
            {
                AppendLog("The file path contains parentheses. Please rename your folders or move your ISO somewhere else.", ErrorColor, rtbPatchingApplicationOutput);
                return;
            }

            // Proceed with MD5 verification
            AppendLog("Verifying ISO MD5 hash...", InfoColor, rtbPatchingApplicationOutput);
            string md5Hash = await ComputeMD5Async(isoPath);
            if (!string.Equals(md5Hash, "a78d6e1c9de69886ce827ad2a7507339", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("The selected ISO does not match the expected Vanilla ISO. Proceeding may cause unexpected results.", "MD5 Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                AppendLog("MD5 hash does not match the expected Vanilla ISO hash.\nProceed if you know what you are doing.", ErrorColor, rtbPatchingApplicationOutput);
            }
            else
            {
                AppendLog("Verified!", SuccessColor, rtbPatchingApplicationOutput);
            }

            btnPatchingApplicationApply.Enabled = true;
        }

        private void btnPatchingApplicationXdeltaPath_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "xDelta files (*.xdelta;*.xdelta3)|*.xdelta;*.xdelta3|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtPatchingApplicationXdeltaPath.Text = openFileDialog.FileName;
                }
            }
        }

        private async void btnPatchingApplicationApply_Click(object sender, EventArgs e)
        {
            string isoPath = txtPatchingApplicationISOPath.Text;
            string xdeltaPath = txtPatchingApplicationXdeltaPath.Text;

            if (!File.Exists(isoPath) || !File.Exists(xdeltaPath))
            {
                MessageBox.Show("Please select valid ISO and xDelta files.");
                return;
            }

            if (isoPath.EndsWith(".nkit.iso", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("The selected ISO is an .nkit.iso file. Please use Swift's linked tool in Discord to convert it to a standard ISO.");
            }

            AppendLog("Applying xDelta patch...", InfoColor, rtbPatchingApplicationOutput);

            try
            {
                await ApplyXDeltaPatchAsync(isoPath, xdeltaPath);
                AppendLog("Patching completed successfully.", SuccessColor, rtbPatchingApplicationOutput);
            }
            catch (Exception ex)
            {
                AppendLog("Error during patching: " + ex.Message, ErrorColor, rtbPatchingApplicationOutput);
            }
        }

        private async Task<string> ComputeMD5Async(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = await Task.Run(() => md5.ComputeHash(stream));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private async Task ApplyXDeltaPatchAsync(string isoPath, string xdeltaPath)
        {
            string outputIsoPath = Path.Combine(Path.GetDirectoryName(isoPath), "Gladius_Patched.ISO");
            string xdeltaExePath = Path.Combine("tools", "xdelta3.exe");

            if (!File.Exists(xdeltaExePath))
            {
                MessageBox.Show("xdelta3.exe not found in tools folder.");
                throw new FileNotFoundException("xdelta3.exe not found in tools folder.");
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = xdeltaExePath,
                Arguments = $"-d -f -s {QuotePath(isoPath)} {QuotePath(xdeltaPath)} {QuotePath(outputIsoPath)}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLog(e.Data, InfoColor, rtbPatchingApplicationOutput); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog(e.Data, ErrorColor, rtbPatchingApplicationOutput); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception("xdelta3 patching failed.");
                }
            }
            txtISOPath.Text = outputIsoPath;
        }
        private void btnPatchingCreationVanISOPath_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "ISO files (*.iso)|*.iso|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtPatchingCreationVanISOPath.Text = openFileDialog.FileName;
                }
            }
        }

        private void btnPatchingCreationModISOPath_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "ISO files (*.iso)|*.iso|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtPatchingCreationModISOPath.Text = openFileDialog.FileName;
                }
            }
        }

        private void btnPatchingCreationOutputPath_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "xDelta files (*.xdelta)|*.xdelta|All files (*.*)|*.*";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtPatchingCreationOutputPath.Text = saveFileDialog.FileName;
                }
            }
        }

        private async void btnPatchingCreation_Click(object sender, EventArgs e)
        {
            string vanIsoPath = txtPatchingCreationVanISOPath.Text;
            string modIsoPath = txtPatchingCreationModISOPath.Text;
            string outputPatchPath = txtPatchingCreationOutputPath.Text;
            vanIsoPath = NormalizePath(vanIsoPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (!File.Exists(vanIsoPath) || !File.Exists(modIsoPath))
            {
                MessageBox.Show("Please select valid Vanilla and Modified ISO files.");
                return;
            }

            if (string.IsNullOrEmpty(outputPatchPath))
            {
                MessageBox.Show("Please select an output path for the xDelta patch.");
                return;
            }

            AppendLog("Creating xDelta patch...", InfoColor, rtbPatchingApplicationOutput);

            try
            {
                await CreateXDeltaPatchAsync(vanIsoPath, modIsoPath, outputPatchPath);
                AppendLog("Patch created successfully.", SuccessColor, rtbPatchingApplicationOutput);
            }
            catch (Exception ex)
            {
                AppendLog("Error during patch creation: " + ex.Message, ErrorColor, rtbPatchingApplicationOutput);
            }
        }


        private async Task CreateXDeltaPatchAsync(string vanIsoPath, string modIsoPath, string outputPatchPath)
        {
            string xdeltaExePath = Path.Combine("tools", "xdelta3.exe");

            if (!File.Exists(xdeltaExePath))
            {
                throw new FileNotFoundException("xdelta3.exe not found in tools folder.");
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = xdeltaExePath,
                Arguments = $"-e -f -s {QuotePath(vanIsoPath)} {QuotePath(modIsoPath)} {QuotePath(outputPatchPath)}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLog(e.Data, InfoColor, rtbPatchingApplicationOutput); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog(e.Data, ErrorColor, rtbPatchingApplicationOutput); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception("xdelta3 patch creation failed.");
                }
            }

        }
    }
}