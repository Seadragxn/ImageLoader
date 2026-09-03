using System.Collections.Generic;
using System.IO;
using ImageLoader.Common.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ImageLoader.Common.Items;

internal sealed class RgbPixelItem : ModItem
{
    public override string Texture =>
        "Terraria/Images/MagicPixel";

    public Color StoredColor
    {
        get;
        private set;
    } = Color.White;

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(
            ModContent.TileType<
                RgbPixelTile
            >()
        );

        Item.width = 16;

        Item.height = 16;

        Item.maxStack = 1;

        Item.value = 0;

        ApplyColor(
            StoredColor
        );
    }

    public void ApplyColor(
        Color color
    )
    {
        StoredColor =
            new Color(
                color.R,
                color.G,
                color.B,
                255
            );

        Item.color =
            StoredColor;

        Item.SetNameOverride(
            $"RGB {StoredColor.R}, {StoredColor.G}, {StoredColor.B}"
        );
    }

    public override void ModifyTooltips(
        List<TooltipLine> tooltips
    )
    {
        foreach (
            TooltipLine line
            in tooltips
        )
        {
            if (
                line.Mod == "Terraria"
                && line.Name == "ItemName"
            )
            {
                line.Text =
                    $"RGB {StoredColor.R}, {StoredColor.G}, {StoredColor.B}";

                line.OverrideColor =
                    StoredColor;

                break;
            }
        }
    }

    public override ModItem Clone(
        Item newEntity
    )
    {
        var clone =
            (RgbPixelItem)base.Clone(
                newEntity
            );

        clone.ApplyColor(
            StoredColor
        );

        return clone;
    }

    public override void SaveData(
        TagCompound tag
    )
    {
        tag["R"] =
            StoredColor.R;

        tag["G"] =
            StoredColor.G;

        tag["B"] =
            StoredColor.B;
    }

    public override void LoadData(
        TagCompound tag
    )
    {
        ApplyColor(
            new Color(
                tag.GetByte("R"),
                tag.GetByte("G"),
                tag.GetByte("B")
            )
        );
    }

    public override void NetSend(
        BinaryWriter writer
    )
    {
        writer.Write(
            StoredColor.R
        );

        writer.Write(
            StoredColor.G
        );

        writer.Write(
            StoredColor.B
        );
    }

    public override void NetReceive(
        BinaryReader reader
    )
    {
        ApplyColor(
            new Color(
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte()
            )
        );
    }
}
