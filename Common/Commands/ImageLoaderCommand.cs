using ImageLoader.Common.Systems;
using Terraria.ModLoader;

namespace ImageLoader.Common.Commands;

public sealed class ImageLoaderCommand : ModCommand
{
    public override CommandType Type => CommandType.Chat;
    public override string Command => "imageloader";
    public override string Usage => "/imageloader";
    public override string Description => "Open the Image Loader menu.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        UIHandler.ToggleMenu();
    }
}
