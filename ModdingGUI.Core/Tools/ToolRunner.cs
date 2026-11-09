namespace ModdingGUI.Core.Tools;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModdingGUI.Core.Logging;

/// <summary>
/// Runs <see cref="ToolStep"/>s directly — no batch file, no <c>cmd.exe</c> — and streams what they print into
/// <see cref="IAppLog"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the runner Phase 7.8 needed and Phase 7.9 made worth writing once: <see cref="ProcessStartInfo.ArgumentList"/>
/// hands each argument to the process verbatim, so a project under <c>C:\Mods (backup)\...</c> packs like any other
/// and no path has to be quoted or escaped first.
/// </para>
/// <para>
/// Output goes to the log <em>as it arrives</em>, because a pack takes minutes and a silent gap looks like a hang.
/// stdout is Info and stderr is Warning, matching the sink's convention; the lines are also captured on
/// <see cref="BuildStepResult.Output"/> so a caller can assert on them without a log.
/// </para>
/// </remarks>
public sealed class ToolRunner
{
    /// <summary>Exit code reported for a step that was cancelled rather than run to completion.</summary>
    public const int CancelledExitCode = -1;

    private readonly IAppLog? _log;
    private readonly string _scope;

    /// <summary>Create a runner; pass a log to observe the toolchain.</summary>
    /// <param name="log">Where child-process output is written, or null to only capture it.</param>
    /// <param name="scope">Source tag for the lines written.</param>
    public ToolRunner(IAppLog? log = null, string scope = "tools")
    {
        _log = log;
        _scope = string.IsNullOrWhiteSpace(scope) ? "tools" : scope;
    }

    /// <summary>
    /// Run the steps in order, stopping at the first failure.
    /// </summary>
    /// <param name="steps">What to run.</param>
    /// <param name="cancellationToken">Kills the running child (and its tree) when signalled.</param>
    /// <param name="stopOnFailure">False to keep going and report the first non-zero exit code.</param>
    /// <returns>The first non-zero exit code, and every step that ran.</returns>
    public BuildResult Run(
        IReadOnlyList<ToolStep> steps,
        CancellationToken cancellationToken = default,
        bool stopOnFailure = true)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0) return BuildResult.Empty;

        var results = new List<BuildStepResult>(steps.Count);
        int exitCode = 0;

        foreach (ToolStep step in steps)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                results.Add(new BuildStepResult(step.Name, CancelledExitCode, Array.Empty<string>()));
                exitCode = CancelledExitCode;
                break;
            }

            BuildStepResult result = RunStep(step, cancellationToken);
            results.Add(result);

            if (!result.Succeeded)
            {
                exitCode = result.ExitCode;
                if (stopOnFailure) break;
            }
        }

        return new BuildResult(exitCode, results);
    }

    /// <summary>Run one step and wait for it.</summary>
    /// <param name="step">What to run.</param>
    /// <param name="cancellationToken">Kills the child (and its tree) when signalled.</param>
    /// <returns>The step's exit code and captured output.</returns>
    public BuildStepResult RunStep(ToolStep step, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentException.ThrowIfNullOrWhiteSpace(step.Executable);

        if (cancellationToken.IsCancellationRequested)
            return new BuildStepResult(step.Name, CancelledExitCode, Array.Empty<string>());

        // Echoing the command is what makes a headless run auditable: a support report contains the exact
        // interpreter, script and arguments that ran. The display form, not the batch form — the arguments reach the
        // process verbatim, so a caret escape in the log would be a lie.
        _log?.Write(LogLevel.Debug, $"$ {step.ToDisplayString()}", _scope);

        var startInfo = new ProcessStartInfo
        {
            FileName = step.Executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        if (!string.IsNullOrWhiteSpace(step.WorkingDirectory))
        {
            if (!Directory.Exists(step.WorkingDirectory))
            {
                string message = $"working directory not found: {step.WorkingDirectory}";
                _log?.Write(LogLevel.Error, message, _scope);
                return new BuildStepResult(step.Name, 1, new[] { message });
            }

            startInfo.WorkingDirectory = step.WorkingDirectory!;
        }

        foreach (string argument in step.Arguments) startInfo.ArgumentList.Add(argument);

        var output = new List<string>();
        var gate = new object();

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                string message = $"could not start {step.Executable}";
                _log?.Write(LogLevel.Error, message, _scope);
                return new BuildStepResult(step.Name, 1, new[] { message });
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            // A missing interpreter is the common case here (Python not installed, tools folder not copied).
            string message = $"{step.Executable}: {ex.Message}";
            _log?.Write(LogLevel.Error, message, _scope);
            return new BuildStepResult(step.Name, 1, new[] { message });
        }

        Task stdout = Task.Run(() => Pump(process.StandardOutput, LogLevel.Info, output, gate));
        Task stderr = Task.Run(() => Pump(process.StandardError, LogLevel.Warning, output, gate));

        using var registration = cancellationToken.Register(() => TryKill(process));

        process.WaitForExit();
        Task.WaitAll(stdout, stderr);

        bool cancelled = cancellationToken.IsCancellationRequested;
        int exitCode = cancelled ? CancelledExitCode : process.ExitCode;

        if (cancelled)
        {
            _log?.Write(LogLevel.Warning, $"cancelled: {step.Name}", _scope);
        }
        else if (exitCode != 0)
        {
            _log?.Write(LogLevel.Error, $"{step.Name} exited with code {exitCode}.", _scope);
        }

        lock (gate) return new BuildStepResult(step.Name, exitCode, output.ToArray());
    }

    private void Pump(StreamReader reader, LogLevel level, List<string> output, object gate)
    {
        try
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                // An empty line from a child is still a line of its output; keep it verbatim.
                lock (gate) output.Add(line);
                _log?.Write(level, line, _scope);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The child died mid-line; the exit code carries the verdict.
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Already gone, or not killable; the wait below still returns.
        }
    }
}
