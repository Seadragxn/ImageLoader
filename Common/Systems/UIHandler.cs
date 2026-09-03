using System.Collections.Generic;
using ImageLoader.Common.Data;
using ImageLoader.Common.Players;
using ImageLoader.Common.Services;
using ImageLoader.Common.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;

namespace ImageLoader.Common.Systems;

public sealed class UIHandler : ModSystem
{
    private static UserInterface
        _userInterface;

    private static MainMenu
        _menu;

    private static PreparedImage
        _preparedImage;

    private static bool
        _placing;

    public static bool IsMenuOpen =>
        _userInterface?
            .CurrentState
        is not null;

    public static bool IsPlacing =>
        _placing;

    public override void Load()
    {
        if (
            Main.dedServ
        )
        {
            return;
        }

        _userInterface =
            new UserInterface();

        _menu =
            new MainMenu();

        _menu.Activate();
    }

    public static void ToggleMenu()
    {
        if (
            _userInterface
            is null
        )
        {
            return;
        }

        if (
            _placing
        )
        {
            _placing =
                false;

            ShowMenu();

            return;
        }

        if (
            IsMenuOpen
        )
        {
            HideMenu();
        }
        else
        {
            ShowMenu();
        }
    }

    public static void ShowMenu()
    {
        if (
            _userInterface
            is null
        )
        {
            return;
        }

        _placing =
            false;

        _userInterface.SetState(
            _menu
        );
    }

    public static void HideMenu()
    {
        _userInterface?
            .SetState(
                null
            );
    }

    internal static void SetPreparedImage(
        PreparedImage image
    )
    {
        _preparedImage =
            image;

        if (
            image is null
        )
        {
            _placing =
                false;
        }
    }

    public static void BeginPlacement()
    {
        if (
            _preparedImage
            is null
        )
        {
            return;
        }

        HideMenu();

        PlayerInput.WritingText =
            false;

        Main.blockInput =
            false;

        Main.clrInput();

        _placing =
            true;

        Main.NewText(
            "Move the image with the mouse. Left-click to place; right-click or Escape to cancel.",
            Color.LightBlue
        );
    }

    public static void HandlePlacementInput(
        Player player
    )
    {
        if (
            !_placing
            || _preparedImage is null
            || Main.gameMenu
        )
        {
            return;
        }

        player.mouseInterface =
            true;

        player.controlUseItem =
            false;

        player.controlUseTile =
            false;

        bool escapePressed =
            Main.keyState.IsKeyDown(
                Keys.Escape
            )
            && Main.oldKeyState.IsKeyUp(
                Keys.Escape
            );

        if (
            escapePressed

            || (
                Main.mouseRight
                && Main.mouseRightRelease
            )
        )
        {
            _placing =
                false;

            ShowMenu();

            Main.NewText(
                "Image placement cancelled.",
                Color.LightGray
            );

            return;
        }

        if (
            !Main.mouseLeft
            || !Main.mouseLeftRelease
        )
        {
            return;
        }

        Point origin =
            GetPlacementOrigin();

        _placing =
            false;

        ImagePlacementService
            .RequestPlacement(
                origin.X,
                origin.Y,
                _preparedImage
            );
    }

    public override void UpdateUI(
        GameTime gameTime
    )
    {
        if (
            IsMenuOpen
        )
        {
            _userInterface.Update(
                gameTime
            );
        }
    }

    public override void PostUpdateInput()
    {
        if (Main.dedServ)
            return;

        if (
            !Main.gameMenu
            && !PlayerInput.WritingText
            && !Main.drawingPlayerChat
            && MenuKeyJustPressed()
        )
        {
            ToggleMenu();
        }

        if (
            _placing
            && !Main.gameMenu
        )
        {
            HandlePlacementInput(
                Main.LocalPlayer
            );
        }
    }

    private static bool MenuKeyJustPressed()
    {
        bool keybindPressed =
            false;

        try
        {
            keybindPressed =
                global::ImageLoader.ImageLoader
                    .OpenMenuKeybind?
                    .JustPressed
                == true;
        }
        catch (
            KeyNotFoundException
        )
        {
            // A stale tModLoader input profile can briefly omit a newly renamed keybind.
        }

        bool physicalPPressed =
            Main.keyState.IsKeyDown(
                Keys.P
            )
            && Main.oldKeyState.IsKeyUp(
                Keys.P
            );

        return keybindPressed
            || physicalPPressed;
    }

