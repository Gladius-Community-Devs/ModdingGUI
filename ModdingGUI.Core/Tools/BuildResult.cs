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
