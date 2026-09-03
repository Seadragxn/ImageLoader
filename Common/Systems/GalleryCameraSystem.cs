using ImageLoader.Common.Players;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics;
using Terraria.ModLoader;

namespace ImageLoader.Common.Systems;

public sealed class GalleryCameraSystem : ModSystem
{
    public override void ModifyTransformMatrix(
        ref SpriteViewMatrix transform
    )
    {
        if (
            Main.dedServ
            || Main.gameMenu
            || Main.myPlayer < 0
            || Main.myPlayer >= Main.maxPlayers
        )
        {
            return;
        }

        GalleryPlayer gallery =
            Main.LocalPlayer
                .GetModPlayer<
                    GalleryPlayer
                >();

        if (
            !gallery.GalleryMode
        )
        {
            return;
        }

        // Gallery zoom is deliberately limited to 100% and closer. Terraria
        // does not reliably render world content outside its normal viewport,
        // so Image Loader no longer offers a misleading zoom-out view.
        float zoom =
            MathHelper.Clamp(
                gallery.GalleryZoom,
                GalleryPlayer.MinimumGalleryZoom,
                GalleryPlayer.MaximumGalleryZoom
            );

        transform.Zoom =
            new Vector2(
                Main.ForcedMinimumZoom
                * zoom
            );
    }
}
