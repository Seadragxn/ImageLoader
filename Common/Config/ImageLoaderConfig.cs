using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace ImageLoader.Common.Config;

public enum ImageLoaderWorldGenerationMode
{
    Vanilla,
    VoidGallery
}

public sealed class ImageLoaderConfig : ModConfig
{
    public override ConfigScope Mode =>
        ConfigScope.ServerSide;

    [DefaultValue(true)]
    public bool EnableExactRgbBlocks { get; set; } =
        true;

    [DefaultValue(true)]
    public bool EnableGalleryMode { get; set; } =
        true;

    [DefaultValue(
        ImageLoaderWorldGenerationMode.Vanilla
    )]
    public ImageLoaderWorldGenerationMode
        WorldGenerationMode { get; set; } =
        ImageLoaderWorldGenerationMode.Vanilla;
}