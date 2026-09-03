using ImageLoader.Common.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
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
        }
    }
}