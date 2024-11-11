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

            // Commands
            sb.AppendLine($"python {QuotePath(ngcisoToolPath)} -unpack {QuotePath(isoPath)} {QuotePath(isoFolder)} {userInput}_FileList.txt");
            sb.AppendLine($"python {QuotePath(becToolPath)} --platform GC -unpack {QuotePath(becInputPath)} {QuotePath(becFolder)}");
            sb.AppendLine($"python {QuotePath(unitsUnpackToolPath)} {QuotePath(dataFolder)}");

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

            // Commands
            sb.AppendLine($"python {QuotePath(tokNumUpdatePath)} {QuotePath(dataConfigFolder)}");
            sb.AppendLine($"python {QuotePath(tokToolPath)} -c {QuotePath(skillsTokPath)} {QuotePath(skillsStringsBinPath)} {QuotePath(skillsLinesBinPath)} {QuotePath(skillsTokBrfPath)}");
            sb.AppendLine($"python {QuotePath(updateStringsBinPath)} {QuotePath(dataConfigFolder)}");
            sb.AppendLine($"python {QuotePath(unitsRepackToolPath)} {QuotePath(dataFolder)}");
            sb.AppendLine($"python {QuotePath(becToolPath)} -pack {QuotePath(becFolder)} {QuotePath(becFilePath)} {QuotePath(fileListPath)} --platform GC");
            sb.AppendLine($"python {QuotePath(ngcisoToolPath)} -pack {QuotePath(isoFolder)} {QuotePath(fstBinPath)} {QuotePath(gladiusFileListPath)} {QuotePath(isoFile)}");

            return sb.ToString();
        }



        // Asynchronous method to run the generated batch file
        private async Task RunBatchFileAsync(string batchContent, string workingDirectory, bool isUnpack)
        {
            // Generate a unique batch file name using a GUID
            var batchFilePath = Path.Combine(workingDirectory, $"GeneratedBatch_{Guid.NewGuid()}.bat");

            try
            {
                // Write the batch content to the batch file
                File.WriteAllText(batchFilePath, batchContent);
                AppendLog($"Saved batch file: {batchFilePath}", InfoColor, isUnpack); // Log that the batch file was saved

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
                    process.OutputDataReceived += (sender, e) => LogProcessOutput(e, isUnpack);
                    process.ErrorDataReceived += (sender, e) => LogProcessOutput(e, isUnpack, ErrorColor);

                    process.Start(); // Start the process
                    process.BeginOutputReadLine(); // Begin asynchronous read of standard output
                    process.BeginErrorReadLine();  // Begin asynchronous read of standard error

                    await process.WaitForExitAsync(); // Wait for the process to exit asynchronously
                }
            }
            catch (Exception ex)
            {
                // Log any exceptions that occur during execution
                AppendLog($"An error occurred while executing the batch file: {ex.Message}", ErrorColor, isUnpack);
            }
            finally
            {
                // If the user has not chosen to save batch files and the file exists
                if (!saveBatMenuItem.Checked && File.Exists(batchFilePath))
                {
                    File.Delete(batchFilePath); // Delete the batch file
                    AppendLog("Batch file deleted after execution.", InfoColor, isUnpack); // Log that the batch file was deleted
                }
            }
        }
    }
}
