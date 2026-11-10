namespace ModdingGUI.Core.Tools;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One command that ran during a build.</summary>
/// <param name="Name">What the step does, for logs and the UI.</param>
/// <param name="ExitCode">Process exit code; 0 means success.</param>
/// <param name="Output">Captured lines, in order.</param>
public sealed record BuildStepResult(string Name, int ExitCode, IReadOnlyList<string> Output)
{
    /// <summary>True when the step succeeded.</summary>
    public bool Succeeded => ExitCode == 0;

    /// <summary>
    /// How long the step took, wall clock. Set by the runner; zero for a step that never ran or a result built by hand.
    /// </summary>
    /// <remarks>
    /// Recorded because the same pack is sometimes 15 seconds and sometimes two minutes, and the only useful question
    /// is *which step* changed. The log line per step carries it, and so does the job result.
    /// </remarks>
    public TimeSpan Duration { get; init; }

    /// <summary>How many lines of output the step produced — a cheap proxy for how chatty (and how costly) it is.</summary>
    public int OutputLines => Output.Count;
}

/// <summary>The result of a whole build.</summary>
/// <param name="ExitCode">First non-zero step exit code, or 0.</param>
/// <param name="Steps">Every step that ran, in order.</param>
public sealed record BuildResult(int ExitCode, IReadOnlyList<BuildStepResult> Steps)
{
    /// <summary>True when every step succeeded.</summary>
    public bool Succeeded => ExitCode == 0;

    /// <summary>The first step that failed, or null when all succeeded.</summary>
    public BuildStepResult? FirstFailure => Steps.FirstOrDefault(step => !step.Succeeded);

    /// <summary>An empty successful result, for a no-op build.</summary>
    public static BuildResult Empty { get; } = new(0, Array.Empty<BuildStepResult>());
}
