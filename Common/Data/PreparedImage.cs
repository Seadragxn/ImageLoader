using Microsoft.Xna.Framework;

namespace ImageLoader.Common.Data;

internal sealed class PreparedImage
{
    public int Width { get; }

    public int Height { get; }

    public ImageConversionMode Mode { get; }

    public ushort[] TileTypes { get; }

    public Color[] PreviewColors { get; }

    public Color[] ExactColors { get; }

    public PreparedImage(
        int width,
        int height,
        ImageConversionMode mode,
        ushort[] tileTypes,
        Color[] previewColors,
        Color[] exactColors
    )
    {
        Width = width;
        Height = height;
        Mode = mode;
        TileTypes = tileTypes;
        PreviewColors = previewColors;
        ExactColors = exactColors;
    }
}
