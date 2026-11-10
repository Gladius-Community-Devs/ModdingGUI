namespace ModdingGUI.Tests.GridBuilder;

using ModdingGUI;
using Xunit;

/// <summary>
/// Tests for the untuned-map fitting rule: an arena page with no hand-tuned transform has to fill the board, otherwise it
/// opens as a thumbnail in the corner (which is what used to happen for every arena except the four with tuned values).
/// </summary>
public class GridMapFitTests
{
    [Fact]
    public void FitScaleFor_ArenaPage_FillsTheBoard()
    {
        // 32 cells x 20 px = 640 px board, 256 px page.
        Assert.Equal(2.5m, GridBuilderView.FitScaleFor(256));
    }

    [Theory]
    [InlineData(128, 5.0)]
    [InlineData(320, 2.0)]
    [InlineData(640, 1.0)]
    public void FitScaleFor_ScalesInverselyWithImageWidth(int imageWidth, decimal expected)
    {
        Assert.Equal(expected, GridBuilderView.FitScaleFor(imageWidth));
    }

    [Fact]
    public void FitScaleFor_DegenerateWidth_DoesNotDivideByZero()
    {
        Assert.Equal(1m, GridBuilderView.FitScaleFor(0));
        Assert.Equal(1m, GridBuilderView.FitScaleFor(-10));
    }
}
