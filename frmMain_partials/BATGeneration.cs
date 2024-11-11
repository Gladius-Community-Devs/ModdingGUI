using System.Diagnostics;
using System.Text;

namespace ModdingGUI
{
    public partial class frmMain
    {
        // Method to generate the content of the batch file for unpacking operations
        private string GenerateUnpackBatchFileContent(string isoPath, string userInput)
        {
            StringBuilder sb = new StringBuilder(); // StringBuilder to construct the batch file content

            // Create the top-level folder based on user input in the current directory
            string topLevelFolder = Path.Combine(Directory.GetCurrentDirectory(), userInput);
            Directory.CreateDirectory(topLevelFolder); // Create the directory if it doesn't exist
            AppendLog($"Top-level folder created: {topLevelFolder}"); // Log the creation of the folder

            // Define the tools path relative to the current directory and normalize it
            string toolsPath = Path.Combine(Directory.GetCurrentDirectory(), "tools");
            toolsPath = NormalizePath(toolsPath);

            // Define paths for the ISO and BEC folders within the top-level folder
            string isoFolder = Path.Combine(topLevelFolder, $"{userInput}_ISO");
            isoFolder = EnsureTrailingSeparator(isoFolder);
            isoFolder = NormalizePath(isoFolder);

            string becFolder = Path.Combine(topLevelFolder, $"{userInput}_BEC");
            becFolder = EnsureTrailingSeparator(becFolder);
            becFolder = NormalizePath(becFolder);

            Directory.CreateDirectory(isoFolder); // Ensure the ISO folder exists for the file list

            // Prepare the command to unpack the ISO file
            string ngcisoToolPath = Path.Combine(toolsPath, "ngciso-tool.py"); // Path to ngciso-tool.py
            ngcisoToolPath = NormalizePath(ngcisoToolPath);

            string isoPathQuoted = QuotePath(NormalizePath(isoPath));                         // Quoted and normalized ISO path
            string isoFolderQuoted = QuotePath(isoFolder);                                    // Quoted ISO folder path

            // Append the command to the StringBuilder
            sb.AppendLine($"python \"{ngcisoToolPath}\" -unpack {isoPathQuoted} {isoFolderQuoted} {userInput}_FileList.txt");

            // Prepare the command to unpack BEC files
            string becToolPath = Path.Combine(toolsPath, "bec-tool-all.py");        // Path to bec-tool-all.py
            becToolPath = NormalizePath(becToolPath);

            string becInputPath = Path.Combine(isoFolder, "gladius.bec");           // Path to gladius.bec within ISO folder
            becInputPath = NormalizePath(becInputPath);
            string becInputPathQuoted = QuotePath(becInputPath);                    // Quoted BEC input path
            string becFolderQuoted = QuotePath(becFolder);                          // Quoted BEC folder path

            // Append the command to the StringBuilder
            sb.AppendLine($"python \"{becToolPath}\" --platform GC -unpack {becInputPathQuoted} {becFolderQuoted}");

            // Prepare the command to unpack units
            string unitsUnpackToolPath = Path.Combine(toolsPath, "Gladius_Units_IDX_Unpack.py"); // Path to unit unpack tool
            unitsUnpackToolPath = NormalizePath(unitsUnpackToolPath);

            string dataFolder = Path.Combine(becFolder, "Data");        // Path to Data folder within BEC folder
            dataFolder = EnsureTrailingSeparator(dataFolder);
            dataFolder = NormalizePath(dataFolder);
            string dataFolderQuoted = QuotePath(dataFolder);            // Quoted Data folder path

            // Append the command to the StringBuilder
            sb.AppendLine($"python \"{unitsUnpackToolPath}\" \"{dataFolderQuoted}\"");

            return sb.ToString(); // Return the generated batch file content as a string
        }

