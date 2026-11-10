namespace ModdingGUI
{
    using System.Text.Json;
    using ModdingGUI.Core.Discs;

    /// <summary>
    /// The <c>convert</c> driver command: the Convert page's agent surface.
    /// </summary>
    /// <remarks>
    /// Same job as the page's own controls, without the file dialogs - a modal file picker blocks the driver. Actions:
    /// <c>state</c> (what can be converted, and with what), <c>detect</c> (what a file is), <c>run</c>
    /// (source + destination + optional target). See docs/UI_AUTOMATION.md.
    /// </remarks>
    public partial class frmMain
    {
        private string ConvertCommand(JsonElement request)
        {
            string action = UiString(request, "action")?.ToLowerInvariant() ?? "state";

            switch (action)
            {
                case "state":
                {
                    DiscImageConverter converter = CreateConverter();
                    return UiOk(new
                    {
                        ok = true,
                        page = _txtConvertSource is not null,
                        source = _txtConvertSource?.Text,
                        detected = DiscImageProbe.Describe(_convertSourceFormat),
                        destination = _txtConvertDestination?.Text,
                        target = SelectedConvertTarget().ToString(),
                        availableTargets = converter.AvailableTargets().Select(t => t.ToString()).ToArray(),
                        hasDolphinTool = converter.HasDolphinTool,
                        hasNkitTool = converter.HasNkitTool,
                        lastOutput = _lastConvertOutput,
                    });
                }

                case "load":
                {
                    // Put a source on the page exactly as Browse would, so a screenshot or a follow-up run matches what a
                    // user sees (the driver cannot open a file dialog).
                    string? path = UiString(request, "source");
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return UiFail("no such file: " + path);

                    LoadConvertSource(path);

                    if (UiString(request, "target") is { } wanted && ParseTarget(wanted) is { } parsed)
                    {
                        SetConvertTarget(parsed);
                    }

                    return UiOk(new { source = _txtConvertSource.Text, detected = DiscImageProbe.Describe(_convertSourceFormat), destination = _txtConvertDestination.Text, target = SelectedConvertTarget().ToString() });
                }

                case "detect":
                {
                    string? path = UiString(request, "source");
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return UiFail("no such file: " + path);

                    DiscImageFormat format;
                    try
                    {
                        format = DiscImageProbe.Detect(path);
                    }
                    catch (IOException ex)
                    {
                        return UiFail(ex.Message);
                    }

                    return UiOk(new { path, format = format.ToString(), description = DiscImageProbe.Describe(format) });
                }

                case "run":
                {
                    string? source = UiString(request, "source") ?? _txtConvertSource?.Text;
                    if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                    {
                        return UiFail("no source image (pass one, or set 'source' on the page)");
                    }

                    DiscImageTarget target = ParseTarget(UiString(request, "target")) ?? SelectedConvertTarget();

                    string destination = UiString(request, "destination") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(destination))
                    {
                        destination = DefaultConvertDestination(source, target);
                    }

                    // Call the Core converter directly, off the UI thread. RunConvertAsync updates controls in its
                    // continuations, so blocking on it here would deadlock the UI thread that the driver runs on.
                    ConvertLog($"Converting '{Path.GetFileName(source)}' to {target}...", LogKind.Info);

                    DiscImageConverter converter = CreateConverter();
                    DiscImageConversion result = Task.Run(() => converter.Convert(source, destination, target)).GetAwaiter().GetResult();
                    ConvertLog(result.Message, result.Ok ? LogKind.Success : LogKind.Error);
                    if (result.Ok) _lastConvertOutput = result.OutputPath;
                    return result.Ok
                        ? UiOk(new { output = result.OutputPath, message = result.Message, target = target.ToString() })
                        : UiFail(result.Message);
                }

                default:
                    return UiFail($"unknown action '{action}' (state, detect, run)");
            }
        }

        private static DiscImageTarget? ParseTarget(string? target) => target?.ToLowerInvariant() switch
        {
            "iso" => DiscImageTarget.Iso,
            "rvz" => DiscImageTarget.Rvz,
            "wia" => DiscImageTarget.Wia,
            "gcz" => DiscImageTarget.Gcz,
            _ => null,
        };
    }
}
