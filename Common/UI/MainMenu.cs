using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ImageLoader.Common.Config;
using ImageLoader.Common.Data;
using ImageLoader.Common.Players;
using ImageLoader.Common.Services;
using ImageLoader.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace ImageLoader.Common.UI;

internal sealed class MainMenu :
    UIState,
    IDisposable
{
    private const int MaxDownloadBytes =
        15 * 1024 * 1024;

    private const int MaxSourcePixels =
        8 * 1024 * 1024;

    private const int MaxSourceDimension =
        8192;

    private const int PreviewMaximumWidth =
        680;

    private const int PreviewMaximumHeight =
        240;

    private static readonly HttpClient
        HttpClient =
            CreateHttpClient();

    private UIPanel
        _panel;

    private TextInputBox
        _urlInput;

    private TextInputBox
        _widthInput;

    private TextInputBox
        _heightInput;

    private UIText
        _sourceInfo;

    private UIText
        _status;

    private ImagePreviewElement
        _preview;

    private UITextPanel<string>
        _modeButton;

    private UITextPanel<string>
        _galleryButton;

    private UITextPanel<string>
        _clearUrlButton;

    private bool
        _clearUrlVisible;

    private Color[]
        _sourcePixels;

    private int
        _sourceWidth;

    private int
        _sourceHeight;

    private PreparedImage
        _preparedImage;

    private CancellationTokenSource
        _loadCancellation;

    private int
        _requestVersion;

    private ImageConversionMode
        _conversionMode =
            ImageConversionMode
                .VanillaBlocks;

    public override void OnInitialize()
    {
        _panel =
            new UIPanel
            {
                HAlign = 0.5f,

                VAlign = 0.5f,

                BackgroundColor =
                    new Color(
                        32,
                        40,
                        62,
                        245
                    ),

                BorderColor =
                    new Color(
                        91,
                        115,
                        165
                    )
            };

        _panel.Width.Set(
            720f,
            0f
        );

        _panel.Height.Set(
            740f,
            0f
        );

        _panel.SetPadding(
            20f
        );

        Append(
            _panel
        );

        CreateTitle();

        CreateUrlSection();

        CreateResolutionSection();

        CreateNotes();

        CreatePreview();

        CreateStatus();

        CreateBottomButtons();

        RefreshModeButton();

        RefreshGalleryButton();
    }

    private void CreateTitle()
    {
        var title =
            new UIText(
                "Image Loader",
                1.25f,
                true
            )
            {
                HAlign = 0.5f
            };

        title.Top.Set(
            2f,
            0f
        );

        _panel.Append(
            title
        );

        UIElement close =
            CreateButton(
                "X",
                638f,
                0f,
                42f,
                34f,

                (
                    _,
                    _
                ) =>
                    UIHandler.HideMenu()
            );

        _panel.Append(
            close
        );
    }

    private void CreateUrlSection()
    {
        AddLabel(
            "Image URL (PNG or JPEG)",
            40f,
            0f
        );

        _urlInput =
            AddInput(
                "https://example.com/image.png",
                62f,
                0f,
                410f
            );

        _panel.Append(
            CreateButton(
                "Load URL",
                426f,
                62f,
                124f,
                40f,
                LoadUrlClicked
            )
        );

        _clearUrlButton =
            CreateButton(
                "Clear URL",
                560f,
                62f,
                120f,
                40f,
                ClearUrlClicked
            );

        _sourceInfo =
            new UIText(
                "No image loaded",
                0.82f
            )
            {
                TextColor =
                    Color.Silver,

                DynamicallyScaleDownToWidth =
                    true
            };

        _sourceInfo.Top.Set(
            111f,
            0f
        );

        _sourceInfo.Width.Set(
            0f,
            1f
        );

        _panel.Append(
            _sourceInfo
        );
    }

    private void CreateResolutionSection()
    {
        AddLabel(
            "Block width",
            137f,
            0f
        );

        AddLabel(
            "Block height",
            137f,
            160f
        );

        AddLabel(
            "Conversion mode",
            137f,
            320f
        );

        _widthInput =
            AddInput(
                "Width",
                159f,
                0f,
                145f
            );

        _heightInput =
            AddInput(
                "Height",
                159f,
                160f,
                145f
            );

        _modeButton =
            CreateButton(
                "",
                320f,
                159f,
                360f,
                40f,
                ModeClicked
            );

        _panel.Append(
            _modeButton
        );

        AddLabel(
            "Aspect ratio scaling",
            214f,
            0f
        );

        _panel.Append(
            CreateButton(
                "Scale -10%",
                160f,
                203f,
                155f,
                40f,
                (
                    _,
                    _
                ) =>
                    ScaleAspect(
                        0.9d
                    )
            )
        );

        _panel.Append(
            CreateButton(
                "Scale +10%",
                325f,
                203f,
                155f,
                40f,
                (
                    _,
                    _
                ) =>
                    ScaleAspect(
                        1.1d
                    )
            )
        );

        _panel.Append(
            CreateButton(
                "Fit Source Ratio",
                490f,
                203f,
                190f,
                40f,
                (
                    _,
                    _
                ) =>
                    ResetAspectScale()
            )
        );
    }

    private void CreateNotes()
    {
        var screenshotHint =
            new UIText(
                "Transparent pixels leave existing tiles unchanged. Exact RGB blocks keep the original 24-bit colour.",
                0.72f
            )
            {
                TextColor =
                    Color.LightGray,

                DynamicallyScaleDownToWidth =
                    true
            };

        screenshotHint.Left.Set(
            0f,
            0f
        );

        screenshotHint.Top.Set(
            257f,
            0f
        );

        screenshotHint.Width.Set(
            0f,
            1f
        );

        _panel.Append(
            screenshotHint
        );

        var limit =
            new UIText(
                $"Maximum {ImagePlacementService.MaxWidth} x {ImagePlacementService.MaxHeight} ({ImagePlacementService.MaxPixels:N0} pixels). Checkerboard areas are transparent and leave tiles unchanged.",
                0.72f
            )
            {
                TextColor =
                    Color.LightGray,

                DynamicallyScaleDownToWidth =
                    true
            };

        limit.Top.Set(
            283f,
            0f
        );

        limit.Width.Set(
            0f,
            1f
        );

        _panel.Append(
            limit
        );

        var explanation =
            new UIText(
                "Vanilla matches Terraria blocks. Exact RGB stores the original 24-bit colour on Image Loader pixel blocks.",
                0.72f
            )
            {
                TextColor =
                    Color.LightGray,

                DynamicallyScaleDownToWidth =
                    true
            };

        explanation.Top.Set(
            309f,
            0f
        );

        explanation.Width.Set(
            0f,
            1f
        );

        _panel.Append(
            explanation
        );
    }

    private void CreatePreview()
    {
        _preview =
            new ImagePreviewElement();

        _preview.Left.Set(
            0f,
            0f
        );

        _preview.Top.Set(
            348f,
            0f
        );

        _preview.Width.Set(
            0f,
            1f
        );

        _preview.Height.Set(
            240f,
            0f
        );

        _panel.Append(
            _preview
        );
    }

    private void CreateStatus()
    {
        _status =
            new UIText(
                "Load an image URL to begin.",
                0.82f
            )
            {
                TextColor =
                    Color.LightGray,

                DynamicallyScaleDownToWidth =
                    true
            };

        _status.Top.Set(
            598f,
            0f
        );

        _status.Width.Set(
            0f,
            1f
        );

        _panel.Append(
            _status
        );
    }

    private void CreateBottomButtons()
    {
        _panel.Append(
            CreateButton(
                "Convert to Blocks",
                0f,
                630f,
                210f,
                44f,
                ConvertClicked
            )
        );

        _panel.Append(
            CreateButton(
                "Select Position",
                225f,
                630f,
                210f,
                44f,
                SelectPositionClicked
            )
        );

        _galleryButton =
            CreateButton(
                "Gallery Mode",
                450f,
                630f,
                230f,
                44f,
                GalleryClicked
            );

        _panel.Append(
            _galleryButton
        );
    }

    public override void Update(
        GameTime gameTime
    )
    {
        base.Update(
            gameTime
        );

        if (
            _panel?
                .ContainsPoint(
                    Main.MouseScreen
                )
            == true
        )
        {
            Main.LocalPlayer.mouseInterface =
                true;
        }

        RefreshGalleryButton();

        RefreshClearUrlButton();
    }

    public override void OnDeactivate()
    {
        base.OnDeactivate();

        StopTextEntry();
    }

    private void ClearUrlClicked(
        UIMouseEvent evt,
        UIElement listeningElement
    )
    {
        _requestVersion++;

        _loadCancellation?
            .Cancel();

        _urlInput.StopWriting();

        _urlInput.Value =
            string.Empty;

        RefreshClearUrlButton();

        SetStatus(
            _sourcePixels is null
                ? "URL cleared."
                : "URL cleared. The loaded image preview has been kept.",
            Color.LightGray
        );
    }

    private void RefreshClearUrlButton()
    {
        if (
            _clearUrlButton is null
            || _urlInput is null
        )
        {
            return;
        }

        bool shouldBeVisible =
            !string.IsNullOrWhiteSpace(
                _urlInput.Value
            );

        if (
            shouldBeVisible
            == _clearUrlVisible
        )
        {
            return;
        }

        _clearUrlVisible =
            shouldBeVisible;

        if (
            shouldBeVisible
        )
        {
            _panel.Append(
                _clearUrlButton
            );

            _clearUrlButton.Recalculate();
        }
        else
        {
            _clearUrlButton.Remove();
        }
    }

    private void ModeClicked(
        UIMouseEvent evt,
        UIElement listeningElement
    )
    {
        if (
            _conversionMode
            == ImageConversionMode
                .VanillaBlocks
        )
        {
            if (
                !ModContent
                    .GetInstance<
                        ImageLoaderConfig
                    >()
                    .EnableExactRgbBlocks
            )
            {
                SetStatus(
                    "Exact RGB blocks are disabled in Image Loader's configuration.",
                    Color.OrangeRed
                );

                return;
            }

            _conversionMode =
                ImageConversionMode
                    .ExactRgb;
        }
        else
        {
            _conversionMode =
                ImageConversionMode
                    .VanillaBlocks;
        }

        _preparedImage =
            null;

        UIHandler.SetPreparedImage(
            null
        );

        RefreshModeButton();

        SetStatus(
            "Conversion mode changed. Convert the image again.",
            Color.LightBlue
        );
    }

    private void RefreshModeButton()
    {
        if (
            _modeButton is null
        )
        {
            return;
        }

        switch (
            _conversionMode
        )
        {
            case ImageConversionMode
                .VanillaBlocks:
            {
                _modeButton.SetText(
                    "Vanilla Blocks"
                );

                break;
            }

            case ImageConversionMode
                .ExactRgb:
            {
                _modeButton.SetText(
                    "Exact RGB (1:1 colour)"
                );

                break;
            }
        }
    }

    private void GalleryClicked(
        UIMouseEvent evt,
        UIElement listeningElement
    )
    {
        if (
            Main.gameMenu
        )
        {
            SetStatus(
                "Enter a world before using Gallery Mode.",
                Color.OrangeRed
            );

            return;
        }

        if (
            !ModContent
                .GetInstance<
                    ImageLoaderConfig
                >()
                .EnableGalleryMode
        )
        {
            SetStatus(
                "Gallery Mode is disabled in Image Loader's configuration.",
                Color.OrangeRed
            );

            return;
        }

        Main.LocalPlayer
            .GetModPlayer<
                GalleryPlayer
            >()
            .ToggleGalleryMode();

        RefreshGalleryButton();
    }

    private void RefreshGalleryButton()
    {
        if (
            _galleryButton is null
        )
        {
            return;
        }

        if (
            Main.gameMenu
        )
        {
            _galleryButton.SetText(
                "Gallery Mode"
            );

            return;
        }

        GalleryPlayer gallery =
            Main.LocalPlayer
                .GetModPlayer<
                    GalleryPlayer
                >();

        _galleryButton.SetText(
            gallery.GalleryMode

                ? "Gallery Mode: ON"

                : "Gallery Mode: OFF"
        );
    }

    private void ScaleAspect(
        double multiplier
    )
    {
        if (
            _sourcePixels is null
            || _sourceWidth < 1
            || _sourceHeight < 1
        )
        {
            SetStatus(
                "Load an image before scaling its aspect ratio.",
                Color.OrangeRed
            );

            return;
        }

        (
            int fittedWidth,
            int fittedHeight
        ) =
            FitInsideLimits(
                _sourceWidth,
                _sourceHeight
            );

        bool widthIsValid =
            int.TryParse(
                _widthInput.Value,
                out int currentWidth
            );

        bool heightIsValid =
            int.TryParse(
                _heightInput.Value,
                out int currentHeight
            );

        if (
            !widthIsValid
            || currentWidth < 1
        )
        {
            currentWidth =
                fittedWidth;
        }

        if (
            !heightIsValid
            || currentHeight < 1
        )
        {
            currentHeight =
                fittedHeight;
        }

        currentWidth =
            Math.Clamp(
                currentWidth,
                1,
                ImagePlacementService.MaxWidth
            );

        currentHeight =
            Math.Clamp(
                currentHeight,
                1,
                ImagePlacementService.MaxHeight
            );

        bool landscape =
            _sourceWidth >= _sourceHeight;

        int currentLongSide =
            landscape
                ? currentWidth
                : currentHeight;

        if (
            currentLongSide < 1
        )
        {
            currentLongSide =
                landscape
                    ? fittedWidth
                    : fittedHeight;
        }

        int maximumLongSide =
            landscape
                ? ImagePlacementService.MaxWidth
                : ImagePlacementService.MaxHeight;

        int nextLongSide =
            (int)Math.Round(
                currentLongSide
                * multiplier
            );

        if (
            nextLongSide == currentLongSide
        )
        {
            nextLongSide +=
                multiplier > 1d
                    ? 1
                    : -1;
        }

        nextLongSide =
            Math.Clamp(
                nextLongSide,
                1,
                maximumLongSide
            );

        if (
            nextLongSide == currentLongSide
        )
        {
            SetStatus(
                multiplier > 1d
                    ? "The image is already at the largest supported aspect-locked size."
                    : "The image is already at the smallest supported aspect-locked size.",
                Color.LightGray
            );

            return;
        }

        ApplyAspectLongSide(
            nextLongSide
        );
    }

    private void ResetAspectScale()
    {
        if (
            _sourcePixels is null
        )
        {
            SetStatus(
                "Load an image before resetting its aspect ratio.",
                Color.OrangeRed
            );

            return;
        }

        (
            int width,
            int height
        ) =
            FitInsideLimits(
                _sourceWidth,
                _sourceHeight
            );

        ApplyDimensions(
            width,
            height
        );
    }

    private void ApplyAspectLongSide(
        int longSide
    )
    {
        bool landscape =
            _sourceWidth >= _sourceHeight;

        double scale =
            longSide
            / (double)(
                landscape
                    ? _sourceWidth
                    : _sourceHeight
            );

        int width =
            Math.Clamp(
                (int)Math.Round(
                    _sourceWidth
                    * scale
                ),
                1,
                ImagePlacementService.MaxWidth
            );

        int height =
            Math.Clamp(
                (int)Math.Round(
                    _sourceHeight
                    * scale
                ),
                1,
                ImagePlacementService.MaxHeight
            );

        ApplyDimensions(
            width,
            height
        );
    }

    private void ApplyDimensions(
        int width,
        int height
    )
    {
        _widthInput.Value =
            width.ToString();

        _heightInput.Value =
            height.ToString();

        _preparedImage =
            null;

        UIHandler.SetPreparedImage(
            null
        );

        _preview.SetPixels(
            _sourcePixels,
            _sourceWidth,
            _sourceHeight
        );

        SetStatus(
            $"Aspect ratio preserved at {width} x {height} blocks. Convert to refresh the block preview.",
            Color.LightBlue
        );
    }

    private async void LoadUrlClicked(
        UIMouseEvent evt,
        UIElement listeningElement
    )
    {
        string value =
            _urlInput
                .Value
                .Trim();

        if (
            !Uri.TryCreate(
                value,
                UriKind.Absolute,
                out Uri uri
            )

            || (
                uri.Scheme
                    != Uri.UriSchemeHttp

                && uri.Scheme
                    != Uri.UriSchemeHttps
            )
        )
        {
            SetStatus(
                "Enter a complete http:// or https:// image URL.",
                Color.OrangeRed
            );

            return;
        }

        int requestVersion =
            ++_requestVersion;

        _loadCancellation?
            .Cancel();

        _loadCancellation?
            .Dispose();

        _loadCancellation =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(
                    20
                )
            );

        SetStatus(
            "Downloading image...",
            Color.LightBlue
        );

        try
        {
            byte[] bytes =
                await DownloadImage(
                    uri,
                    _loadCancellation
                        .Token
                );

            Main.QueueMainThreadAction(
                () =>
                    FinishLoading(
                        bytes,
                        requestVersion
                    )
            );
        }
        catch (
            OperationCanceledException
        )
        {
            if (
                requestVersion
                == _requestVersion
            )
            {
                Main.QueueMainThreadAction(
                    () =>
                        SetStatus(
                            "The download timed out or was cancelled.",
                            Color.OrangeRed
                        )
                );
            }
        }
        catch (
            Exception exception
        )
        {
            if (
                requestVersion
                == _requestVersion
            )
            {
                Main.QueueMainThreadAction(
                    () =>
                        SetStatus(
                            $"Could not load image: {ShortMessage(exception)}",
                            Color.OrangeRed
                        )
                );
            }
        }
    }

    private void FinishLoading(
        byte[] bytes,
        int requestVersion
    )
    {
        if (
            requestVersion
            != _requestVersion
        )
        {
            return;
        }

        Texture2D decodedTexture =
            null;

        try
        {
            if (
                !TryReadImageDimensions(
                    bytes,
                    out int encodedWidth,
                    out int encodedHeight
                )
            )
            {
                throw new InvalidDataException(
                    "the response is not a valid PNG or JPEG"
                );
            }

            ValidateSourceDimensions(
                encodedWidth,
                encodedHeight
            );

            using var stream =
                new MemoryStream(
                    bytes,
                    writable: false
                );

            decodedTexture =
                Texture2D.FromStream(
                    Main.instance
                        .GraphicsDevice,
                    stream
                );

            ValidateSourceDimensions(
                decodedTexture.Width,
                decodedTexture.Height
            );

            var pixels =
                new Color[
                    decodedTexture.Width
                    * decodedTexture.Height
                ];

            decodedTexture.GetData(
                pixels
            );

            _sourcePixels =
                pixels;

            _sourceWidth =
                decodedTexture.Width;

            _sourceHeight =
                decodedTexture.Height;

            _preparedImage =
                null;

            UIHandler.SetPreparedImage(
                null
            );

            Color[] previewPixels =
                CreatePreviewPixels(
                    pixels,
                    decodedTexture.Width,
                    decodedTexture.Height,
                    out int previewWidth,
                    out int previewHeight
                );

            _preview.SetPixels(
                previewPixels,
                previewWidth,
                previewHeight
            );

            (
                int width,
                int height
            ) =
                FitInsideLimits(
                decodedTexture.Width,
                decodedTexture.Height
                );

            _widthInput.Value =
                width.ToString();

            _heightInput.Value =
                height.ToString();

            int transparentPixels =
                0;

            int partiallyTransparentPixels =
                0;

            int alphaSampleStride =
                Math.Max(
                    1,
                    pixels.Length
                    / 500_000
                );

            int alphaSamples = 0;

            for (
                int pixelIndex = 0;
                pixelIndex < pixels.Length;
                pixelIndex += alphaSampleStride
            )
            {
                Color pixel =
                    pixels[pixelIndex];

                alphaSamples++;

                if (
                    pixel.A
                    < ImagePlacementService
                        .TransparencyThreshold
                )
                {
                    transparentPixels++;
                }
                else if (
                    pixel.A < byte.MaxValue
                )
                {
                    partiallyTransparentPixels++;
                }
            }

            string transparencyInfo;

            if (
                transparentPixels == 0
                && partiallyTransparentPixels == 0
            )
            {
                transparencyInfo =
                    "opaque";
            }
            else
            {
                double transparentPercent =
                    transparentPixels
                    * 100d
                    / alphaSamples;

                double partialPercent =
                    partiallyTransparentPixels
                    * 100d
                    / alphaSamples;

                transparencyInfo =
                    $"{transparentPercent:0.#}% transparent, {partialPercent:0.#}% partial alpha";
            }

            _sourceInfo.SetText(
                $"Source: {decodedTexture.Width:N0} x {decodedTexture.Height:N0} pixels | Alpha: {transparencyInfo}"
            );

            SetStatus(
                "Image loaded. Transparency is shown by the grey-and-white checkerboard.",
                Color.LightGreen
            );
        }
        catch (
            Exception exception
        )
        {
            SetStatus(
                $"That file is not a supported PNG or JPEG: {ShortMessage(exception)}",
                Color.OrangeRed
            );
        }
        finally
        {
            if (
                decodedTexture is not null
                && !decodedTexture.IsDisposed
            )
            {
                decodedTexture.Dispose();
            }
        }
    }

    private void ConvertClicked(
        UIMouseEvent evt,
        UIElement listeningElement
    )
    {
        if (
            _sourcePixels is null
        )
        {
            SetStatus(
                "Load an image first.",
                Color.OrangeRed
            );

            return;
        }

        if (
            !int.TryParse(
                _widthInput.Value,
                out int width
            )

            || !int.TryParse(
                _heightInput.Value,
                out int height
            )

            || width
                is < 1
                or > ImagePlacementService
                    .MaxWidth

            || height
                is < 1
                or > ImagePlacementService
                    .MaxHeight

            || width * height
                > ImagePlacementService
                    .MaxPixels
        )
        {
            SetStatus(
                $"Use a size from 1 x 1 through {ImagePlacementService.MaxWidth} x {ImagePlacementService.MaxHeight}.",
                Color.OrangeRed
            );

            return;
        }

        if (
            _conversionMode
                == ImageConversionMode
                    .ExactRgb

            && !ModContent
                .GetInstance<
                    ImageLoaderConfig
                >()
                .EnableExactRgbBlocks
        )
        {
            SetStatus(
                "Exact RGB blocks are disabled in Image Loader's configuration.",
                Color.OrangeRed
            );

            return;
        }

        SetStatus(
            _conversionMode
                == ImageConversionMode
                    .ExactRgb

                ? "Preparing exact RGB pixels..."

                : "Matching pixels to Terraria blocks...",

            Color.LightBlue
        );

        var tileTypes =
            new ushort[
                width * height
            ];

        var previewColors =
            new Color[
                width * height
            ];

        Color[] exactColors =
            _conversionMode
                == ImageConversionMode
                    .ExactRgb

                ? new Color[
                    width * height
                ]

                : null;

        int opaquePixels = 0;

        for (
            int y = 0;
            y < height;
            y++
        )
        {
            for (
                int x = 0;
                x < width;
                x++
            )
            {
                Color source =
                    SampleSourcePixel(
                        x,
                        y,
                        width,
                        height
                    );

                    int index =
                        y * width + x;

                    if (
                        source.A
                            < ImagePlacementService
                                .TransparencyThreshold
                    )
                    {
                        tileTypes[
                            index
                        ] =
                            ImagePlacementService
                                .Transparent;

                        previewColors[
                            index
                        ] =
                            Color.Transparent;

                        if (
                            exactColors
                            is not null
                        )
                        {
                            exactColors[
                                index
                            ] =
                                Color.Transparent;
                        }

                        continue;
                    }

                    if (
                        _conversionMode
                        == ImageConversionMode
                            .ExactRgb
                    )
                    {
                        Color exact =
                            new Color(
                                source.R,
                                source.G,
                                source.B,
                                255
                            );

                        tileTypes[
                            index
                        ] = 0;

                        previewColors[
                            index
                        ] =
                            exact;

                        exactColors[
                            index
                        ] =
                            exact;
                    }
                    else
                    {
                        tileTypes[
                            index
                        ] =
                            ImagePalette
                                .FindClosestTile(
                                    source,
                                    out Color matchedColor
                                );

                        previewColors[
                            index
                        ] =
                            matchedColor;
                    }

                opaquePixels++;
            }
        }

        _preparedImage =
            new PreparedImage(
                width,
                height,
                _conversionMode,
                tileTypes,
                previewColors,
                exactColors
            );

        UIHandler.SetPreparedImage(
            _preparedImage
        );

        _preview.SetPixels(
            previewColors,
            width,
            height
        );

        string modeText =
            _conversionMode
                == ImageConversionMode
                    .ExactRgb

                ? "Exact RGB"

                : "Vanilla Blocks";

        SetStatus(
            $"Ready: {opaquePixels:N0} blocks using {modeText}. Review the preview, then select a position.",
            Color.LightGreen
        );
    }

    private Color SampleSourcePixel(
        int outputX,
        int outputY,
        int outputWidth,
        int outputHeight
    )
    {
        double left =
            outputX
            * _sourceWidth
            / (double)outputWidth;

        double right =
            (outputX + 1)
            * _sourceWidth
            / (double)outputWidth;

        double top =
            outputY
            * _sourceHeight
            / (double)outputHeight;

        double bottom =
            (outputY + 1)
            * _sourceHeight
            / (double)outputHeight;

        int startX =
            Math.Max(
                0,
                (int)Math.Floor(
                    left
                )
            );

        int endX =
            Math.Min(
                _sourceWidth,
                (int)Math.Ceiling(
                    right
                )
            );

        int startY =
            Math.Max(
                0,
                (int)Math.Floor(
                    top
                )
            );

        int endY =
            Math.Min(
                _sourceHeight,
                (int)Math.Ceiling(
                    bottom
                )
            );

        double totalWeight =
            0d;

        double alphaWeight =
            0d;

        double redWeight =
            0d;

        double greenWeight =
            0d;

        double blueWeight =
            0d;

        for (
            int sourceY = startY;
            sourceY < endY;
            sourceY++
        )
        {
            double verticalWeight =
                Math.Max(
                    0d,
                    Math.Min(
                        bottom,
                        sourceY + 1d
                    )
                    - Math.Max(
                        top,
                        sourceY
                    )
                );

            for (
                int sourceX = startX;
                sourceX < endX;
                sourceX++
            )
            {
                double horizontalWeight =
                    Math.Max(
                        0d,
                        Math.Min(
                            right,
                            sourceX + 1d
                        )
                        - Math.Max(
                            left,
                            sourceX
                        )
                    );

                double weight =
                    horizontalWeight
                    * verticalWeight;

                Color pixel =
                    _sourcePixels[
                        sourceY
                        * _sourceWidth
                        + sourceX
                    ];

                double weightedAlpha =
                    pixel.A
                    * weight;

                totalWeight +=
                    weight;

                alphaWeight +=
                    weightedAlpha;

                redWeight +=
                    pixel.R
                    * weightedAlpha;

                greenWeight +=
                    pixel.G
                    * weightedAlpha;

                blueWeight +=
                    pixel.B
                    * weightedAlpha;
            }
        }

        if (
            totalWeight <= 0d
            || alphaWeight <= 0d
        )
        {
            return Color.Transparent;
        }

        byte alpha =
            (byte)Math.Clamp(
                (int)Math.Round(
                    alphaWeight
                    / totalWeight
                ),
                0,
                byte.MaxValue
            );

        byte red =
            (byte)Math.Clamp(
                (int)Math.Round(
                    redWeight
                    / alphaWeight
                ),
                0,
                byte.MaxValue
            );

        byte green =
            (byte)Math.Clamp(
                (int)Math.Round(
                    greenWeight
                    / alphaWeight
                ),
                0,
                byte.MaxValue
            );

        byte blue =
            (byte)Math.Clamp(
                (int)Math.Round(
                    blueWeight
                    / alphaWeight
                ),
                0,
                byte.MaxValue
            );

        return new Color(
            red,
            green,
            blue,
            alpha
        );
    }

    private void SelectPositionClicked(
        UIMouseEvent evt,
        UIElement listeningElement
    )
    {
        if (
            _preparedImage
            is null
        )
        {
            SetStatus(
                "Convert the image to blocks first.",
                Color.OrangeRed
            );

            return;
        }

        if (
            Main.gameMenu
        )
        {
            SetStatus(
                "Enter a world before selecting a placement position.",
                Color.OrangeRed
            );

            return;
        }

        StopTextEntry();

        UIHandler.BeginPlacement();
    }

    private void StopTextEntry()
    {
        _urlInput?
            .StopWriting();

        _widthInput?
            .StopWriting();

        _heightInput?
            .StopWriting();

        Terraria.GameInput
            .PlayerInput
            .WritingText =
                false;

        Main.blockInput =
            false;
    }

    private TextInputBox AddInput(
        string hint,
        float top,
        float left,
        float width
    )
    {
        var holder =
            new UIPanel
            {
                BackgroundColor =
                    new Color(
                        20,
                        25,
                        38
                    ),

                BorderColor =
                    new Color(
                        72,
                        91,
                        130
                    )
            };

        holder.Left.Set(
            left,
            0f
        );

        holder.Top.Set(
            top,
            0f
        );

        holder.Width.Set(
            width,
            0f
        );

        holder.Height.Set(
            40f,
            0f
        );

        holder.SetPadding(
            7f
        );

        _panel.Append(
            holder
        );

        var input =
            new TextInputBox(
                hint
            );

        input.Width.Set(
            0f,
            1f
        );

        input.Height.Set(
            0f,
            1f
        );

        holder.Append(
            input
        );

        return input;
    }

    private void AddLabel(
        string text,
        float top,
        float left
    )
    {
        var label =
            new UIText(
                text,
                0.82f
            );

        label.Left.Set(
            left,
            0f
        );

        label.Top.Set(
            top,
            0f
        );

        _panel.Append(
            label
        );
    }

    private static UITextPanel<string>
        CreateButton(
            string text,
            float left,
            float top,
            float width,
            float height,
            MouseEvent click
        )
    {
        var button =
            new UITextPanel<string>(
                text,
                0.82f
            )
            {
                BackgroundColor =
                    new Color(
                        63,
                        82,
                        124
                    ),

                BorderColor =
                    new Color(
                        106,
                        135,
                        190
                    )
            };

        button.Left.Set(
            left,
            0f
        );

        button.Top.Set(
            top,
            0f
        );

        button.Width.Set(
            width,
            0f
        );

        button.Height.Set(
            height,
            0f
        );

        button.OnLeftClick +=
            click;

        return button;
    }

    private void SetStatus(
        string text,
        Color color
    )
    {
        _status.SetText(
            text
        );

        _status.TextColor =
            color;
    }

    private static void ValidateSourceDimensions(
        int width,
        int height
    )
    {
        if (
            width < 1
            || height < 1
            || width > MaxSourceDimension
            || height > MaxSourceDimension
            || (long)width * height
                > MaxSourcePixels
        )
        {
            throw new InvalidDataException(
                "the image dimensions exceed the safe 8-megapixel / 8192-pixel limit"
            );
        }
    }

    private static Color[] CreatePreviewPixels(
        Color[] source,
        int sourceWidth,
        int sourceHeight,
        out int previewWidth,
        out int previewHeight
    )
    {
        double scale =
            Math.Min(
                1d,
                Math.Min(
                    PreviewMaximumWidth
                    / (double)sourceWidth,
                    PreviewMaximumHeight
                    / (double)sourceHeight
                )
            );

        previewWidth =
            Math.Max(
                1,
                (int)Math.Round(
                    sourceWidth
                    * scale
                )
            );

        previewHeight =
            Math.Max(
                1,
                (int)Math.Round(
                    sourceHeight
                    * scale
                )
            );

        if (
            previewWidth == sourceWidth
            && previewHeight == sourceHeight
        )
        {
            return source;
        }

        var preview =
            new Color[
                previewWidth
                * previewHeight
            ];

        for (
            int y = 0;
            y < previewHeight;
            y++
        )
        {
            int sourceY =
                Math.Min(
                    sourceHeight - 1,
                    (
                        y * 2 + 1
                    )
                    * sourceHeight
                    / (
                        previewHeight * 2
                    )
                );

            for (
                int x = 0;
                x < previewWidth;
                x++
            )
            {
                int sourceX =
                    Math.Min(
                        sourceWidth - 1,
                        (
                            x * 2 + 1
                        )
                        * sourceWidth
                        / (
                            previewWidth * 2
                        )
                    );

                preview[
                    y * previewWidth + x
                ] =
                    source[
                        sourceY * sourceWidth
                        + sourceX
                    ];
            }
        }

        return preview;
    }

    private static bool TryReadImageDimensions(
        byte[] bytes,
        out int width,
        out int height
    )
    {
        width = 0;

        height = 0;

        if (
            bytes.Length >= 24
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47
            && bytes[4] == 0x0D
            && bytes[5] == 0x0A
            && bytes[6] == 0x1A
            && bytes[7] == 0x0A
        )
        {
            width =
                ReadBigEndianInt32(
                    bytes,
                    16
                );

            height =
                ReadBigEndianInt32(
                    bytes,
                    20
                );

            return width > 0
                && height > 0;
        }

        if (
            bytes.Length < 4
            || bytes[0] != 0xFF
            || bytes[1] != 0xD8
        )
        {
            return false;
        }

        int offset = 2;

        while (
            offset + 8 < bytes.Length
        )
        {
            while (
                offset < bytes.Length
                && bytes[offset] == 0xFF
            )
            {
                offset++;
            }

            if (
                offset >= bytes.Length
            )
            {
                break;
            }

            byte marker =
                bytes[offset++];

            if (
                marker == 0xD8
                || marker == 0xD9
            )
            {
                continue;
            }

            if (
                offset + 1 >= bytes.Length
            )
            {
                break;
            }

            int segmentLength =
                bytes[offset] * 256
                + bytes[offset + 1];

            if (
                segmentLength < 2
                || offset + segmentLength
                    > bytes.Length
            )
            {
                break;
            }

            if (
                IsJpegStartOfFrame(
                    marker
                )
                && segmentLength >= 7
            )
            {
                height =
                    bytes[offset + 3] * 256
                    + bytes[offset + 4];

                width =
                    bytes[offset + 5] * 256
                    + bytes[offset + 6];

                return width > 0
                    && height > 0;
            }

            offset +=
                segmentLength;
        }

        return false;
    }

    private static bool IsJpegStartOfFrame(
        byte marker
    )
    {
        return marker
            is 0xC0
            or 0xC1
            or 0xC2
            or 0xC3
            or 0xC5
            or 0xC6
            or 0xC7
            or 0xC9
            or 0xCA
            or 0xCB
            or 0xCD
            or 0xCE
            or 0xCF;
    }

    private static int ReadBigEndianInt32(
        byte[] bytes,
        int offset
    )
    {
        return (
                bytes[offset] << 24
            )
            | (
                bytes[offset + 1] << 16
            )
            | (
                bytes[offset + 2] << 8
            )
            | bytes[offset + 3];
    }

    private static async Task<byte[]>
        DownloadImage(
            Uri uri,
            CancellationToken cancellationToken
        )
    {
        using HttpResponseMessage response =
            await HttpClient.GetAsync(
                uri,

                HttpCompletionOption
                    .ResponseHeadersRead,

                cancellationToken
            );

        response.EnsureSuccessStatusCode();

        if (
            response
                .Content
                .Headers
                .ContentLength
            > MaxDownloadBytes
        )
        {
            throw new InvalidDataException(
                "the file is larger than 15 MB"
            );
        }

        await using Stream input =
            await response
                .Content
                .ReadAsStreamAsync(
                    cancellationToken
                );

        using var output =
            new MemoryStream();

        var buffer =
            new byte[
                81920
            ];

        while (
            true
        )
        {
            int read =
                await input.ReadAsync(
                    buffer.AsMemory(
                        0,
                        buffer.Length
                    ),

                    cancellationToken
                );

            if (
                read == 0
            )
            {
                break;
            }

            if (
                output.Length
                + read
                > MaxDownloadBytes
            )
            {
                throw new InvalidDataException(
                    "the file is larger than 15 MB"
                );
            }

            output.Write(
                buffer,
                0,
                read
            );
        }

        return output.ToArray();
    }

    private static HttpClient
        CreateHttpClient()
    {
        var client =
            new HttpClient();

        client
            .DefaultRequestHeaders
            .UserAgent
            .ParseAdd(
                "ImageLoader-tModLoader/0.3"
            );

        return client;
    }

    private static (
        int Width,
        int Height
    )
        FitInsideLimits(
            int width,
            int height
        )
    {
        double scale =
            Math.Min(
                1d,

                Math.Min(
                    ImagePlacementService
                        .MaxWidth
                    / (double)width,

                    ImagePlacementService
                        .MaxHeight
                    / (double)height
                )
            );

        return (
            Math.Max(
                1,
                (int)Math.Round(
                    width * scale
                )
            ),

            Math.Max(
                1,
                (int)Math.Round(
                    height * scale
                )
            )
        );
    }

    private static string ShortMessage(
        Exception exception
    )
    {
        string message =
            exception
                .GetBaseException()
                .Message
                .Replace(
                    '\r',
                    ' '
                )
                .Replace(
                    '\n',
                    ' '
                );

        return message.Length
                <= 100

            ? message

            : message[
                ..100
            ]
            + "...";
    }

    public void Dispose()
    {
        _requestVersion++;

        _loadCancellation?
            .Cancel();

        _loadCancellation?
            .Dispose();

        _preview?
            .Dispose();

        _sourcePixels =
            null;

        _preparedImage =
            null;
    }
}
