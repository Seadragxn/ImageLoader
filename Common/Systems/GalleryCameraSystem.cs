using System;
using ImageLoader.Common.Players;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics;
using Terraria.ModLoader;

namespace ImageLoader.Common.Systems;

public sealed class GalleryCameraSystem : ModSystem
{
    private const int VanillaLogicWidth =
        1920;

    private const int VanillaLogicHeight =
        1200;

    private bool _expandedRenderBounds;

    private int _previousLogicWidth;

    private int _previousLogicHeight;

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
            RestoreRenderBounds();

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
            RestoreRenderBounds();

            return;
        }

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

        // Terraria uses a separate recommended-zoom context and logic
        // rectangle for deciding what world content is worth processing.
        // Keep both in step with the Gallery transform so zooming out grows
        // the active/drawn area instead of merely shrinking a normal-sized
        // render, and zooming back in naturally contracts it again.
        Main.SetRecommendedZoomContext(
            transform.TransformationMatrix
        );

        float inverseZoom =
            1f
            / Math.Max(
                zoom,
                GalleryPlayer.MinimumGalleryZoom
            );

        if (
            !_expandedRenderBounds
        )
        {
            _previousLogicWidth =
                Main.LogicCheckScreenWidth;

            _previousLogicHeight =
                Main.LogicCheckScreenHeight;

            _expandedRenderBounds =
                true;
        }

        Main.LogicCheckScreenWidth =
            Math.Max(
                Math.Max(
                    VanillaLogicWidth,
                    _previousLogicWidth
                ),
                (int)Math.Ceiling(
                    Main.screenWidth
                    * inverseZoom
                )
            );

        Main.LogicCheckScreenHeight =
            Math.Max(
                Math.Max(
                    VanillaLogicHeight,
                    _previousLogicHeight
                ),
                (int)Math.Ceiling(
                    Main.screenHeight
                    * inverseZoom
                )
            );
    }

    public override void Unload()
    {
        RestoreRenderBounds();
    }

    private void RestoreRenderBounds()
    {
        if (
            !_expandedRenderBounds
        )
        {
            return;
        }

        Main.LogicCheckScreenWidth =
            _previousLogicWidth;

        Main.LogicCheckScreenHeight =
            _previousLogicHeight;

        _expandedRenderBounds =
            false;
    }
}
