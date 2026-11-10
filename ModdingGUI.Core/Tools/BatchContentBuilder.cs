namespace ModdingGUI.Core.Tools;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// The toolbelt command chains for unpacking and packing, as structured steps.
/// </summary>
/// <remarks>
/// <para>
/// Before Phase 7.9 this class only produced batch text, which the app wrote to a <c>.bat</c> and ran through
/// <c>cmd.exe</c>. That made the transport — batch parsing — the thing every path had to survive. The chains are
/// now <see cref="ToolStep"/> lists that <see cref="ToolRunner"/> starts directly, so paths are passed to the
/// tools untouched.
/// </para>
/// <para>
/// The text form remains for two reasons: a user who ticks <c>Save batch files</c> can inspect or reuse the exact
/// chain, and the existing tests pin the chain's content. <see cref="ToBatchText"/> renders it from the same
/// steps, so the two forms cannot drift.
/// </para>
/// </remarks>
public static class BatchContentBuilder
{
    /// <summary>Generate the batch file content for unpacking an ISO into a project folder.</summary>
    /// <param name="isoPath">The ISO to unpack.</param>
    /// <param name="projectName">Folder name of the project to create.</param>
    /// <param name="workingDir">Folder the project is created in (the projects folder).</param>
    /// <param name="toolsDirectory">
    /// Folder holding the toolbelt, or null to use <paramref name="workingDir"/>. Projects can be configured to
    /// live elsewhere while <c>tools/</c> always sits beside the executable, so the two are separate.
    /// </param>
    public static string GenerateUnpackContent(string isoPath, string projectName, string workingDir,
        string? toolsDirectory = null)
    {
        // Historically this parameter is the *application* folder, because tools/ always sits beside the exe.
        string toolsBase = string.IsNullOrWhiteSpace(toolsDirectory) ? workingDir : toolsDirectory!;
        return ToBatchText(BuildUnpackSteps(isoPath, projectName, workingDir,
            Path.Combine(toolsBase, "tools")));
    }

    /// <summary>Generate the batch file content for re-packing a project folder back into an ISO.</summary>
    /// <param name="selectedFolder">The project folder.</param>
    /// <param name="workingDir">Root holding the toolbelt in its <c>tools</c> subfolder.</param>
    public static string GeneratePackContent(string selectedFolder, string workingDir)
        => ToBatchText(BuildPackSteps(selectedFolder, Path.Combine(workingDir, "tools")));

    /// <summary>Render steps as batch text, one line each.</summary>
    public static string ToBatchText(IEnumerable<ToolStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var builder = new StringBuilder();
        foreach (ToolStep step in steps) builder.AppendLine(step.ToBatchLine());
        return builder.ToString();
    }