    public override void ModifyInterfaceLayers(
        List<GameInterfaceLayer> layers
    )
    {
        int mouseTextIndex =
            layers.FindIndex(
                layer =>
                    layer.Name
                    == "Vanilla: Mouse Text"
            );

        if (
            mouseTextIndex < 0
        )
        {
            return;
        }

        layers.Insert(
            mouseTextIndex,

            new LegacyGameInterfaceLayer(
                "ImageLoader: Placement Preview",

                () =>
                {
                    if (
                        _placing
                    )
                    {
                        DrawPlacementPreview(
                            Main.spriteBatch
                        );
                    }

                    return true;
                },

                InterfaceScaleType.Game
            )
        );

        layers.Insert(
            mouseTextIndex + 1,

            new LegacyGameInterfaceLayer(
                "ImageLoader: Gallery Zoom HUD",

                () =>
                {
                    DrawGalleryZoomHud(
                        Main.spriteBatch
                    );

                    return true;
                },

                InterfaceScaleType.UI
            )
        );

        layers.Insert(
            mouseTextIndex + 2,

            new LegacyGameInterfaceLayer(
                "ImageLoader: Menu",

                () =>
                {
                    if (
                        IsMenuOpen
                    )
                    {
                        _userInterface.Draw(
                            Main.spriteBatch,
                            new GameTime()
                        );
                    }

                    return true;
                },

                InterfaceScaleType.UI
            )
        );
    }

    private static void DrawGalleryZoomHud(
        SpriteBatch spriteBatch
    )
    {
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

        string text =
            $"Gallery Zoom: {gallery.GalleryZoom:P0}  |  Ctrl + Scroll";

        Vector2 textSize =
            FontAssets
                .MouseText
                .Value
                .MeasureString(
                    text
                );

        float uiHeight =
            Main.screenHeight
            / Main.UIScale;

        var position =
            new Vector2(
                24f,
                uiHeight
                    - textSize.Y
                    - 28f
            );

        var background =
            new Rectangle(
                (int)position.X - 8,
                (int)position.Y - 5,
                (int)textSize.X + 16,
                (int)textSize.Y + 10
            );

        spriteBatch.Draw(
            TextureAssets
                .MagicPixel
                .Value,
            background,
            new Color(
                15,
                22,
                38,
                205
            )
        );

        Utils.DrawBorderString(
            spriteBatch,
            text,
            position,
            Color.LightBlue
        );
    }

    private static Point GetPlacementOrigin()
    {
        Point mouseTile =
            Main.MouseWorld
                .ToTileCoordinates();

        return new Point(
            mouseTile.X
                - _preparedImage.Width
                / 2,

            mouseTile.Y
                - _preparedImage.Height
                / 2
        );
    }

    private static void DrawPlacementPreview(
        SpriteBatch spriteBatch
    )
    {
        if (
            _preparedImage
            is null
        )
        {
            return;
        }

        Point origin =
            GetPlacementOrigin();

        var screenBounds =
            new Rectangle(
                0,
                0,
                Main.screenWidth,
                Main.screenHeight
            );

        Texture2D pixel =
            TextureAssets
                .MagicPixel
                .Value;

        for (
            int y = 0;
            y < _preparedImage.Height;
            y++
        )
        {
            for (
                int x = 0;
                x < _preparedImage.Width;
                x++
            )
            {
                Color color =
                    _preparedImage
                        .PreviewColors[
                            y
                            * _preparedImage.Width
                            + x
                        ];

                if (
                    color.A == 0
                )
                {
                    continue;
                }

                var destination =
                    new Rectangle(
                        (
                            origin.X
                            + x
                        )
                        * 16
                        - (int)Main
                            .screenPosition
                            .X,

                        (
                            origin.Y
                            + y
                        )
                        * 16
                        - (int)Main
                            .screenPosition
                            .Y,

                        16,
                        16
                    );

                if (
                    destination.Intersects(
                        screenBounds
                    )
                )
                {
                    spriteBatch.Draw(
                        pixel,
                        destination,
                        color * 0.72f
                    );
                }
            }
        }

        var border =
            new Rectangle(
                origin.X
                    * 16
                    - (int)Main
                        .screenPosition
                        .X,

                origin.Y
                    * 16
                    - (int)Main
                        .screenPosition
                        .Y,

                _preparedImage.Width
                    * 16,

                _preparedImage.Height
                    * 16
            );

        DrawBorder(
            spriteBatch,
            pixel,
            border,
            Color.Yellow,
            2
        );
    }

    private static void DrawBorder(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle rectangle,
        Color color,
        int thickness
    )
    {
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                rectangle.Left,
                rectangle.Top,
                rectangle.Width,
                thickness
            ),
            color
        );

        spriteBatch.Draw(
            pixel,
            new Rectangle(
                rectangle.Left,
                rectangle.Bottom
                    - thickness,
                rectangle.Width,
                thickness
            ),
            color
        );

        spriteBatch.Draw(
            pixel,
            new Rectangle(
                rectangle.Left,
                rectangle.Top,
                thickness,
                rectangle.Height
            ),
            color
        );

        spriteBatch.Draw(
            pixel,
            new Rectangle(
                rectangle.Right
                    - thickness,
                rectangle.Top,
                thickness,
                rectangle.Height
            ),
            color
        );
    }

    public override void Unload()
    {
        _menu?.Dispose();

        _menu =
            null;

        _userInterface =
            null;

        _preparedImage =
            null;

        _placing =
            false;

        ImagePalette.Unload();

    }
}
