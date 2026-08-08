using ModdingGUI;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// Tests for <see cref="BatchContentBuilder.GenerateUnpackContent"/> and
/// <see cref="BatchContentBuilder.GeneratePackContent"/>.
/// All assertions inspect the returned batch script string — no file I/O needed.
/// </summary>
public class BatchContentBuilderTests
{
    private const string WorkingDir = @"C:\gladius";
    private const string ProjectName = "MyMod";
    private const string IsoPath = "C:/isos/Gladius.iso";
    private const string SelectedFolder = @"C:\gladius\MyMod";

    // GenerateUnpackContent ---------------------------------------------------

    [Fact]
    public void GenerateUnpackContent_ContainsNgcisoUnpackCommand()
    {
        string result = BatchContentBuilder.GenerateUnpackContent(IsoPath, ProjectName, WorkingDir);
        Assert.Contains("ngciso-tool.py", result);
        Assert.Contains("-unpack", result);
    }

    [Fact]
    public void GenerateUnpackContent_ContainsBecToolUnpackCommand()
    {
        string result = BatchContentBuilder.GenerateUnpackContent(IsoPath, ProjectName, WorkingDir);
        Assert.Contains("bec-tool-all.py", result);
        Assert.Contains("--platform GC -unpack", result);
    }

    [Fact]
    public void GenerateUnpackContent_ContainsUnitsUnpackCommand()
    {
        string result = BatchContentBuilder.GenerateUnpackContent(IsoPath, ProjectName, WorkingDir);
        Assert.Contains("Gladius_Units_IDX_Unpack.py", result);
    }

    [Fact]
    public void GenerateUnpackContent_HasExactlyThreeCommands()
    {
        string result = BatchContentBuilder.GenerateUnpackContent(IsoPath, ProjectName, WorkingDir);
        int lineCount = result.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.Equal(3, lineCount);
    }

    [Fact]
    public void GenerateUnpackContent_IsoPathAppearsQuoted()
    {
        string result = BatchContentBuilder.GenerateUnpackContent(IsoPath, ProjectName, WorkingDir);
        // The iso path should appear inside double-quotes
        Assert.Contains("\"", result);
        Assert.Contains("Gladius.iso", result);
    }

    [Fact]
    public void GenerateUnpackContent_ProjectNameAppearsInFileListArg()
    {
        string result = BatchContentBuilder.GenerateUnpackContent(IsoPath, ProjectName, WorkingDir);
        Assert.Contains($"{ProjectName}_FileList.txt", result);
    }

    [Fact]
    public void GenerateUnpackContent_ProjectNameAppearsInFolderPaths()
    {
        string result = BatchContentBuilder.GenerateUnpackContent(IsoPath, ProjectName, WorkingDir);
        Assert.Contains($"{ProjectName}_ISO", result);
        Assert.Contains($"{ProjectName}_BEC", result);
    }

    [Fact]
    public void GenerateUnpackContent_PathsUseForwardSlashes()
    {
        string result = BatchContentBuilder.GenerateUnpackContent(IsoPath, ProjectName, WorkingDir);
        // All path separators in the generated content should be forward slashes
        // (backslashes only appear inside quotes as escaped chars, not as path separators)
        string[] lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            // Strip quotes and check no raw backslashes remain in paths
            string stripped = line.Replace("\"\"", "").Replace("\"", "");
            Assert.DoesNotContain("\\", stripped);
        }
    }

    [Fact]
    public void GenerateUnpackContent_SpecialCharInProjectName_IsEscaped()
    {
        string result = BatchContentBuilder.GenerateUnpackContent(IsoPath, "My&Mod", WorkingDir);
        Assert.Contains("^&", result);
    }

    // GeneratePackContent -----------------------------------------------------

    [Fact]
    public void GeneratePackContent_ContainsAllSixCommands()
    {
        string result = BatchContentBuilder.GeneratePackContent(SelectedFolder, WorkingDir);
        int lineCount = result.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.Equal(6, lineCount);
    }

    [Fact]
    public void GeneratePackContent_FirstCommandIsTokNumUpdate()
    {
        string result = BatchContentBuilder.GeneratePackContent(SelectedFolder, WorkingDir);
        string firstLine = result.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0];
        Assert.Contains("Tok_Num_Update.py", firstLine);
    }

    [Fact]
    public void GeneratePackContent_SecondCommandIsTokTool()
    {
        string result = BatchContentBuilder.GeneratePackContent(SelectedFolder, WorkingDir);
        string secondLine = result.Split('\n', StringSplitOptions.RemoveEmptyEntries)[1];
        Assert.Contains("tok-tool.py", secondLine);
        Assert.Contains("-c", secondLine);
        Assert.Contains("skills.tok", secondLine);
    }

    [Fact]
    public void GeneratePackContent_ThirdCommandIsUpdateStringsBin()
    {
        string result = BatchContentBuilder.GeneratePackContent(SelectedFolder, WorkingDir);
        string thirdLine = result.Split('\n', StringSplitOptions.RemoveEmptyEntries)[2];
        Assert.Contains("Update_Strings_Bin.py", thirdLine);
    }

    [Fact]
    public void GeneratePackContent_FourthCommandIsUnitsRepack()
    {
        string result = BatchContentBuilder.GeneratePackContent(SelectedFolder, WorkingDir);
        string fourthLine = result.Split('\n', StringSplitOptions.RemoveEmptyEntries)[3];
        Assert.Contains("Gladius_Units_IDX_Repack.py", fourthLine);
    }

    [Fact]
    public void GeneratePackContent_FifthCommandIsBecToolPack()
    {
        string result = BatchContentBuilder.GeneratePackContent(SelectedFolder, WorkingDir);
        string fifthLine = result.Split('\n', StringSplitOptions.RemoveEmptyEntries)[4];
        Assert.Contains("bec-tool-all.py", fifthLine);
        Assert.Contains("-pack", fifthLine);
        Assert.Contains("--platform GC", fifthLine);
    }

    [Fact]
    public void GeneratePackContent_SixthCommandIsNgcisoPack()
    {
        string result = BatchContentBuilder.GeneratePackContent(SelectedFolder, WorkingDir);
        string sixthLine = result.Split('\n', StringSplitOptions.RemoveEmptyEntries)[5];
        Assert.Contains("ngciso-tool.py", sixthLine);
        Assert.Contains("-pack", sixthLine);
    }

    [Fact]
    public void GeneratePackContent_OutputIsoNameMatchesProjectFolder()
    {
        string result = BatchContentBuilder.GeneratePackContent(SelectedFolder, WorkingDir);
        Assert.Contains("MyMod.iso", result);
    }

    [Fact]
    public void GeneratePackContent_BecAndIsoFoldersNamedFromBaseName()
    {
        string result = BatchContentBuilder.GeneratePackContent(SelectedFolder, WorkingDir);
        Assert.Contains("MyMod_BEC", result);
        Assert.Contains("MyMod_ISO", result);
    }

    [Fact]
    public void GeneratePackContent_PathsUseForwardSlashes()
    {
        string result = BatchContentBuilder.GeneratePackContent(SelectedFolder, WorkingDir);
        string[] lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            string stripped = line.Replace("\"\"", "").Replace("\"", "");
            Assert.DoesNotContain("\\", stripped);
        }
    }

    [Fact]
    public void GeneratePackContent_SpecialCharInPath_IsEscaped()
    {
        string result = BatchContentBuilder.GeneratePackContent(@"C:\gladius\My&Mod", WorkingDir);
        Assert.Contains("^&", result);
    }
}