    /// <summary>
    /// The unpack chain: ISO → <c>_ISO</c>, then the BEC archive → <c>_BEC</c>, then the unit index files.
    /// </summary>
    /// <param name="isoPath">The ISO to unpack.</param>
    /// <param name="projectName">Folder name of the project.</param>
    /// <param name="projectsFolder">Folder the project lives in.</param>
    /// <param name="toolsDirectory">Folder holding the Python toolbelt scripts themselves.</param>
    /// <param name="pythonCommand">Interpreter command line, e.g. <c>python</c> or <c>py -3</c>.</param>
    public static IReadOnlyList<ToolStep> BuildUnpackSteps(
        string isoPath,
        string projectName,
        string projectsFolder,
        string toolsDirectory,
        string pythonCommand = "python")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectsFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolsDirectory);

        string topLevelFolder = PathHelper.NormalizePath(
            PathHelper.EnsureTrailingSeparator(Path.Combine(projectsFolder, projectName)));
        string toolsPath = toolsDirectory;
        string ngcisoToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "ngciso-tool.py"));
        string isoFolder = PathHelper.NormalizePath(
            PathHelper.EnsureTrailingSeparator(Path.Combine(topLevelFolder, $"{projectName}_ISO")));
        string becToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "bec-tool-all.py"));
        string becInputPath = PathHelper.NormalizePath(Path.Combine(isoFolder, "gladius.bec"));
        string becFolder = PathHelper.NormalizePath(
            PathHelper.EnsureTrailingSeparator(Path.Combine(topLevelFolder, $"{projectName}_BEC")));
        string unitsUnpackToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "Gladius_Units_IDX_Unpack.py"));
        string dataFolder = PathHelper.NormalizePath(
            PathHelper.EnsureTrailingSeparator(Path.Combine(becFolder, "Data")));
        string fileListName = $"{projectName}_FileList.txt";

        // The tools were written expecting forward slashes (the app has always normalised paths for them), and
        // nothing here needs the Windows form: the argument reaches Python byte for byte.
        var steps = new List<ToolStep>
        {
            ToolStep.PythonIn(topLevelFolder, pythonCommand, ngcisoToolPath,
                "-unpack", PathHelper.NormalizePath(isoPath), isoFolder, fileListName),
            ToolStep.PythonIn(topLevelFolder, pythonCommand, becToolPath,
                "--platform", "GC", "-unpack", becInputPath, becFolder),
            ToolStep.PythonIn(topLevelFolder, pythonCommand, unitsUnpackToolPath, dataFolder),
        };

        // Arena maps for the Grid Builder's overlay. The BEC unpack above is what extracts the texture pages, and the
        // converter writes PNGs to <project>/maps - deliberately outside _BEC, because the packer walks that whole tree
        // and would ship the images inside the ISO.
        string mapConverterPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "pttp_tga.py"));
        if (File.Exists(mapConverterPath))
        {
            string mapSourceFolder = PathHelper.NormalizePath(
                Path.Combine(becFolder, "data", "texture", "gui", "maps"));
            string mapOutputFolder = PathHelper.NormalizePath(
                Path.Combine(topLevelFolder, "maps"));

            if (Directory.Exists(mapSourceFolder))
            {
                steps.Add(ToolStep.PythonIn(topLevelFolder, pythonCommand, mapConverterPath,
                    "--png", PathHelper.NormalizePath(mapSourceFolder), mapOutputFolder));
            }
        }

        return steps;
    }

    /// <summary>
    /// The pack chain: unit indexes, skills strings, unit strings, BEC archive, then the ISO.
    /// </summary>
    /// <param name="projectFolder">The project folder (holds <c>_BEC</c> and <c>_ISO</c>).</param>
    /// <param name="toolsDirectory">Folder holding the Python toolbelt scripts themselves.</param>
    /// <param name="pythonCommand">Interpreter command line, e.g. <c>python</c> or <c>py -3</c>.</param>
    public static IReadOnlyList<ToolStep> BuildPackSteps(
        string projectFolder,
        string toolsDirectory,
        string pythonCommand = "python")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolsDirectory);

        string project = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string baseName = Path.GetFileName(project);
        if (string.IsNullOrEmpty(baseName))
            throw new ArgumentException($"The project path has no folder name: {projectFolder}", nameof(projectFolder));

        // The chain runs from the project's parent, which is where the tools were always run from.
        string workingRoot = Path.GetDirectoryName(project) ?? project;

        string toolsPath = PathHelper.EnsureTrailingSeparator(toolsDirectory);
        string becFolder = PathHelper.NormalizePath(
            PathHelper.EnsureTrailingSeparator(Path.Combine(project, $"{baseName}_BEC")));
        string isoFolder = PathHelper.NormalizePath(
            PathHelper.EnsureTrailingSeparator(Path.Combine(project, $"{baseName}_ISO")));
        string isoFile = PathHelper.NormalizePath(Path.Combine(project, $"{baseName}.iso"));

        string tokNumUpdatePath = PathHelper.NormalizePath(Path.Combine(toolsPath, "Tok_Num_Update.py"));
        string dataConfigFolder = PathHelper.NormalizePath(
            PathHelper.EnsureTrailingSeparator(Path.Combine(becFolder, "Data", "Config")));
        string tokToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "tok-tool.py"));
        string skillsTokPath = PathHelper.NormalizePath(Path.Combine(dataConfigFolder, "skills.tok"));
        string skillsStringsBinPath = PathHelper.NormalizePath(Path.Combine(dataConfigFolder, "skills_strings.bin"));
        string skillsLinesBinPath = PathHelper.NormalizePath(Path.Combine(dataConfigFolder, "skills_lines.bin"));
        string skillsTokBrfPath = PathHelper.NormalizePath(Path.Combine(dataConfigFolder, "skills.tok.brf"));
        string updateStringsBinPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "Update_Strings_Bin.py"));

        string unitsRepackToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "Gladius_Units_IDX_Repack.py"));
        string dataFolder = PathHelper.NormalizePath(
            PathHelper.EnsureTrailingSeparator(Path.Combine(becFolder, "Data")));

        string becToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "bec-tool-all.py"));
        string becFilePath = PathHelper.NormalizePath(Path.Combine(isoFolder, "gladius.bec"));
        string fileListPath = PathHelper.NormalizePath(Path.Combine(becFolder, "filelist.txt"));

        string ngcisoToolPath = PathHelper.NormalizePath(Path.Combine(toolsPath, "ngciso-tool.py"));
        string fstBinPath = PathHelper.NormalizePath(Path.Combine(isoFolder, "fst.bin"));
        string gladiusFileListPath = PathHelper.NormalizePath(
            Path.Combine(isoFolder, $"{baseName}_FileList.txt"));

        return new[]
        {
            ToolStep.PythonIn(workingRoot, pythonCommand, tokNumUpdatePath, dataConfigFolder),
            ToolStep.PythonIn(workingRoot, pythonCommand, tokToolPath,
                "-c", skillsTokPath, skillsStringsBinPath, skillsLinesBinPath, skillsTokBrfPath),
            ToolStep.PythonIn(workingRoot, pythonCommand, updateStringsBinPath, dataConfigFolder),
            ToolStep.PythonIn(workingRoot, pythonCommand, unitsRepackToolPath, dataFolder),
            ToolStep.PythonIn(workingRoot, pythonCommand, becToolPath,
                "-pack", becFolder, becFilePath, fileListPath, "--platform", "GC"),
            ToolStep.PythonIn(workingRoot, pythonCommand, ngcisoToolPath,
                "-pack", isoFolder, fstBinPath, gladiusFileListPath, isoFile),
        };
    }
}
