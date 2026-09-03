using System;
using System.Collections.Generic;
using System.IO;
using ImageLoader.Common.Config;
using ImageLoader.Common.Data;
using ImageLoader.Common.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ImageLoader.Common.Services;

internal static class ImagePlacementService
{
    public const int MaxWidth = 128;

    public const int MaxHeight = 128;

    public const int MaxPixels =
        MaxWidth * MaxHeight;

    public const ushort Transparent =
        ushort.MaxValue;

    public const byte TransparencyThreshold =
        64;

    private readonly record struct Run(
        ushort TileType,
        ushort Length
    );

    public static void RequestPlacement(
        int startX,
        int startY,
        PreparedImage image
    )
    {
        if (
            !ValidateBounds(
                startX,
                startY,
                image.Width,
                image.Height
            )
        )
        {
            Main.NewText(
                "The image must fit inside the world boundary.",
                Color.OrangeRed
            );

            return;
        }

        if (
            image.Mode
                == ImageConversionMode.ExactRgb

            && !ModContent
                .GetInstance<
                    ImageLoaderConfig
                >()
                .EnableExactRgbBlocks
        )
        {
            Main.NewText(
                "Exact RGB blocks are disabled in Image Loader's configuration.",
                Color.OrangeRed
            );

            return;
        }

        if (
            Main.netMode
            == NetmodeID.MultiplayerClient
        )
        {
            SendPlacementPacket(
                startX,
                startY,
                image
            );

            Main.NewText(
                $"Sent {image.Width} x {image.Height} image to the server for placement.",
                Color.LightGreen
            );

            return;
        }

        int placed =
            ApplyPlacement(
                startX,
                startY,
                image.Width,
                image.Height,
                image.Mode,
                image.TileTypes,
                image.ExactColors
            );

        Main.NewText(
            $"Placed {placed:N0} image blocks.",
            Color.LightGreen
        );
    }

    private static void SendPlacementPacket(
        int startX,
        int startY,
        PreparedImage image
    )
    {
        if (
            image.Mode
            == ImageConversionMode.VanillaBlocks
        )
        {
            List<Run> runs =
                BuildRuns(
                    image.TileTypes
                );

            ModPacket packet =
                global::ImageLoader.ImageLoader
                    .Instance
                    .GetPacket(
                        20
                        + runs.Count * 4
                    );

            WriteHeader(
                packet,
                image.Mode,
                startX,
                startY,
                image.Width,
                image.Height
            );

            packet.Write(
                (ushort)runs.Count
            );

            foreach (
                Run run
                in runs
            )
            {
                packet.Write(
                    run.TileType
                );

                packet.Write(
                    run.Length
                );
            }

            packet.Send();

            return;
        }

        if (
            image.ExactColors is null
        )
        {
            return;
        }

        int pixelCount =
            image.Width
            * image.Height;

        int maskBytes =
            (
                pixelCount + 7
            )
            / 8;

        ModPacket rgbPacket =
            global::ImageLoader.ImageLoader
                .Instance
                .GetPacket(
                    20
                    + maskBytes
                    + pixelCount * 3
                );

        WriteHeader(
            rgbPacket,
            image.Mode,
            startX,
            startY,
            image.Width,
            image.Height
        );

        WriteRgbPayload(
            rgbPacket,
            image.ExactColors
        );

        rgbPacket.Send();
    }

    private static void WriteHeader(
        ModPacket packet,
        ImageConversionMode mode,
        int startX,
        int startY,
        int width,
        int height
    )
    {
        packet.Write(
            (byte)global::ImageLoader.ImageLoader
                .MessageType
                .PlaceImage
        );

        packet.Write(
            (byte)mode
        );

        packet.Write(
            startX
        );

        packet.Write(
            startY
        );

        packet.Write(
            (ushort)width
        );

        packet.Write(
            (ushort)height
        );
    }

    private static void WriteRgbPayload(
        ModPacket packet,
        Color[] colors
    )
    {
        int count =
            colors.Length;

        int maskLength =
            (
                count + 7
            )
            / 8;

        var mask =
            new byte[
                maskLength
            ];

        for (
            int i = 0;
            i < count;
            i++
        )
        {
            if (
                colors[i].A < TransparencyThreshold
            )
            {
                continue;
            }

            mask[
                i >> 3
            ] |=
                (byte)(
                    1
                    << (
                        i & 7
                    )
                );
        }

        packet.Write(
            mask
        );

        for (
            int i = 0;
            i < count;
            i++
        )
        {
            Color color =
                colors[i];

            if (
                color.A < TransparencyThreshold
            )
            {
                continue;
            }

            packet.Write(
                color.R
            );

            packet.Write(
                color.G
            );

            packet.Write(
                color.B
            );
        }
    }

