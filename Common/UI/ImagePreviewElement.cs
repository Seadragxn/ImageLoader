using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;

namespace ImageLoader.Common.UI;

internal sealed class ImagePreviewElement : UIElement, IDisposable
{
    private const int CheckerSize = 12;

    private static readonly Color CheckerLight =
        new(224, 224, 224);

    private static readonly Color CheckerDark =
        new(158, 158, 158);

    private Texture2D _texture;

    public void SetTexture(Texture2D texture)
    {
        Texture2D previousTexture =
            _texture;

        _texture = texture;

        DisposeTextureSafely(
            previousTexture
        );
    }

    public void SetPixels(Color[] colors, int width, int height)
    {
        var texture = new Texture2D(Main.instance.GraphicsDevice, width, height);
        texture.SetData(colors);
        SetTexture(texture);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);

        CalculatedStyle dimensions = GetInnerDimensions();
        var bounds = new Rectangle((int)dimensions.X, (int)dimensions.Y, (int)dimensions.Width, (int)dimensions.Height);
        Texture2D pixel =
            Terraria.GameContent.TextureAssets.MagicPixel.Value;

        spriteBatch.Draw(
            pixel,
            bounds,
            new Color(15, 18, 28, 220)
        );

        if (_texture is null || _texture.IsDisposed)
            return;

        float scale = Math.Min(dimensions.Width / _texture.Width, dimensions.Height / _texture.Height);
        int drawWidth = Math.Max(1, (int)(_texture.Width * scale));
        int drawHeight = Math.Max(1, (int)(_texture.Height * scale));
        int drawX = (int)(dimensions.X + (dimensions.Width - drawWidth) * 0.5f);
        int drawY = (int)(dimensions.Y + (dimensions.Height - drawHeight) * 0.5f);

        var imageBounds =
            new Rectangle(
                drawX,
                drawY,
                drawWidth,
                drawHeight
            );

        DrawCheckerboard(
            spriteBatch,
            pixel,
            imageBounds
        );

        spriteBatch.Draw(
            _texture,
            imageBounds,
            Color.White
        );

        DrawBorder(
            spriteBatch,
            pixel,
            imageBounds,
            new Color(90, 104, 132)
        );
    }

    private static void DrawCheckerboard(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds
    )
    {
        for (
            int y = bounds.Top;
            y < bounds.Bottom;
            y += CheckerSize
        )
        {
            for (
                int x = bounds.Left;
                x < bounds.Right;
                x += CheckerSize
            )
            {
                int column =
                    (x - bounds.Left)
                    / CheckerSize;

                int row =
                    (y - bounds.Top)
                    / CheckerSize;

                Color color =
                    ((column + row) & 1) == 0
                        ? CheckerLight
                        : CheckerDark;

                spriteBatch.Draw(
                    pixel,
                    new Rectangle(
                        x,
                        y,
                        Math.Min(
                            CheckerSize,
                            bounds.Right - x
                        ),
                        Math.Min(
                            CheckerSize,
                            bounds.Bottom - y
                        )
                    ),
                    color
                );
            }
        }
    }

    private static void DrawBorder(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color color
    )
    {
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Left,
                bounds.Top,
                bounds.Width,
                1
            ),
            color
        );

        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Left,
                bounds.Bottom - 1,
                bounds.Width,
                1
            ),
            color
        );

        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Left,
                bounds.Top,
                1,
                bounds.Height
            ),
            color
        );

        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Right - 1,
                bounds.Top,
                1,
                bounds.Height
            ),
            color
        );
    }

    public void Dispose()
    {
        Texture2D texture =
            _texture;

        _texture = null;

        DisposeTextureSafely(
            texture
        );
    }

    private static void DisposeTextureSafely(
        Texture2D texture
    )
    {
        if (
            texture is null
            || texture.IsDisposed
        )
        {
            return;
        }

        try
        {
            texture.Dispose();
        }
        catch (
            System.Threading.ThreadStateException
        )
        {
            Main.QueueMainThreadAction(
                () =>
                {
                    if (
                        !texture.IsDisposed
                    )
                    {
                        texture.Dispose();
                    }
                }
            );
        }
    }
}
