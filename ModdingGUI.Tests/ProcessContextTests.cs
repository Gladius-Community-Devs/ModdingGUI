using System;
using ModdingGUI.Helpers;
using Xunit;

namespace ModdingGUI.Tests;

/// <summary>
/// The process-context probe must always answer, never fault.
/// </summary>
/// <remarks>
/// A previous version sized the integrity buffer with a constant and freed a pointer Marshal had not allocated, which
/// raised <see cref="AccessViolationException"/> - an uncatchable exception that killed the process. These assertions
/// exercise the real P/Invoke path: if it faults again, the test host dies and the suite reports a crash instead of a
/// green run.
/// </remarks>
public sealed class ProcessContextTests
{
    [Fact]
    public void DescribeAlwaysAnswers()
    {
        string described = ProcessContext.Describe();

        Assert.False(string.IsNullOrWhiteSpace(described));
        Assert.Contains("elevated=", described);
        Assert.Contains("integrity=", described);
        Assert.Contains("inJob=", described);
        Assert.Contains("interactive=", described);
        Assert.Contains("session=", described);
    }

    [Fact]
    public void IntegrityIsOneOfTheKnownLabels()
    {
        string[] allowed =
        {
            "Untrusted", "Low", "Medium", "Medium-Plus", "High", "System", "unknown",
        };

        string integrity = ProcessContext.IntegrityLevel();

        Assert.Contains(integrity, allowed);
    }

    [Fact]
    public void RepeatedCallsDoNotDisturbTheProcess()
    {
        // Leaking or double-freeing the token buffer shows up as instability over repeats.
        for (int i = 0; i < 200; i++)
        {
            _ = ProcessContext.IntegrityLevel();
            _ = ProcessContext.IsElevated();
            _ = ProcessContext.IsInJob();
        }

        Assert.NotNull(ProcessContext.Describe());
    }
}
