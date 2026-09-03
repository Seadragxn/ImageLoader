using System;
using System.Collections.Generic;
using ImageLoader.Common.Services;
using ImageLoader.Common.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace ImageLoader.Common.Map;

public sealed class RgbPixelMapLayer : ModMapLayer
{
    public override void Draw(
        ref MapOverlayDrawContext context,
        ref string text
    )
    {
        if (
            Main.dedServ
            || RgbPixelService.Count == 0
        )
        {
            return;
        }

        Texture2D pixel =
            TextureAssets.MagicPixel.Value;

        // MagicPixel is not guaranteed to be a 1 x 1 texture. Split it into
        // one-pixel frames and draw a single frame, otherwise every stored
        // colour overlaps many neighbouring map tiles and collapses the
        // image into stripes.
        SpriteFrame pixelFrame =
            new(
                (byte)Math.Min(
                    byte.MaxValue,
                    pixel.Width
                ),
                (byte)Math.Min(
                    byte.MaxValue,
                    pixel.Height
                ),
                0,
                0
            );

        float pixelScale =
            Math.Max(
                1f,
                context.MapScale
            )
            / Math.Max(
                0.0001f,
                context.DrawScale
            );

        ushort rgbTileType =
            (ushort)ModContent
                .TileType<
                    RgbPixelTile
                >();

        foreach (
            KeyValuePair<
                Point,
                Color
            > entry
            in RgbPixelService
                .EnumerateColors()
        )
        {
            Point position =
                entry.Key;

            if (
                !WorldGen.InWorld(
                    position.X,
                    position.Y,
                    1
                )
            )
            {
                continue;
            }

            Tile tile =
                Main.tile[
                    position.X,
                    position.Y
                ];

            if (
                !tile.HasTile
                || tile.TileType
                    != rgbTileType
            )
            {
                continue;
            }

            context.Draw(
                pixel,
                new Vector2(
                    position.X + 0.5f,
                    position.Y + 0.5f
                ),
                entry.Value,
                pixelFrame,
                pixelScale,
                pixelScale,
                Alignment.Center
            );
        }
    }
}
