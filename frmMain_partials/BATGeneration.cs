using System.Diagnostics;

namespace ModdingGUI
{
    public partial class frmMain
    {
        // Method to generate the content of the batch file for unpacking operations
        private string GenerateUnpackBatchFileContent(string isoPath, string userInput)
        {
            return BatchContentBuilder.GenerateUnpackContent(isoPath, userInput, Directory.GetCurrentDirectory());
        }


        // Method to generate the content of the batch file for packing operations
        private string GeneratePackBatchFileContent(string selectedFolder)
        {
            return BatchContentBuilder.GeneratePackContent(selectedFolder, Directory.GetCurrentDirectory());
        }



        // Asynchronous method to run the generated batch file
        private async Task RunBatchFileAsync(string batchContent, string workingDirectory, RichTextBox targetRtb = null)
        {
            // Generate a unique batch file name using a GUID
            var batchFilePath = Path.Combine(workingDirectory, $"GeneratedBatch_{Guid.NewGuid()}.bat");

            try
            {
                // Write the batch content to the batch file
                File.WriteAllText(batchFilePath, batchContent);
                AppendLog($"Saved batch file: {batchFilePath}", InfoColor, targetRtb); // Log that the batch file was saved

                // Use PathHelper.QuotePath to properly quote and escape the batch file path
                string quotedBatchFilePath = PathHelper.QuotePath(batchFilePath);

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
        }
    }
}