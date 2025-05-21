using System.Drawing; // Required for Point and Image
using System.Drawing.Drawing2D; // Required for RotateFlipType

namespace ModdingGUI
{
    /// <summary>
    /// Defines the contract for a context that provides GridFile data,
    /// the current painting mask, friendly names for bits, and map display parameters.
    /// </summary>
    public interface IGridContext
    {
        /// <summary>
        /// Gets the currently active GridFile.
        /// </summary>
        GridFile CurrentGrid { get; }

        /// <summary>
        /// Gets the current bit mask used for painting operations.
        /// </summary>
        int CurrentDrawMask { get; }

        /// <summary>
        /// Gets the friendly display name for a given bit index from the paint slot selector.
        /// </summary>
        /// <param name="bitIndex">The bit index (0-29) for which to get the name.</param>
        /// <returns>The friendly name of the tag associated with the bit index.</returns>
        string GetFriendlyNameForBit(int bitIndex);

        /// <summary>
        /// Gets the current background map image for the arena.
        /// </summary>
        Image? CurrentMapArenaImage { get; }

        /// <summary>
        /// Gets the current display offset (X, Y) for the map image.
        /// </summary>
        Point MapDisplayOffset { get; }

        /// <summary>
        /// Gets the current display scale factor for the map image.
        /// </summary>
        float MapDisplayScale { get; }

        /// <summary>
        /// Gets the current display rotation for the map image.
        /// Expected values correspond to 0, 90, 180, 270 degrees.
        /// </summary>
        RotateFlipType MapDisplayRotation { get; }
    }
}