    public static void ReceivePlacement(
        BinaryReader reader,
        int whoAmI
    )
    {
        if (
            Main.netMode
            != NetmodeID.Server
        )
        {
            return;
        }

        try
        {
            ImageConversionMode mode =
                (ImageConversionMode)
                reader.ReadByte();

            int startX =
                reader.ReadInt32();

            int startY =
                reader.ReadInt32();

            int width =
                reader.ReadUInt16();

            int height =
                reader.ReadUInt16();

            if (
                !Main.player[
                    whoAmI
                ].active

                || mode
                    is not (
                        ImageConversionMode
                            .VanillaBlocks

                        or ImageConversionMode
                            .ExactRgb
                    )

                || width
                    is < 1
                    or > MaxWidth

                || height
                    is < 1
                    or > MaxHeight

                || width * height
                    > MaxPixels

                || !ValidateBounds(
                    startX,
                    startY,
                    width,
                    height
                )
            )
            {
                return;
            }

            if (
                mode
                    == ImageConversionMode
                        .ExactRgb

                && !ModContent
                    .GetInstance<
                        ImageLoaderConfig
                    >()
                    .EnableExactRgbBlocks
            )
            {
                return;
            }

            ushort[] tileTypes =
                new ushort[
                    width * height
                ];

            Array.Fill(
                tileTypes,
                Transparent
            );

            Color[] exactColors =
                null;

            if (
                mode
                == ImageConversionMode
                    .VanillaBlocks
            )
            {
                if (
                    !ReadVanillaPayload(
                        reader,
                        tileTypes
                    )
                )
                {
                    return;
                }
            }
            else
            {
                exactColors =
                    ReadRgbPayload(
                        reader,
                        tileTypes.Length
                    );

                if (
                    exactColors is null
                )
                {
                    return;
                }
            }

            ApplyPlacement(
                startX,
                startY,
                width,
                height,
                mode,
                tileTypes,
                exactColors
            );
        }
        catch (
            IOException
        )
        {
            global::ImageLoader.ImageLoader
                .Instance
                .Logger
                .Warn(
                    $"Player {whoAmI} sent a truncated Image Loader placement packet."
                );
        }
    }

    private static bool ReadVanillaPayload(
        BinaryReader reader,
        ushort[] tileTypes
    )
    {
        int runCount =
            reader.ReadUInt16();

        if (
            runCount
                is < 1
                or > MaxPixels
        )
        {
            return false;
        }

        int offset = 0;

        for (
            int runIndex = 0;
            runIndex < runCount;
            runIndex++
        )
        {
            ushort tileType =
                reader.ReadUInt16();

            int length =
                reader.ReadUInt16();

            if (
                length < 1

                || offset
                    + length
                    > tileTypes.Length

                || (
                    tileType
                        != Transparent

                    && !ImagePalette
                        .IsAllowedBlock(
                            tileType
                        )
                )
            )
            {
                return false;
            }

            Array.Fill(
                tileTypes,
                tileType,
                offset,
                length
            );

            offset +=
                length;
        }

        return offset
            == tileTypes.Length;
    }

    private static Color[] ReadRgbPayload(
        BinaryReader reader,
        int pixelCount
    )
    {
        int maskLength =
            (
                pixelCount + 7
            )
            / 8;

        byte[] mask =
            reader.ReadBytes(
                maskLength
            );

        if (
            mask.Length
            != maskLength
        )
        {
            return null;
        }

        var colors =
            new Color[
                pixelCount
            ];

        for (
            int i = 0;
            i < pixelCount;
            i++
        )
        {
            bool opaque =
                (
                    mask[
                        i >> 3
                    ]
                    & (
                        1
                        << (
                            i & 7
                        )
                    )
                )
                != 0;

            if (
                !opaque
            )
            {
                colors[i] =
                    Color.Transparent;

                continue;
            }

            byte red =
                reader.ReadByte();

            byte green =
                reader.ReadByte();

            byte blue =
                reader.ReadByte();

            colors[i] =
                new Color(
                    red,
                    green,
                    blue,
                    255
                );
        }

        return colors;
    }

