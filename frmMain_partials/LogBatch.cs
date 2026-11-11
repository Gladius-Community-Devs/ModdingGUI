namespace ModdingGUI
{
    using System.Collections.Concurrent;
    using System.Drawing;

    /// <summary>
    /// Coalesces log lines into periodic RichTextBox appends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The toolchain is chatty: a single pack prints ~33,000 lines (one per archive entry). Rendering each line as it
    /// arrives costs an <see cref="Control.Invoke(System.Delegate)"/>, a colour change, an <c>AppendText</c> and a
    /// <c>ScrollToCaret</c> - and each of those forces the RichTextBox to re-layout and repaint. Measured on this machine:
    /// the same pack took <b>43 s</b> with its output going to a file and <b>171 s</b> with its output going through the
    /// panel, i.e. the log plumbing cost more than the work.
    /// </para>
    /// <para>
    /// So lines are queued from any thread and drained on the UI thread by a timer, appending in colour runs with a single
    /// scroll per flush. The authoritative log stream (<c>IAppLog</c>) is untouched - the panel is a view, and
    /// <c>RefreshLogPanes</c> can re-render it from <c>Tail</c> whenever the theme changes - so nothing is lost by
    /// repainting less often. The pane also trims itself, because a 33,000-line control stays slow for every later append.
    /// </para>
    /// </remarks>
    public partial class frmMain
    {
        /// <summary>Lines kept in a log pane before the oldest are dropped.</summary>
        private const int LogPaneLineLimit = 4000;

        private readonly ConcurrentQueue<(RichTextBox Pane, string Text, Color Colour)> _logQueue = new();

        private System.Windows.Forms.Timer? _logFlushTimer;
        private int _logTimerRequested;

        /// <summary>Queue one line for the next flush, from any thread.</summary>
        private void QueueLogLine(RichTextBox pane, string text, Color colour)
        {
            ArgumentNullException.ThrowIfNull(pane);

            _logQueue.Enqueue((pane, text ?? string.Empty, colour));

            // Only the first line of a burst needs to hop to the UI thread.
            if (Interlocked.Exchange(ref _logTimerRequested, 1) != 0) return;

            if (pane.IsDisposed) return;

            try
            {
                if (pane.InvokeRequired) pane.BeginInvoke(new Action(StartLogFlushTimer));
                else StartLogFlushTimer();
            }
            catch (InvalidOperationException)
            {
                // Handle destroyed between the check and the call: nothing to render into.
            }
        }

        private void StartLogFlushTimer()
        {
            if (IsDisposed) return;

            if (_logFlushTimer is null)
            {
                _logFlushTimer = new System.Windows.Forms.Timer { Interval = 120 };
                _logFlushTimer.Tick += (_, _) => FlushLogQueue();
            }

            if (!_logFlushTimer.Enabled) _logFlushTimer.Start();
        }

        /// <summary>Drain the queue into the panes. UI thread only.</summary>
        private void FlushLogQueue()
        {
            try
            {
                if (_logQueue.IsEmpty)
                {
                    _logFlushTimer?.Stop();
                    Interlocked.Exchange(ref _logTimerRequested, 0);
                    return;
                }

                var byPane = new Dictionary<RichTextBox, List<(string Text, Color Colour)>>();

                while (_logQueue.TryDequeue(out (RichTextBox Pane, string Text, Color Colour) entry))
                {
                    if (entry.Pane is null || entry.Pane.IsDisposed || !entry.Pane.IsHandleCreated) continue;

                    if (!byPane.TryGetValue(entry.Pane, out List<(string, Color)>? lines))
                    {
                        lines = new List<(string, Color)>();
                        byPane[entry.Pane] = lines;
                    }

                    lines.Add((entry.Text, entry.Colour));
                }

                foreach ((RichTextBox pane, List<(string Text, Color Colour)> lines) in byPane)
                {
                    AppendLinesToPane(pane, lines);
                }
            }
            finally
            {
                // Anything that arrived while this flush ran gets the next tick; if none did, stop the timer.
                if (_logQueue.IsEmpty)
                {
                    _logFlushTimer?.Stop();
                    Interlocked.Exchange(ref _logTimerRequested, 0);
                }
            }
        }

        private static void AppendLinesToPane(RichTextBox pane, List<(string Text, Color Colour)> lines)
        {
            if (lines.Count == 0) return;

            bool scrolled = pane.TextLength > 0;

            pane.SuspendLayout();
            try
            {
                // One SelectionColor change per run of identical colours rather than per line.
                int index = 0;
                while (index < lines.Count)
                {
                    Color colour = lines[index].Colour;
                    var builder = new System.Text.StringBuilder();

                    while (index < lines.Count && lines[index].Colour.ToArgb() == colour.ToArgb())
                    {
                        builder.Append(lines[index].Text).Append(Environment.NewLine);
                        index++;
                    }

                    pane.SelectionStart = pane.TextLength;
                    pane.SelectionColor = colour;
                    pane.AppendText(builder.ToString());
                }
            }
            finally
            {
                pane.ResumeLayout();
            }

            TrimPane(pane);
            if (scrolled) pane.ScrollToCaret();
        }

        /// <summary>Keep a pane bounded: an unbounded RichTextBox slows every later append and repaint.</summary>
        private static void TrimPane(RichTextBox pane)
        {
            int excessLines = pane.Lines.Length - LogPaneLineLimit;
            if (excessLines <= 0) return;

            int firstKept = pane.GetFirstCharIndexFromLine(excessLines);
            if (firstKept <= 0) return;

            string kept = pane.Text[firstKept..];
            pane.Text = kept;
        }
    }
}
