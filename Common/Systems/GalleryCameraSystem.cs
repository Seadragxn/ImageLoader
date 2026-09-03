using System;
using System.Reflection;
using ImageLoader.Common.Players;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.GameContent.Drawing;
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

    private Hook _screenDrawAreaHook;

    private delegate void OriginalGetScreenDrawArea(
        TileDrawing self,
        Vector2 screenPosition,
        Vector2 offscreenOffset,
        out int firstTileX,
        out int lastTileX,
        out int firstTileY,
        out int lastTileY
    );

    private delegate void HookGetScreenDrawArea(
        OriginalGetScreenDrawArea original,
        TileDrawing self,
        Vector2 screenPosition,
        Vector2 offscreenOffset,
        out int firstTileX,
        out int lastTileX,
        out int firstTileY,
        out int lastTileY
    );

    public override void Load()
    {
        if (
            Main.dedServ
        )
        {
            return;
        }

        MethodInfo method =
            typeof(TileDrawing)
                .GetMethod(
                    "GetScreenDrawArea",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic
                    | BindingFlags.Public
                );

        if (
            method is null
        )
        {
            Mod.Logger.Warn(
                "Gallery Mode could not expand Terraria's tile draw area on this tModLoader build."
            );

            return;
        }

        try
        {
            _screenDrawAreaHook =
                new Hook(
                    method,
                    (HookGetScreenDrawArea)
                        ExpandScreenDrawArea
                );
        }
        catch (
            Exception exception
        )
        {
            // Camera expansion is optional. A tModLoader internal signature
            // change must never prevent the rest of Image Loader from loading
            // or unloading cleanly.
            Mod.Logger.Warn(
                $"Gallery Mode could not hook Terraria's tile draw area: {exception.Message}"
            );

            _screenDrawAreaHook =
                null;
        }
    }

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

        _screenDrawAreaHook?
            .Dispose();

        _screenDrawAreaHook =
            null;
    }

    private static void ExpandScreenDrawArea(
        OriginalGetScreenDrawArea original,
        TileDrawing self,
        Vector2 screenPosition,
        Vector2 offscreenOffset,
        out int firstTileX,
        out int lastTileX,
        out int firstTileY,
        out int lastTileY
    )
    {
        original(
            self,
            screenPosition,
            offscreenOffset,
            out firstTileX,
            out lastTileX,
            out firstTileY,
            out lastTileY
        );

        if (
            Main.gameMenu
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

        float effectiveZoom =
            Math.Max(
                0.05f,
                Main.ForcedMinimumZoom
                * gallery.GalleryZoom
            );

        int desiredWidth =
            (int)Math.Ceiling(
                Main.screenWidth
                / 16f
                / effectiveZoom
            )
            + 12;

        int desiredHeight =
            (int)Math.Ceiling(
                Main.screenHeight
                / 16f
                / effectiveZoom
            )
            + 12;

        ExpandBounds(
            ref firstTileX,
            ref lastTileX,
            desiredWidth,
            Main.maxTilesX
        );

        ExpandBounds(
            ref firstTileY,
            ref lastTileY,
            desiredHeight,
            Main.maxTilesY
        );
    }

    private static void ExpandBounds(
        ref int first,
        ref int last,
        int desiredSize,
        int worldSize
    )
    {
        int currentSize =
            last - first;

        if (
            currentSize >= desiredSize
        )
        {
            return;
        }

        int extra =
            desiredSize
            - currentSize;

        first -=
            extra / 2;

        last +=
            extra
            - extra / 2;

        if (
            first < 1
        )
        {
            last +=
                1 - first;

            first = 1;
        }

        int maximum =
            worldSize - 2;

        if (
            last > maximum
        )
        {
            first -=
                last - maximum;

            last =
                maximum;

            first =
                Math.Max(
                    1,
                    first
                );
        }
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