    private static int ApplyPlacement(
        int startX,
        int startY,
        int width,
        int height,
        ImageConversionMode mode,
        ushort[] tileTypes,
        Color[] exactColors
    )
    {
        int placed = 0;

        ushort rgbTileType =
            (ushort)ModContent
                .TileType<
                    RgbPixelTile
                >();

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
                int index =
                    y * width + x;

                bool transparent;

                if (
                    mode
                    == ImageConversionMode
                        .ExactRgb
                )
                {
                    transparent =
                        exactColors is null
                        || exactColors[
                            index
                        ].A < TransparencyThreshold;
                }
                else
                {
                    transparent =
                        tileTypes[
                            index
                        ]
                        == Transparent;
                }

                if (
                    transparent
                )
                {
                    continue;
                }

                int worldX =
                    startX + x;

                int worldY =
                    startY + y;

                Tile existing =
                    Main.tile[
                        worldX,
                        worldY
                    ];

                if (
                    existing.HasTile
                )
                {
                    WorldGen.KillTile(
                        worldX,
                        worldY,
                        noItem: true
                    );
                }

                RgbPixelService.RemoveColor(
                    worldX,
                    worldY
                );

                Tile tile =
                    Main.tile[
                        worldX,
                        worldY
                    ];

                tile.ClearTile();

                tile.HasTile =
                    true;

                tile.TileType =
                    mode
                        == ImageConversionMode
                            .ExactRgb

                        ? rgbTileType

                        : tileTypes[
                            index
                        ];

                tile.Slope =
                    SlopeType.Solid;

                tile.IsHalfBlock =
                    false;

                tile.LiquidAmount =
                    0;

                // Image tiles are display pixels. Glow coating keeps both
                // vanilla matches and Exact RGB pixels readable regardless
                // of the world's ambient lighting, and is persisted/synced
                // by Terraria as part of the tile data.
                tile.IsTileFullbright =
                    true;

                if (
                    mode
                    == ImageConversionMode
                        .ExactRgb
                )
                {
                    RgbPixelService.SetColor(
                        worldX,
                        worldY,
                        exactColors[
                            index
                        ]
                    );
                }

                placed++;
            }
        }

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
                int index =
                    y * width + x;

                bool transparent =
                    mode
                    == ImageConversionMode
                        .ExactRgb

                    ? exactColors is null
                        || exactColors[
                            index
                        ].A < TransparencyThreshold

                    : tileTypes[
                        index
                    ]
                    == Transparent;

                if (
                    transparent
                )
                {
                    continue;
                }

                WorldGen.SquareTileFrame(
                    startX + x,
                    startY + y,
                    true
                );
            }
        }

        if (
            Main.netMode
            == NetmodeID.Server
        )
        {
            SyncTileRegion(
                startX,
                startY,
                width,
                height
            );

            if (
                mode
                == ImageConversionMode
                    .ExactRgb
            )
            {
                RgbPixelService.BroadcastRegion(
                    startX,
                    startY,
                    width,
                    height,
                    exactColors
                );
            }
        }

        return placed;
    }

    private static void SyncTileRegion(
        int startX,
        int startY,
        int width,
        int height
    )
    {
        const int syncSize =
            32;

        for (
            int y = 0;
            y < height;
            y += syncSize
        )
        {
            for (
                int x = 0;
                x < width;
                x += syncSize
            )
            {
                NetMessage.SendTileSquare(
                    -1,

                    startX + x,

                    startY + y,

                    Math.Min(
                        syncSize,
                        width - x
                    ),

                    Math.Min(
                        syncSize,
                        height - y
                    )
                );
            }
        }
    }

    private static bool ValidateBounds(
        int startX,
        int startY,
        int width,
        int height
    )
    {
        return width
                is >= 1
                and <= MaxWidth

            && height
                is >= 1
                and <= MaxHeight

            && width * height
                <= MaxPixels

            && WorldGen.InWorld(
                startX,
                startY,
                5
            )

            && WorldGen.InWorld(
                startX
                    + width
                    - 1,

                startY
                    + height
                    - 1,

                5
            );
    }

    private static List<Run> BuildRuns(
        ushort[] tileTypes
    )
    {
        var runs =
            new List<Run>();

        ushort tileType =
            tileTypes[0];

        int length = 1;

        for (
            int index = 1;
            index < tileTypes.Length;
            index++
        )
        {
            if (
                tileTypes[
                    index
                ]
                    == tileType

                && length
                    < ushort.MaxValue
            )
            {
                length++;

                continue;
            }

            runs.Add(
                new Run(
                    tileType,
                    (ushort)length
                )
            );

            tileType =
                tileTypes[
                    index
                ];

            length = 1;
        }

        runs.Add(
            new Run(
                tileType,
                (ushort)length
            )
        );

        return runs;
    }
}
