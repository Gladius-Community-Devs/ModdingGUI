using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Drawing.Imaging; // Required for PixelFormat

namespace ModdingGUI
{
    public partial class GridPanel : UserControl
    {
        public const int CellSize = 20;
        private IGridContext? _ctx;
        private bool _drag = false;
        private bool _eraseMode = false;
        private Point _dragStart;
        private Point _dragEnd;

        /// <summary>
        /// Gets the current grid cell coordinates under the mouse cursor.
        /// X and Y are -1 if the context is not available or mouse is outside.
        /// </summary>
        public Point CurrentMouseGridCell { get; private set; } = new Point(-1, -1);

        public GridPanel()
        {
            InitializeComponent();
            DoubleBuffered = true;
            this.Size = new Size(CellSize * GridFile.ArenaSize, CellSize * GridFile.ArenaSize);
            this.BackColor = Color.White;

            this.MouseDown += OnMouseDown;
            this.MouseMove += OnMouseMove; // Subscribing to the panel's own MouseMove event
            this.MouseUp += OnMouseUp;
        }

        public void Initialise(IGridContext context)
        {
            _ctx = context;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_ctx == null || _ctx.CurrentGrid == null)
            {
                e.Graphics.Clear(Color.LightGray);
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    e.Graphics.DrawString("No Grid Loaded", Font, Brushes.Black, ClientRectangle, sf);
                }
                return;
            }

            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            // 1. Draw the background map image
            Image? originalMapImage = _ctx.CurrentMapArenaImage;
            if (originalMapImage != null)
            {
                Point offset = _ctx.MapDisplayOffset;
                float scale = _ctx.MapDisplayScale;
                RotateFlipType rotation = _ctx.MapDisplayRotation;

                if (scale <= 0) scale = 1.0f;

                Bitmap? imageToProcess = null;
                try
                {
                    imageToProcess = new Bitmap(originalMapImage.Width, originalMapImage.Height, PixelFormat.Format32bppArgb);
                    using (Graphics tempGraphics = Graphics.FromImage(imageToProcess))
                    {
                        tempGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        tempGraphics.DrawImage(originalMapImage, 0, 0, originalMapImage.Width, originalMapImage.Height);
                    }

                    if (rotation != RotateFlipType.RotateNoneFlipNone)
                    {
                        imageToProcess.RotateFlip(rotation);
                    }

                    float scaledWidth = imageToProcess.Width * scale;
                    float scaledHeight = imageToProcess.Height * scale;

                    RectangleF destRect = new RectangleF(offset.X, offset.Y, scaledWidth, scaledHeight);
                    g.DrawImage(imageToProcess, destRect);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing map image for drawing: {ex.Message}");
                    g.Clear(Color.FromArgb(240, 240, 240));
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("Map Error", Font, Brushes.Red, ClientRectangle, sf);
                    }
                }
                finally
                {
                    imageToProcess?.Dispose();
                }
            }
            else
            {
                g.Clear(Color.White);
            }

            // 2. Draw the grid cells on top
            for (int x = 0; x < GridFile.ArenaSize; x++)
            {
                for (int y = 0; y < GridFile.ArenaSize; y++)
                {
                    int cellPixelX = x * CellSize;
                    int cellPixelY = y * CellSize;
                    Color cellColor = _ctx.CurrentGrid.GetColor(x, y);
                    if (cellColor != Color.White)
                    {
                        using var brush = new SolidBrush(cellColor);
                        g.FillRectangle(brush, cellPixelX + 1, cellPixelY + 1, CellSize - 1, CellSize - 1);
                    }
                    g.DrawRectangle(Pens.Black, cellPixelX, cellPixelY, CellSize, CellSize);
                }
            }

            // 3. Draw the drag selection rectangle
            if (_drag)
            {
                using var dashPen = new Pen(Color.DarkBlue) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(dashPen, GetDragRectangle());
            }
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (_ctx?.CurrentGrid == null) return;

            if (e.Button is MouseButtons.Left or MouseButtons.Right)
            {
                _drag = true;
                _eraseMode = (e.Button == MouseButtons.Right) ||
                             ((e.Button == MouseButtons.Left) && (ModifierKeys & Keys.Control) == Keys.Control);

                _dragStart = e.Location;
                _dragEnd = e.Location;
                // No Invalidate() here; OnMouseUp will handle the final paint.
                // If visual feedback is needed on click before drag, Invalidate() could be called.
            }
        }

        // This is the GridPanel's own handler for its MouseMove event.
        // It's primarily used for updating the drag rectangle.
        // The public MouseMove event is still raised by the base Control class,
        // which GridBuilderView subscribes to for updating the hover label.
        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            // Update the publicly accessible current mouse grid cell
            CurrentMouseGridCell = new Point(e.X / CellSize, e.Y / CellSize);

            // Logic for handling drag operations
            if (_drag && _ctx?.CurrentGrid != null)
            {
                _dragEnd = e.Location;
                Invalidate(); // Redraw to show the drag rectangle moving
            }
            // Note: We don't Invalidate() solely for CurrentMouseGridCell update,
            // as that would cause constant repaints. GridBuilderView handles hover text.
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            if (!_drag || _ctx?.CurrentGrid == null) return;

            _dragEnd = e.Location;
            _drag = false;

            Rectangle dragRectPixels = GetDragRectangle();

            int startCellX = Math.Max(0, dragRectPixels.Left / CellSize);
            int startCellY = Math.Max(0, dragRectPixels.Top / CellSize);
            int endCellX = Math.Min(GridFile.ArenaSize - 1, dragRectPixels.Right / CellSize);
            int endCellY = Math.Min(GridFile.ArenaSize - 1, dragRectPixels.Bottom / CellSize);

            int valueToPaint;
            if (_eraseMode)
            {
                valueToPaint = 0;
            }
            else
            {
                valueToPaint = _ctx.CurrentDrawMask;
            }

            for (int x = startCellX; x <= endCellX; x++)
            {
                for (int y = startCellY; y <= endCellY; y++)
                {
                    if (x >= 0 && x < GridFile.ArenaSize && y >= 0 && y < GridFile.ArenaSize)
                    {
                        int currentValue = _ctx.CurrentGrid.GetValue(x, y);
                        int newValue;

                        if (_eraseMode)
                        {
                            newValue = 0;
                        }
                        else
                        {
                            if ((ModifierKeys & Keys.Shift) == Keys.Shift)
                            {
                                newValue = currentValue ^ _ctx.CurrentDrawMask;
                            }
                            else
                            {
                                newValue = _ctx.CurrentDrawMask;
                            }
                        }

                        if (currentValue != newValue)
                        {
                            _ctx.CurrentGrid.SetValue(x, y, newValue);
                        }
                    }
                }
            }
            _eraseMode = false;
            Invalidate();
        }

        private Rectangle GetDragRectangle()
        {
            int x0 = Math.Min(_dragStart.X, _dragEnd.X);
            int y0 = Math.Min(_dragStart.Y, _dragEnd.Y);
            int x1 = Math.Max(_dragStart.X, _dragEnd.X);
            int y1 = Math.Max(_dragStart.Y, _dragEnd.Y);
            return new Rectangle(x0, y0, x1 - x0, y1 - y0);
        }
    }
}