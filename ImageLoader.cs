using System.IO;
using ImageLoader.Common.Players;
using ImageLoader.Common.Services;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ImageLoader;

public sealed class ImageLoader : Mod
{
    internal enum MessageType : byte
    {
        PlaceImage = 1,
        RequestRgbSync = 2,
        RgbSyncReset = 3,
        RgbSyncChunk = 4,
        GalleryState = 5
    }

    public static ImageLoader Instance { get; private set; }

    public static ModKeybind OpenMenuKeybind { get; private set; }

    public static ModKeybind ToggleGalleryKeybind { get; private set; }

    public static ModKeybind ZoomInKeybind { get; private set; }

    public override void Load()
    {
        Instance = this;

        if (Main.dedServ)
            return;

        OpenMenuKeybind = KeybindLoader.RegisterKeybind(
            this,
            "OpenImageLoader",
            Keys.P
        );

        ToggleGalleryKeybind = KeybindLoader.RegisterKeybind(
            this,
            "ToggleGalleryMode",
            Keys.G
        );

        ZoomInKeybind = KeybindLoader.RegisterKeybind(
            this,
            "GalleryZoomIn",
            Keys.PageUp
        );

    }

    public override void HandlePacket(
        BinaryReader reader,
        int whoAmI
    )
    {
        MessageType messageType =
            (MessageType)reader.ReadByte();

        switch (messageType)
        {
            case MessageType.PlaceImage:
                ImagePlacementService.ReceivePlacement(
                    reader,
                    whoAmI
                );
                break;

            case MessageType.RequestRgbSync:
                if (Main.netMode == NetmodeID.Server)
                {
                    RgbPixelService.SendFullSync(
                        whoAmI
                    );
                }
                break;

            case MessageType.RgbSyncReset:
                if (
                    Main.netMode
                    == NetmodeID.MultiplayerClient
                )
                {
                    RgbPixelService.ReceiveReset();
                }
                break;

            case MessageType.RgbSyncChunk:
                if (
                    Main.netMode
                    == NetmodeID.MultiplayerClient
                )
                {
                    RgbPixelService.ReceiveChunk(
                        reader
                    );
                }
                break;

            case MessageType.GalleryState:
                if (Main.netMode == NetmodeID.Server)
                {
                    GalleryPlayer.ReceiveClientState(
                        reader,
                        whoAmI
                    );
                }
                else if (
                    Main.netMode
                    == NetmodeID.MultiplayerClient
                )
                {
                    GalleryPlayer.ReceiveServerState(
                        reader
                    );
                }
                break;
        }
    }

    public override void Unload()
    {
        OpenMenuKeybind = null;
        ToggleGalleryKeybind = null;
        ZoomInKeybind = null;
        Instance = null;
    }
}