        // Method to generate the content of the batch file for packing operations
        private string GeneratePackBatchFileContent(string selectedFolder)
        {
            StringBuilder sb = new StringBuilder(); // StringBuilder to construct the batch file content

            // Retrieve the base folder name from the user's input
            string baseName = Path.GetFileName(selectedFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // Define paths for tools, BEC folder, ISO folder, and output ISO file
            string toolsPath = Path.Combine(Directory.GetCurrentDirectory(), "tools");
            toolsPath = EnsureTrailingSeparator(toolsPath);
            toolsPath = NormalizePath(toolsPath);

            string becFolder = Path.Combine(selectedFolder, $"{baseName}_BEC"); // Path to BEC folder
            becFolder = EnsureTrailingSeparator(becFolder);
            becFolder = NormalizePath(becFolder);

            string isoFolder = Path.Combine(selectedFolder, $"{baseName}_ISO"); // Path to ISO folder
            isoFolder = EnsureTrailingSeparator(isoFolder);
            isoFolder = NormalizePath(isoFolder);

            string isoFile = Path.Combine(selectedFolder, $"{baseName}.iso");   // Path to output ISO file
            isoFile = NormalizePath(isoFile);

            // Prepare the command to update token numbers
            string tokNumUpdatePath = Path.Combine(toolsPath, "Tok_Num_Update.py");              // Path to Tok_Num_Update.py
            tokNumUpdatePath = NormalizePath(tokNumUpdatePath);

            string dataConfigFolder = Path.Combine(becFolder, "Data", "Config"); // Path to Config folder
            dataConfigFolder = EnsureTrailingSeparator(dataConfigFolder);
            dataConfigFolder = NormalizePath(dataConfigFolder);

            sb.AppendLine($"python \"{tokNumUpdatePath}\" \"{dataConfigFolder}\"");                             // Append the command

            // Prepare the command to compile skills.tok
            string tokToolPath = Path.Combine(toolsPath, "tok-tool.py");               // Path to tok-tool.py
            tokToolPath = NormalizePath(tokToolPath);

            string skillsTokPath = Path.Combine(dataConfigFolder, "skills.tok");       // Path to skills.tok
            skillsTokPath = NormalizePath(skillsTokPath);

            string skillsStringsBinPath = Path.Combine(dataConfigFolder, "skills_strings.bin"); // Path to skills_strings.bin
            skillsStringsBinPath = NormalizePath(skillsStringsBinPath);

            string skillsLinesBinPath = Path.Combine(dataConfigFolder, "skills_lines.bin");     // Path to skills_lines.bin
            skillsLinesBinPath = NormalizePath(skillsLinesBinPath);

            string skillsTokBrfPath = Path.Combine(dataConfigFolder, "skills.tok.brf");         // Path to skills.tok.brf
            skillsTokBrfPath = NormalizePath(skillsTokBrfPath);

            sb.AppendLine($"python \"{tokToolPath}\" -c \"{skillsTokPath}\" \"{skillsStringsBinPath}\" \"{skillsLinesBinPath}\" \"{skillsTokBrfPath}\""); // Append the command

            // Prepare the command to update strings.bin
            string updateStringsBinPath = Path.Combine(toolsPath, "Update_Strings_Bin.py"); // Path to Update_Strings_Bin.py
            updateStringsBinPath = NormalizePath(updateStringsBinPath);

            sb.AppendLine($"python \"{updateStringsBinPath}\" \"{dataConfigFolder}\"");                   // Append the command

            // Prepare the command to repack units
            string unitsRepackToolPath = Path.Combine(toolsPath, "Gladius_Units_IDX_Repack.py"); // Path to unit repack tool
            unitsRepackToolPath = NormalizePath(unitsRepackToolPath);

            string dataFolder = Path.Combine(becFolder, "Data");        // Path to Data folder
            dataFolder = EnsureTrailingSeparator(dataFolder);
            dataFolder = NormalizePath(dataFolder);

            sb.AppendLine($"python \"{unitsRepackToolPath}\" \"{dataFolder}\"");                                // Append the command

            // Prepare the command to pack BEC files
            string becToolPath = Path.Combine(toolsPath, "bec-tool-all.py");        // Path to bec-tool-all.py
            becToolPath = NormalizePath(becToolPath);

            string becFilePath = Path.Combine(isoFolder, "gladius.bec");           // Path to gladius.bec output, which lives in the ISO folder
            becFilePath = NormalizePath(becFilePath);

            string fileListPath = Path.Combine(becFolder, "filelist.txt");         // Path to filelist.txt
            fileListPath = NormalizePath(fileListPath);

            sb.AppendLine($"python \"{becToolPath}\" -pack \"{becFolder}\" \"{becFilePath}\" \"{fileListPath}\" --platform GC"); // Append the command

            // Prepare the command to pack the ISO
            string ngcisoToolPath = Path.Combine(toolsPath, "ngciso-tool.py");                // Path to ngciso-tool.py
            ngcisoToolPath = NormalizePath(ngcisoToolPath);

            string fstBinPath = Path.Combine(isoFolder, "fst.bin");                           // Path to fst.bin
            fstBinPath = NormalizePath(fstBinPath);

            string gladiusFileListPath = Path.Combine(isoFolder, $"{baseName}_FileList.txt"); // Path to file list
            gladiusFileListPath = NormalizePath(gladiusFileListPath);

            sb.AppendLine($"python \"{ngcisoToolPath}\" -pack \"{isoFolder}\" \"{fstBinPath}\" \"{gladiusFileListPath}\" \"{isoFile}\""); // Append the command

            return sb.ToString(); // Return the generated batch file content as a string
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

                // Set up the process start info to execute the batch file via cmd.exe
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchFilePath}\"",
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
