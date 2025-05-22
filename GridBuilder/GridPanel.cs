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
                    // Create a new bitmap to draw the original image onto, allowing for rotation without modifying the source image directly
                    imageToProcess = new Bitmap(originalMapImage.Width, originalMapImage.Height, PixelFormat.Format32bppArgb);
                    using (Graphics tempGraphics = Graphics.FromImage(imageToProcess))
                    {
                        tempGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic; // Ensure quality for this operation too
                        tempGraphics.DrawImage(originalMapImage, 0, 0, originalMapImage.Width, originalMapImage.Height);
                    }

                    // Apply rotation if needed
                    if (rotation != RotateFlipType.RotateNoneFlipNone)
                    {
                        imageToProcess.RotateFlip(rotation);
                    }

                    // Calculate scaled dimensions based on the (potentially rotated) imageToProcess
                    float scaledWidth = imageToProcess.Width * scale;
                    float scaledHeight = imageToProcess.Height * scale;

                    RectangleF destRect = new RectangleF(offset.X, offset.Y, scaledWidth, scaledHeight);
                    g.DrawImage(imageToProcess, destRect);
                }
                catch (Exception ex)
                {
                    // Log or handle the exception appropriately
                    Console.WriteLine($"Error processing map image for drawing: {ex.Message}");
                    // Optionally, draw an error message on the panel
                    g.Clear(Color.FromArgb(240, 240, 240)); // A light background for the error message
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("Map Error", Font, Brushes.Red, ClientRectangle, sf);
                    }
                }
                finally
                {
                    imageToProcess?.Dispose(); // Dispose the temporary bitmap
                }
            }
            else
            {
                // If no map image, clear with a default background (e.g., white)
                g.Clear(Color.White);
            }

            // 2. Draw the grid cells on top
            for (int x = 0; x < GridFile.ArenaSize; x++)
            {
                for (int y = 0; y < GridFile.ArenaSize; y++)
                {
                    int cellPixelX = x * CellSize;
                    int cellPixelY = y * CellSize;
                    Color cellColor = _ctx.CurrentGrid.GetColor(x, y); // Assuming GetColor handles 0 value as white or transparent
                    if (cellColor != Color.White) // Optimization: only draw if not default background
                    {
                        using var brush = new SolidBrush(cellColor);
                        g.FillRectangle(brush, cellPixelX + 1, cellPixelY + 1, CellSize - 1, CellSize - 1);
                    }
                    // Draw cell border
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

            // Handle left and right mouse buttons for painting/erasing
            if (e.Button is MouseButtons.Left or MouseButtons.Right)
            {
                _drag = true;
                // Erase mode if Right button OR (Left button AND Ctrl key is pressed)
                _eraseMode = (e.Button == MouseButtons.Right) ||
                             ((e.Button == MouseButtons.Left) && (ModifierKeys & Keys.Control) == Keys.Control);

                _dragStart = e.Location;
                _dragEnd = e.Location; // Initialize dragEnd to start, in case it's just a click
                // No Invalidate() here; OnMouseUp will handle the final paint.
                // If visual feedback is needed on click *before* drag, Invalidate() could be called.
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

            _dragEnd = e.Location; // Finalize drag end position
            _drag = false; // Drag operation finished

            Rectangle dragRectPixels = GetDragRectangle();

            // Convert pixel coordinates to grid cell coordinates
            // Ensure values are within grid bounds
            int startCellX = Math.Max(0, dragRectPixels.Left / CellSize);
            int startCellY = Math.Max(0, dragRectPixels.Top / CellSize);
            int endCellX = Math.Min(GridFile.ArenaSize - 1, dragRectPixels.Right / CellSize);
            int endCellY = Math.Min(GridFile.ArenaSize - 1, dragRectPixels.Bottom / CellSize);

            // Loop through all cells in the drag-selected rectangle
            for (int x = startCellX; x <= endCellX; x++)
            {
                for (int y = startCellY; y <= endCellY; y++)
                {
                    // Double-check bounds, though covered by Math.Min/Max above
                    if (x >= 0 && x < GridFile.ArenaSize && y >= 0 && y < GridFile.ArenaSize)
                    {
                        int currentValue = _ctx.CurrentGrid.GetValue(x, y);
                        int newValue;

                        if (_eraseMode) // Erase Mode (Right Click or Ctrl+Left Click)
                        {
                            newValue = 0; // Set cell value to 0 (clear all tags)
                        }
                        else // Paint Mode (Left Click)
                        {
                            if ((ModifierKeys & Keys.Shift) == Keys.Shift) // Shift + Left Click
                            {
                                // Toggle the specific bit selected in the paint slot box
                                newValue = currentValue ^ _ctx.CurrentDrawMask;
                            }
                            else // Normal Left Click
                            {
                                // MODIFIED BEHAVIOR:
                                // Add the specific bit selected in the paint slot box to the current value (Bitwise OR)
                                // This allows "painting over" to add multiple tags.
                                newValue = currentValue | _ctx.CurrentDrawMask;
                            }
                        }

                        // Only update and mark as dirty if the value actually changed
                        if (currentValue != newValue)
                        {
                            _ctx.CurrentGrid.SetValue(x, y, newValue);
                        }
                    }
                }
            }
            _eraseMode = false; // Reset erase mode after operation
            Invalidate(); // Redraw the panel to reflect changes
        }

        private Rectangle GetDragRectangle()
        {
            // Calculate the rectangle based on drag start and end points
            int x0 = Math.Min(_dragStart.X, _dragEnd.X);
            int y0 = Math.Min(_dragStart.Y, _dragEnd.Y);
            int x1 = Math.Max(_dragStart.X, _dragEnd.X);
            int y1 = Math.Max(_dragStart.Y, _dragEnd.Y);
            return new Rectangle(x0, y0, x1 - x0, y1 - y0);
        }
    }
}
