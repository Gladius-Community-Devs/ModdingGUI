using System.Diagnostics;
using System.Security.Cryptography;
using ModdingGUI.Core.Tools;

using ModdingGUI.Core.Discs;

namespace ModdingGUI
{
    public partial class frmMain : Form
    {
        // Updated event handler for selecting the Patching Application ISO path
        private async void btnPatchingApplicationISOPath_Click(object sender, EventArgs e)
        {
            try
            {
                btnPatchingApplicationApply.Enabled = false;

                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "Disc images (*.iso;*.gcm;*.nkit.iso;*.rvz;*.wia;*.gcz;*.ciso)|*.iso;*.gcm;*.nkit.iso;*.rvz;*.wia;*.gcz;*.ciso|ISO files (*.iso;*.gcm)|*.iso;*.gcm|All files (*.*)|*.*";
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        txtPatchingApplicationISOPath.Text = openFileDialog.FileName;
                    }
                }

                string isoPath = txtPatchingApplicationISOPath.Text;

                // Parentheses are ordinary characters now: xdelta3 is started with an argument list, so a file named
                // "Gladius (USA).iso" no longer needs copying or renaming first (PLAN.md Phase 7.9).
                if (!File.Exists(isoPath))
                {
                    AppendLog($"ISO not found: {isoPath}", ErrorColor, rtbPatchingApplicationOutput);
                    return;
                }

                // Only a plain ISO can be hashed directly. Compressed containers are converted at apply time, and the
                // vanilla check runs on the converted bytes then - hashing the container would always "fail".
                DiscImageFormat format = DiscImageProbe.Detect(isoPath);
                AppendLog($"Detected {DiscImageProbe.Describe(format)}.", InfoColor, rtbPatchingApplicationOutput);

                if (format == DiscImageFormat.PlainIso)
                {
                    await VerifyVanillaIsoAsync(isoPath);
                }
                else if (format is DiscImageFormat.Rvz or DiscImageFormat.Wia or DiscImageFormat.Gcz or DiscImageFormat.Ciso)
                {
                    string? converter = ResolveDiscImageConverter();
                    AppendLog(converter is null
                            ? "This image will need converting to a plain ISO first: set File > Settings > General > 'Disc image converter'."
                            : $"This image will be converted with '{Path.GetFileName(converter)}' when the patch is applied.",
                        converter is null ? WarningColor : InfoColor, rtbPatchingApplicationOutput);
                }
                else if (format == DiscImageFormat.Nkit)
                {
                    AppendLog("NKit (scrubbed) image: Dolphin can play it but cannot rebuild it, and xdelta needs real disc " +
                        "bytes. Convert it with NKit's ConvertToISO first, or supply a plain ISO.",
                        WarningColor, rtbPatchingApplicationOutput);
                }

                btnPatchingApplicationApply.Enabled = true;
            }
            catch (Exception ex)
            {
                AppendLog($"Patching ISO selection failed: {ex.Message}", ErrorColor);
            }
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

            // xdelta needs real disc bytes, so any container is converted to a plain ISO first (and the temporary file is
            // removed whatever happens). The MD5 gate ran on the selected file earlier; this path is the actual source.
            btnPatchingApplicationApply.Enabled = false;
            DiscImagePreparation prepared = PrepareDiscImage(isoPath, rtbPatchingApplicationOutput);
            if (!prepared.Ok || prepared.IsoPath is null)
            {
                btnPatchingApplicationApply.Enabled = true;
                return;
            }

            await VerifyVanillaIsoAsync(prepared.IsoPath);

            AppendLog("Applying xDelta patch...", InfoColor, rtbPatchingApplicationOutput);

            try
            {
                await ApplyXDeltaPatchAsync(prepared.IsoPath, xdeltaPath);
                AppendLog("Patching completed successfully.", SuccessColor, rtbPatchingApplicationOutput);
            }
            catch (Exception ex)
            {
                AppendLog("Error during patching: " + ex.Message, ErrorColor, rtbPatchingApplicationOutput);
            }
            finally
            {
                if (prepared.IsTemporary) DiscImagePreparer.Cleanup(prepared.IsoPath);
                btnPatchingApplicationApply.Enabled = true;
            }
        }

        /// <summary>
        /// The vanilla-ISO gate, on a plain ISO. Warns but never blocks: modded discs are a legitimate thing to patch.
        /// </summary>
        private async Task VerifyVanillaIsoAsync(string isoPath)
        {
            AppendLog("Verifying ISO MD5 hash...", InfoColor, rtbPatchingApplicationOutput);
            string md5Hash = await ComputeMD5Async(isoPath);

            if (string.Equals(md5Hash, VanillaIsoMd5, StringComparison.OrdinalIgnoreCase))
            {
                AppendLog("Verified against the vanilla ISO hash.", SuccessColor, rtbPatchingApplicationOutput);
                return;
            }

            AppendLog($"MD5 {md5Hash} does not match the vanilla ISO hash {VanillaIsoMd5}; proceeding may cause unexpected results.",
                WarningColor, rtbPatchingApplicationOutput);
            MessageBox.Show("The selected ISO does not match the expected Vanilla ISO. Proceeding may cause unexpected results.",
                "MD5 Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        /// <summary>The xdelta3 executable that ships in the toolbelt beside the app.</summary>
        /// <summary>The MD5 of the retail USA disc; the app has always used this one hash as its "clean ISO" marker.</summary>
        private const string VanillaIsoMd5 = "a78d6e1c9de69886ce827ad2a7507339";

        private static string XDeltaPath() =>
            Path.Combine(GetAppDirectory(), "tools", "xdelta3.exe");

        /// <summary>
        /// Run one xdelta3 command through Core, mirroring its output into the patching panel.
        /// </summary>
        /// <remarks>
        /// Arguments go to the process verbatim via <see cref="ToolRunner"/>, so a path containing parentheses,
        /// spaces or an ampersand needs no quoting — and must not get any, because a caret escape is literal to
        /// xdelta3. The old code passed <c>PathHelper.QuotePath</c> output here, which silently mangled such paths.
        /// </remarks>
        private async Task RunXDeltaAsync(string name, params string[] arguments)
        {
            string xdeltaExePath = XDeltaPath();
            if (!File.Exists(xdeltaExePath))
            {
                MessageBox.Show("xdelta3.exe not found in tools folder.");
                throw new FileNotFoundException("xdelta3.exe not found in tools folder.", xdeltaExePath);
            }

            var step = new ToolStep(name, xdeltaExePath, arguments);

            BuildStepResult result;
            using (MirrorLogInto(rtbPatchingApplicationOutput))
            {
                result = await Task.Run(() => new ToolRunner(_log, "patch").RunStep(step));
            }

            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"xdelta3 {name} failed (exit {result.ExitCode}).");
            }
        }

        private async Task ApplyXDeltaPatchAsync(string isoPath, string xdeltaPath)
        {
            string outputIsoPath = Path.Combine(Path.GetDirectoryName(isoPath)!, "Gladius_Patched.ISO");

            await RunXDeltaAsync("patch", "-d", "-f", "-s", isoPath, xdeltaPath, outputIsoPath);

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
            vanIsoPath = PathHelper.NormalizePath(vanIsoPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

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
            await RunXDeltaAsync("create", "-e", "-f", "-s", vanIsoPath, modIsoPath, outputPatchPath);
        }
    }
}