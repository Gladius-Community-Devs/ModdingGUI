using ModdingGUI;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// Tests for <see cref="PathHelper"/> — QuotePath, NormalizePath, EnsureTrailingSeparator.
/// </summary>
public class PathHelperTests
{
    // QuotePath ---------------------------------------------------------------

    [Fact]
    public void QuotePath_EmptyString_ReturnsEmptyQuotes()
    {
        Assert.Equal("\"\"", PathHelper.QuotePath(""));
    }

    [Fact]
    public void QuotePath_WhitespaceOnly_ReturnsEmptyQuotes()
    {
        Assert.Equal("\"\"", PathHelper.QuotePath("   "));
    }

    [Fact]
    public void QuotePath_SimplePath_IsWrappedInDoubleQuotes()
    {
        string result = PathHelper.QuotePath(@"C:\tools\ngciso-tool.py");
        Assert.StartsWith("\"", result);
        Assert.EndsWith("\"", result);
    }

    [Fact]
    public void QuotePath_EscapesCaret()
    {
        string result = PathHelper.QuotePath("a^b");
        Assert.Contains("^^", result);
    }

    [Fact]
    public void QuotePath_EscapesAmpersand()
    {
        string result = PathHelper.QuotePath("a&b");
        Assert.Contains("^&", result);
    }

    [Fact]
    public void QuotePath_EscapesLessThan()
    {
        string result = PathHelper.QuotePath("a<b");
        Assert.Contains("^<", result);
    }

    [Fact]
    public void QuotePath_EscapesGreaterThan()
    {
        string result = PathHelper.QuotePath("a>b");
        Assert.Contains("^>", result);
    }

    [Fact]
    public void QuotePath_EscapesPipe()
    {
        string result = PathHelper.QuotePath("a|b");
        Assert.Contains("^|", result);
    }

    [Fact]
    public void QuotePath_EscapesParentheses()
    {
        string result = PathHelper.QuotePath("a(b)c");
        Assert.Contains("^(", result);
        Assert.Contains("^)", result);
    }

    [Fact]
    public void QuotePath_EscapesPercent()
    {
        string result = PathHelper.QuotePath("a%b");
        Assert.Contains("%%", result);
    }

    [Fact]
    public void QuotePath_EscapesEmbeddedDoubleQuotes()
    {
        string result = PathHelper.QuotePath("a\"b");
        Assert.Contains("\"\"", result);
    }

    [Fact]
    public void QuotePath_CleanPath_ContainsOriginalSegments()
    {
        string result = PathHelper.QuotePath(@"C:\projects\MyMod");
        Assert.Contains("projects", result);
        Assert.Contains("MyMod", result);
    }

    // NormalizePath -----------------------------------------------------------

    [Fact]
    public void NormalizePath_ReplacesBackslashesWithForwardSlashes()
    {
        string result = PathHelper.NormalizePath(@"C:\tools\ngciso-tool.py");
        Assert.DoesNotContain("\\", result);
        Assert.Contains("/", result);
    }

    [Fact]
    public void NormalizePath_CollapsesConsecutiveSlashes()
    {
        string result = PathHelper.NormalizePath("C:/tools//scripts///file.py");
        Assert.DoesNotContain("//", result);
    }

    [Fact]
    public void NormalizePath_AlreadyNormalizedPath_IsUnchanged()
    {
        string path = "C:/tools/ngciso-tool.py";
        Assert.Equal(path, PathHelper.NormalizePath(path));
    }

    [Fact]
    public void NormalizePath_MixedSeparators_AllBecomForwardSlash()
    {
        string result = PathHelper.NormalizePath(@"C:\tools/sub\file.py");
        Assert.DoesNotContain("\\", result);
        Assert.Equal("C:/tools/sub/file.py", result);
    }

    // EnsureTrailingSeparator ------------------------------------------------

    [Fact]
    public void EnsureTrailingSeparator_AddsTrailingSeparator_WhenMissing()
    {
        string result = PathHelper.EnsureTrailingSeparator(@"C:\projects\MyMod");
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), result);
    }

    [Fact]
    public void EnsureTrailingSeparator_DoesNotDoubleUp_WhenAlreadyPresent()
    {
        string path = @"C:\projects\MyMod" + Path.DirectorySeparatorChar;
        string result = PathHelper.EnsureTrailingSeparator(path);
        Assert.False(result.EndsWith($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}"));
    }

    [Fact]
    public void EnsureTrailingSeparator_NormalizesAltSeparatorToDirectorySeparator()
    {
        string path = "C:/projects/MyMod/";
        string result = PathHelper.EnsureTrailingSeparator(path);
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), result);
    }
}
