namespace ModdingGUI.Core.Randomizer;

using System;
using System.Collections.Generic;
using System.Linq;
using ModdingGUI.Core.Logging;
using ModdingGUI.Core.Tools;

/// <summary>The outcome of a full randomize-and-build run.</summary>
/// <param name="Randomize">What the randomizer did, including the seed.</param>
/// <param name="Build">What the build did, or null when no build was requested.</param>
public sealed record RandomizerRunResult(RandomizerResult Randomize, BuildResult? Build)
{
    /// <summary>True when every requested step succeeded.</summary>
    public bool Succeeded => Build?.Succeeded ?? true;
}

/// <summary>
/// One call that randomizes a project and, optionally, compiles its scripts and packs it.
/// </summary>
/// <remarks>
/// This is the entry point the GUI and (later) the MCP server share: the same options record, the same seeded
/// run, the same build pipeline. Keeping the composition here means neither front end can accidentally run half
/// the pipeline.
/// </remarks>
public sealed class RandomizerRunner
{
    private readonly RandomizerService _randomizer;
    private readonly ProjectBuildService _build;
    private readonly IAppLog? _log;

    /// <summary>Create the runner; everything is injectable for tests.</summary>
    public RandomizerRunner(
        RandomizerService? randomizer = null, ProjectBuildService? build = null, IAppLog? log = null)
    {
        _randomizer = randomizer ?? new RandomizerService(log: log);
        _build = build ?? new ProjectBuildService(log);
        _log = log;
    }

    /// <summary>
    /// Randomize, then build when <paramref name="buildOptions"/> is given. Scripts are compiled before packing
    /// because the randomizer can introduce hero classes that the campaign scripts must know about.
    /// </summary>
    public RandomizerRunResult Run(
        string projectRoot,
        RandomizerOptions options,
        BuildOptions? buildOptions = null,
        IEnumerable<string>? scriptsToCompile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(options);

        var randomized = _randomizer.Randomize(projectRoot, options);

        if (buildOptions is null) return new RandomizerRunResult(randomized, null);

        // Scripts the run patched have to be compiled before packing, or the edits stay text on disk.
        var toCompile = (scriptsToCompile ?? Array.Empty<string>())
            .Concat(randomized.ModifiedScripts)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (toCompile.Length > 0)
        {
            var compiled = _build.CompileScripts(buildOptions, toCompile);
            if (!compiled.Succeeded)
            {
                _log?.Write(LogLevel.Error, "Script compilation failed; stopping before the pack.", "randomizer");
                return new RandomizerRunResult(randomized, compiled);
            }
        }

        var packed = _build.Pack(buildOptions);
        return new RandomizerRunResult(randomized, packed);
    }
}
