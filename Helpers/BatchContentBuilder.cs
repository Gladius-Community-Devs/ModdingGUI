using System.Text;

namespace ModdingGUI
{
    public static class BatchContentBuilder
    {
        // Generate the batch file content for unpacking an ISO into a project folder.
        // workingDir: the application's working directory (Directory.GetCurrentDirectory()).
        public static string GenerateUnpackContent(string isoPath, string projectName, string workingDir)
        {
            var sb = new StringBuilder();

            string topLevelFolder = PathHelper.NormalizePath(PathHelper.EnsureTrailingSeparator(Path.Combine(workingDir, projectName)));
            string toolsPath = PathHelper.NormalizePath(Path.Combine(workingDir, "tools"));
            string ngcisoToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "ngciso-tool.py"));
            string isoFolder = PathHelper.NormalizePath(PathHelper.EnsureTrailingSeparator(Path.Combine(topLevelFolder, $"{projectName}_ISO")));
            string becToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "bec-tool-all.py"));
            string becInputPath = PathHelper.NormalizePath(Path.Combine(isoFolder, "gladius.bec"));
            string becFolder = PathHelper.NormalizePath(PathHelper.EnsureTrailingSeparator(Path.Combine(topLevelFolder, $"{projectName}_BEC")));
            string unitsUnpackToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "Gladius_Units_IDX_Unpack.py"));
            string dataFolder = PathHelper.NormalizePath(PathHelper.EnsureTrailingSeparator(Path.Combine(becFolder, "Data")));

            sb.AppendLine($"python {PathHelper.QuotePath(ngcisoToolPath)} -unpack {PathHelper.QuotePath(isoPath)} {PathHelper.QuotePath(isoFolder)} {projectName}_FileList.txt");
            sb.AppendLine($"python {PathHelper.QuotePath(becToolPath)} --platform GC -unpack {PathHelper.QuotePath(becInputPath)} {PathHelper.QuotePath(becFolder)}");
            sb.AppendLine($"python {PathHelper.QuotePath(unitsUnpackToolPath)} {PathHelper.QuotePath(dataFolder)}");

            return sb.ToString();
        }

        // Generate the batch file content for re-packing a project folder back into an ISO.
        // workingDir: the application's working directory (Directory.GetCurrentDirectory()).
        public static string GeneratePackContent(string selectedFolder, string workingDir)
        {
            var sb = new StringBuilder();

            string baseName = Path.GetFileName(selectedFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string toolsPath = PathHelper.NormalizePath(PathHelper.EnsureTrailingSeparator(Path.Combine(workingDir, "tools")));
            string becFolder = PathHelper.NormalizePath(PathHelper.EnsureTrailingSeparator(Path.Combine(selectedFolder, $"{baseName}_BEC")));
            string isoFolder = PathHelper.NormalizePath(PathHelper.EnsureTrailingSeparator(Path.Combine(selectedFolder, $"{baseName}_ISO")));
            string isoFile = PathHelper.NormalizePath(Path.Combine(selectedFolder, $"{baseName}.iso"));

            string tokNumUpdatePath = PathHelper.NormalizePath(Path.Combine(toolsPath, "Tok_Num_Update.py"));
            string dataConfigFolder = PathHelper.NormalizePath(PathHelper.EnsureTrailingSeparator(Path.Combine(becFolder, "Data", "Config")));
            string tokToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "tok-tool.py"));
            string skillsTokPath = PathHelper.NormalizePath(Path.Combine(dataConfigFolder, "skills.tok"));
            string skillsStringsBinPath = PathHelper.NormalizePath(Path.Combine(dataConfigFolder, "skills_strings.bin"));
            string skillsLinesBinPath = PathHelper.NormalizePath(Path.Combine(dataConfigFolder, "skills_lines.bin"));
            string skillsTokBrfPath = PathHelper.NormalizePath(Path.Combine(dataConfigFolder, "skills.tok.brf"));
            string updateStringsBinPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "Update_Strings_Bin.py"));

            string unitsRepackToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "Gladius_Units_IDX_Repack.py"));
            string dataFolder = PathHelper.NormalizePath(PathHelper.EnsureTrailingSeparator(Path.Combine(becFolder, "Data")));

            string becToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "bec-tool-all.py"));
            string becFilePath = PathHelper.NormalizePath(Path.Combine(isoFolder, "gladius.bec"));
            string fileListPath = PathHelper.NormalizePath(Path.Combine(becFolder, "filelist.txt"));

            string ngcisoToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "ngciso-tool.py"));
            string fstBinPath = PathHelper.NormalizePath(Path.Combine(isoFolder, "fst.bin"));
            string gladiusFileListPath = PathHelper.NormalizePath(Path.Combine(isoFolder, $"{baseName}_FileList.txt"));

            sb.AppendLine($"python {PathHelper.QuotePath(tokNumUpdatePath)} {PathHelper.QuotePath(dataConfigFolder)}");
            sb.AppendLine($"python {PathHelper.QuotePath(tokToolPath)} -c {PathHelper.QuotePath(skillsTokPath)} {PathHelper.QuotePath(skillsStringsBinPath)} {PathHelper.QuotePath(skillsLinesBinPath)} {PathHelper.QuotePath(skillsTokBrfPath)}");
            sb.AppendLine($"python {PathHelper.QuotePath(updateStringsBinPath)} {PathHelper.QuotePath(dataConfigFolder)}");
            sb.AppendLine($"python {PathHelper.QuotePath(unitsRepackToolPath)} {PathHelper.QuotePath(dataFolder)}");
            sb.AppendLine($"python {PathHelper.QuotePath(becToolPath)} -pack {PathHelper.QuotePath(becFolder)} {PathHelper.QuotePath(becFilePath)} {PathHelper.QuotePath(fileListPath)} --platform GC");
            sb.AppendLine($"python {PathHelper.QuotePath(ngcisoToolPath)} -pack {PathHelper.QuotePath(isoFolder)} {PathHelper.QuotePath(fstBinPath)} {PathHelper.QuotePath(gladiusFileListPath)} {PathHelper.QuotePath(isoFile)}");

            return sb.ToString();
        }
    }
}
