namespace ModdingGUI
{
    using ModdingGUI.Core.Discs;
    using ModdingGUI.Core.Logging;

    /// <summary>
    /// Shell side of disc-image widening: build a preparer from the settings and turn any supported image into a plain ISO.
    /// </summary>
    /// <remarks>
    /// The tool chain can only read a plain ISO (<c>ngciso-tool.py</c> seeks raw offsets and opens the file for writing),
    /// and xdelta needs real disc bytes too. Rather than teach every step about RVZ/WIA/GCZ/NKit, the shell converts once
    /// up front and deletes the temporary ISO afterwards. Nothing is bundled: the converter path is a setting, guessed
    /// from the Dolphin executable the user already configured when possible (see <see cref="DiscImagePreparer"/> for why).
    /// </remarks>
    public partial class frmMain
    {
        /// <summary>The converter to use: the configured path, or DolphinTool.exe beside the configured Dolphin.</summary>
        private string? ResolveDiscImageConverter()
        {
            string configured = appSettings?.DiscImageConverterPath ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

            return DiscImagePreparer.GuessConverterFrom(appSettings?.DolphinExecutablePath);
        }

        /// <summary>Build a preparer from the current settings.</summary>
        private DiscImagePreparer CreateDiscImagePreparer()
        {
            string? converter = ResolveDiscImageConverter();

            if (converter is not null && !string.Equals(converter, appSettings?.DiscImageConverterPath, StringComparison.Ordinal))
            {
                AppendLog($"Using '{converter}' as the disc image converter (found beside the configured Dolphin).",
                    InfoColor, rtbPatchingApplicationOutput);
            }

            // NKit's converter is a separate, closed-source tool; it is only used when the user points at it.
            return new DiscImagePreparer(converter, nkitConverterPath: null, _log);
        }

        /// <summary>
        /// Ask for a plain ISO, logging what happened. The caller owns the result and must clean it up when temporary.
        /// </summary>
        private DiscImagePreparation PrepareDiscImage(string imagePath, RichTextBox? target = null)
        {
            AppendLog($"Checking '{Path.GetFileName(imagePath)}'...", InfoColor, target);

            DiscImagePreparation preparation = CreateDiscImagePreparer().Prepare(imagePath);

            AppendLog(preparation.Message, preparation.Ok ? SuccessColor : ErrorColor, target);
            return preparation;
        }
    }
}
