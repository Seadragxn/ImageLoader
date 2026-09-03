using ImageLoader.Common.Items;
using ImageLoader.Common.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ImageLoader.Common.Tiles;

internal sealed class RgbPixelTile : ModTile
{
    public override string Texture =>
        "Terraria/Images/MagicPixel";

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileBlockLight[Type] = true;
        Main.tileFrameImportant[Type] = false;
        Main.tileNoAttach[Type] = false;

        AddMapEntry(
            new Color(
                128,
                128,
                128
            )
        );
    }

    public override bool PreDraw(
        int i,
        int j,
        SpriteBatch spriteBatch
    )
    {
        Color color;

        if (
            !RgbPixelService.TryGetColor(
                i,
                j,
                out color
            )
        )
        {
            color = Color.Magenta;
        }

        Vector2 offScreenOffset =
            Main.drawToScreen
                ? Vector2.Zero
                : new Vector2(
                    Main.offScreenRange,
                    Main.offScreenRange
                );

        Vector2 position =
            new Vector2(
                i * 16,
                j * 16
            )
            - Main.screenPosition
            + offScreenOffset;

        spriteBatch.Draw(
            TextureAssets.MagicPixel.Value,
            new Rectangle(
                (int)position.X,
                (int)position.Y,
                16,
                16
            ),
            color
        );

        return false;
    }

    public override void KillTile(
        int i,
        int j,
        ref bool fail,
        ref bool effectOnly,
        ref bool noItem
    )
    {
        Color storedColor =
            Color.White;

        bool shouldDrop =
            !noItem
            && !fail
            && !effectOnly
            && RgbPixelService.TryGetColor(
                i,
                j,
                out storedColor
            );

        noItem = true;

        if (
            !fail
            && !effectOnly
        )
        {
            RgbPixelService.RemoveColor(
                i,
                j
            );

            if (
                shouldDrop
                && Main.netMode
                    != NetmodeID
                        .MultiplayerClient
            )
            {
                int itemIndex =
                    Item.NewItem(
                        new EntitySource_TileBreak(
                            i,
                            j
                        ),
                        i * 16,
                        j * 16,
                        16,
                        16,
                        ModContent.ItemType<
                            RgbPixelItem
                        >()
                    );

                if (
                    itemIndex >= 0
                    && itemIndex < Main.maxItems
                    && Main.item[itemIndex]
                        .ModItem
                        is RgbPixelItem item
                )
                {
                    item.ApplyColor(
                        storedColor
                    );

                    if (
                        Main.netMode
                        == NetmodeID.Server
                    )
                    {
                        NetMessage.SendData(
                            MessageID.SyncItem,
                            number: itemIndex
                        );
                    }
                }
            }
        }
    }

    public override void PlaceInWorld(
        int i,
        int j,
        Item item
    )
    {
        if (
            item.ModItem
            is not RgbPixelItem rgbItem
        )
        {
            return;
        }

        Color color =
            rgbItem.StoredColor;

        RgbPixelService.SetColor(
            i,
            j,
            color
        );

        if (
            Main.netMode
            == NetmodeID.Server
        )
        {
            RgbPixelService.BroadcastRegion(
                i,
                j,
                1,
                1,
                new[]
                {
                    color
                }
            );
        }
    }
}
