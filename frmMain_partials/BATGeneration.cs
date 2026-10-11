using System.Diagnostics;
using System.Text;

namespace ModdingGUI
{
    public partial class frmMain
    {
        // Method to generate the content of the batch file for unpacking operations
        private string GenerateUnpackBatchFileContent(string isoPath, string userInput)
        {
            StringBuilder sb = new StringBuilder();

            // Paths
            string topLevelFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(Directory.GetCurrentDirectory(), userInput)));
            string toolsPath = NormalizePath(Path.Combine(Directory.GetCurrentDirectory(), "tools"));
            string ngcisoToolPath = NormalizePath(Path.Combine(toolsPath, "ngciso-tool.py"));
            string isoFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(topLevelFolder, $"{userInput}_ISO")));
            string becToolPath = NormalizePath(Path.Combine(toolsPath, "bec-tool-all.py"));
            string becInputPath = NormalizePath(Path.Combine(isoFolder, "gladius.bec"));
            string becFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(topLevelFolder, $"{userInput}_BEC")));
            string unitsUnpackToolPath = NormalizePath(Path.Combine(toolsPath, "Gladius_Units_IDX_Unpack.py"));
            string dataFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(becFolder, "Data")));

            // Commands.
            //
            // Every step is guarded with "|| goto :<step>_failed" so that a tool
            // failure (the v008 tools exit non-zero on bad input) stops the pipeline
            // instead of letting the later steps run against stale data.
            //
            // hashes.txt is checked up front, but ONLY when the v008 toolchain is
            // actually installed (tools\tools_v008\). Without it bec-tool-all.py
            // silently names EVERY unpacked file "unknown-*", which produces a project
            // folder that looks okay but has no real paths in it. The nested "if exist"
            // keeps an exe-only partial update (old tools still in place) working exactly
            // as it did before.
            string hashesPath = NormalizePath(Path.Combine(toolsPath, "hashes.txt"));
            string v008MarkerPath = NormalizePath(Path.Combine(toolsPath, "tools_v008", "__init__.py"));

            sb.AppendLine("@echo off");
            sb.AppendLine($"if exist {QuotePath(v008MarkerPath)} if not exist {QuotePath(hashesPath)} goto :unpack_nohashes");
            sb.AppendLine($"python {QuotePath(ngcisoToolPath)} -unpack {QuotePath(isoPath)} {QuotePath(isoFolder)} {userInput}_FileList.txt || goto :unpack_failed");
            sb.AppendLine($"python {QuotePath(becToolPath)} --platform GC -unpack {QuotePath(becInputPath)} {QuotePath(becFolder)} || goto :unpack_failed");
            sb.AppendLine($"python {QuotePath(unitsUnpackToolPath)} {QuotePath(dataFolder)} || goto :unpack_failed");
            sb.AppendLine("goto :unpack_ok");
            sb.AppendLine(":unpack_nohashes");
            sb.AppendLine("echo.");
            sb.AppendLine("echo *****************************************************************");
            sb.AppendLine("echo  ERROR: the tools folder is missing hashes.txt");
            sb.AppendLine("echo  The upgraded toolchain needs that file to name the unpacked data.");
            sb.AppendLine("echo  Restore it from the 4.1.4 zip - it lives at tools\\hashes.txt");
            sb.AppendLine("echo  Unpacking was stopped and no project files were written.");
            sb.AppendLine("echo *****************************************************************");
            sb.AppendLine("echo.");
            sb.AppendLine("exit /b 2");
            sb.AppendLine(":unpack_failed");
            sb.AppendLine("echo.");
            sb.AppendLine("echo *****************************************************************");
            sb.AppendLine("echo  ERROR: a tool step failed - see the messages above.");
            sb.AppendLine("echo  Unpacking stopped early; this project folder is incomplete.");
            sb.AppendLine("echo *****************************************************************");
            sb.AppendLine("echo.");
            sb.AppendLine("exit /b 1");
            sb.AppendLine(":unpack_ok");
            sb.AppendLine("exit /b 0");

            return sb.ToString();
        }


        // Method to generate the content of the batch file for packing operations
        private string GeneratePackBatchFileContent(string selectedFolder)
        {
            StringBuilder sb = new StringBuilder();

            // Paths
            string baseName = Path.GetFileName(selectedFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string toolsPath = NormalizePath(EnsureTrailingSeparator(Path.Combine(Directory.GetCurrentDirectory(), "tools")));
            string becFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(selectedFolder, $"{baseName}_BEC")));
            string isoFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(selectedFolder, $"{baseName}_ISO")));
            string isoFile = NormalizePath(Path.Combine(selectedFolder, $"{baseName}.iso"));

            string tokNumUpdatePath = NormalizePath(Path.Combine(toolsPath, "Tok_Num_Update.py"));
            string dataConfigFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(becFolder, "Data", "Config")));
            string tokToolPath = NormalizePath(Path.Combine(toolsPath, "tok-tool.py"));
            string skillsTokPath = NormalizePath(Path.Combine(dataConfigFolder, "skills.tok"));
            string skillsStringsBinPath = NormalizePath(Path.Combine(dataConfigFolder, "skills_strings.bin"));
            string skillsLinesBinPath = NormalizePath(Path.Combine(dataConfigFolder, "skills_lines.bin"));
            string skillsTokBrfPath = NormalizePath(Path.Combine(dataConfigFolder, "skills.tok.brf"));
            string updateStringsBinPath = NormalizePath(Path.Combine(toolsPath, "Update_Strings_Bin.py"));

            string unitsRepackToolPath = NormalizePath(Path.Combine(toolsPath, "Gladius_Units_IDX_Repack.py"));
            string dataFolder = NormalizePath(EnsureTrailingSeparator(Path.Combine(becFolder, "Data")));

            string becToolPath = NormalizePath(Path.Combine(toolsPath, "bec-tool-all.py"));
            string becFilePath = NormalizePath(Path.Combine(isoFolder, "gladius.bec"));
            string fileListPath = NormalizePath(Path.Combine(becFolder, "filelist.txt"));

            string ngcisoToolPath = NormalizePath(Path.Combine(toolsPath, "ngciso-tool.py"));
            string fstBinPath = NormalizePath(Path.Combine(isoFolder, "fst.bin"));
            string gladiusFileListPath = NormalizePath(Path.Combine(isoFolder, $"{baseName}_FileList.txt"));

            // Commands.
            //
            // Every step is guarded with "|| goto :pack_failed" so that a failed step
            // (for example the IDX repack rejecting a unit that references a skill set
            // that does not exist) stops the pipeline. Without the guard the remaining
            // steps still run and produce a modded ISO that silently contains the OLD
            // data for the file that failed.
            sb.AppendLine("@echo off");
            sb.AppendLine($"python {QuotePath(tokNumUpdatePath)} {QuotePath(dataConfigFolder)} || goto :pack_failed");
            sb.AppendLine($"python {QuotePath(tokToolPath)} -c {QuotePath(skillsTokPath)} {QuotePath(skillsStringsBinPath)} {QuotePath(skillsLinesBinPath)} {QuotePath(skillsTokBrfPath)} || goto :pack_failed");
            sb.AppendLine($"python {QuotePath(updateStringsBinPath)} {QuotePath(dataConfigFolder)} || goto :pack_failed");
            sb.AppendLine($"python {QuotePath(unitsRepackToolPath)} {QuotePath(dataFolder)} || goto :pack_failed");
            sb.AppendLine($"python {QuotePath(becToolPath)} -pack {QuotePath(becFolder)} {QuotePath(becFilePath)} {QuotePath(fileListPath)} --platform GC || goto :pack_failed");
            sb.AppendLine($"python {QuotePath(ngcisoToolPath)} -pack {QuotePath(isoFolder)} {QuotePath(fstBinPath)} {QuotePath(gladiusFileListPath)} {QuotePath(isoFile)} || goto :pack_failed");
            sb.AppendLine("goto :pack_ok");
            sb.AppendLine(":pack_failed");
            sb.AppendLine("echo.");
            sb.AppendLine("echo *****************************************************************");
            sb.AppendLine("echo  ERROR: a tool step failed - see the messages above.");
            sb.AppendLine("echo  Packing stopped early. No ISO was written from this run,");
            sb.AppendLine("echo  or the ISO is missing whatever step failed.");
            sb.AppendLine("echo  Your project files were not changed by the failed step.");
            sb.AppendLine("echo *****************************************************************");
            sb.AppendLine("echo.");
            sb.AppendLine("exit /b 1");
            sb.AppendLine(":pack_ok");
            sb.AppendLine("exit /b 0");

            return sb.ToString();
        }



        // Asynchronous method to run the generated batch file.
        // Returns the batch file's exit code: 0 means every tool step succeeded.
        // A non-zero code means a step failed and the pipeline stopped early, so the
        // caller must NOT report success.
        private async Task<int> RunBatchFileAsync(string batchContent, string workingDirectory, RichTextBox targetRtb = null)
        {
            // Generate a unique batch file name using a GUID
            var batchFilePath = Path.Combine(workingDirectory, $"GeneratedBatch_{Guid.NewGuid()}.bat");
            int exitCode = -1;

            try
            {
                // Write the batch content to the batch file
                File.WriteAllText(batchFilePath, batchContent);
                AppendLog($"Saved batch file: {batchFilePath}", InfoColor, targetRtb); // Log that the batch file was saved

                // Use QuotePath to properly quote and escape the batch file path
                string quotedBatchFilePath = QuotePath(batchFilePath);

                // Set up the process start info to execute the batch file via cmd.exe
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {quotedBatchFilePath}",
                    WorkingDirectory = workingDirectory, // Set the working directory
                    UseShellExecute = false,           // Do not use the OS shell
                    RedirectStandardOutput = true,     // Redirect standard output
                    RedirectStandardError = true,      // Redirect standard error
                    CreateNoWindow = true,             // Do not create a window
                };

                // Create a new process with the specified start info
                using (var process = new Process { StartInfo = startInfo })
                {
                    // Subscribe to the output and error data received events
                    process.OutputDataReceived += (sender, e) => LogProcessOutput(e, InfoColor, targetRtb);
                    process.ErrorDataReceived += (sender, e) => LogProcessOutput(e, ErrorColor, targetRtb);

                    process.Start(); // Start the process
                    process.BeginOutputReadLine(); // Begin asynchronous read of standard output
                    process.BeginErrorReadLine();  // Begin asynchronous read of standard error

                    await process.WaitForExitAsync(); // Wait for the process to exit asynchronously
                    exitCode = process.ExitCode;       // 0 = every tool step in the batch succeeded
                }

                if (exitCode != 0)
                {
                    AppendLog($"The toolchain stopped with exit code {exitCode}. See the ERROR block above for which step failed.", ErrorColor, targetRtb);
                }
            }
            catch (Exception ex)
            {
                // Log any exceptions that occur during execution
                AppendLog($"An error occurred while executing the batch file: {ex.Message}", ErrorColor, targetRtb);
            }
            finally
            {
                // If the user has not chosen to save batch files and the file exists
                if (!saveBatMenuItem.Checked && File.Exists(batchFilePath))
                {
                    File.Delete(batchFilePath); // Delete the batch file
                    AppendLog("Batch file deleted after execution.", InfoColor, targetRtb); // Log that the batch file was deleted
                }
            }

            return exitCode;
        }
    }
}