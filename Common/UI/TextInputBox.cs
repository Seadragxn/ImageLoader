using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;

namespace ImageLoader.Common.UI;

internal sealed class TextInputBox :
    UIElement
{
    private const int MaxLength =
        2048;

    private const float TextScale =
        0.82f;

    private static TextInputBox
        _activeInput;

    private readonly string
        _hint;

    private string
        _value =
            string.Empty;

    private bool
        _focused;

    public string Value
    {
        get =>
            _value;

        set
        {
            string next =
                value
                ?? string.Empty;

            if (
                next.Length
                > MaxLength
            )
            {
                next =
                    next[
                        ..MaxLength
                    ];
            }

            _value =
                next;
        }
    }

    public TextInputBox(
        string hint
    )
    {
        _hint =
            hint
            ?? string.Empty;
    }

    public override void LeftClick(
        UIMouseEvent evt
    )
    {
        base.LeftClick(
            evt
        );

        Focus();
    }

    public override void Update(
        GameTime gameTime
    )
    {
        base.Update(
            gameTime
        );

        if (
            !_focused
        )
        {
            return;
        }

        Main.LocalPlayer.mouseInterface =
            true;

        Main.blockInput =
            true;

        PlayerInput.WritingText =
            true;

        Main.instance.HandleIME();

        string next =
            Main.GetInputText(
                _value
            );

        if (
            next.Length
            > MaxLength
        )
        {
            next =
                next[
                    ..MaxLength
                ];
        }

        _value =
            next;

        bool enter =
            Main.keyState.IsKeyDown(
                Keys.Enter
            )
            && Main.oldKeyState.IsKeyUp(
                Keys.Enter
            );

        bool tab =
            Main.keyState.IsKeyDown(
                Keys.Tab
            )
            && Main.oldKeyState.IsKeyUp(
                Keys.Tab
            );

        bool escape =
            Main.keyState.IsKeyDown(
                Keys.Escape
            )
            && Main.oldKeyState.IsKeyUp(
                Keys.Escape
            );

        bool clickedElsewhere =
            Main.mouseLeft
            && Main.mouseLeftRelease
            && !ContainsPoint(
                Main.MouseScreen
            );

        if (
            enter
            || tab
            || escape
            || clickedElsewhere
        )
        {
            StopWriting();
        }
    }

    protected override void DrawSelf(
        SpriteBatch spriteBatch
    )
    {
        base.DrawSelf(
            spriteBatch
        );

        CalculatedStyle dimensions =
            GetInnerDimensions();

        string text;

        Color color;

        if (
            string.IsNullOrEmpty(
                _value
            )
            && !_focused
        )
        {
            text =
                _hint;

            color =
                Color.Gray;
        }
        else
        {
            text =
                _value;

            color =
                Color.White;
        }

        if (
            _focused
            && Main.GameUpdateCount
                % 60
                < 30
        )
        {
            text +=
                "|";
        }

        float availableWidth =
            Math.Max(
                1f,
                (float)dimensions.Width
                    - 6f
            );

        string visible =
            FitText(
                text,
                availableWidth,
                _focused
            );

        float textHeight =
            FontAssets
                .MouseText
                .Value
                .MeasureString(
                    "A"
                )
                .Y
            * TextScale;

        Vector2 position =
            new Vector2(
                (float)dimensions.X
                    + 3f,

                (float)dimensions.Y
                    + (
                        (
                            (float)dimensions.Height
                            - textHeight
                        )
                        * 0.5f
                    )
            );

        Utils.DrawBorderString(
            spriteBatch,
            visible,
            position,
            color,
            TextScale
        );
    }

    private static string FitText(
        string text,
        float maxWidth,
        bool keepEnd
    )
    {
        if (
            Measure(
                text
            )
            <= maxWidth
        )
        {
            return text;
        }

        const string ellipsis =
            "...";

        if (
            keepEnd
        )
        {
            int start =
                0;

            while (
                start
                    < text.Length

                && Measure(
                    ellipsis
                    + text[
                        start..
                    ]
                )
                    > maxWidth
            )
            {
                start++;
            }

            return ellipsis
                + text[
                    start..
                ];
        }

        int end =
            text.Length;

        while (
            end > 0

            && Measure(
                text[
                    ..end
                ]
                + ellipsis
            )
                > maxWidth
        )
        {
            end--;
        }

        return text[
            ..end
        ]
        + ellipsis;
    }

    private static float Measure(
        string text
    )
    {
        return FontAssets
            .MouseText
            .Value
            .MeasureString(
                text
            )
            .X
            * TextScale;
    }

    private void Focus()
    {
        if (
            _focused
        )
        {
            return;
        }

        _activeInput?
            .StopWriting(
                clearGlobalInput: false
            );

        _activeInput =
            this;

        _focused =
            true;

        Main.clrInput();

        PlayerInput.WritingText =
            true;

        Main.blockInput =
            true;
    }

    public void StopWriting()
    {
        StopWriting(
            clearGlobalInput: true
        );
    }

    private void StopWriting(
        bool clearGlobalInput
    )
    {
        _focused =
            false;

        if (
            _activeInput
            == this
        )
        {
            _activeInput =
                null;
        }

        if (
            clearGlobalInput
        )
        {
            PlayerInput.WritingText =
                false;

            Main.blockInput =
                false;
        }
    }
}