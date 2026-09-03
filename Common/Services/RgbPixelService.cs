using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using ImageLoader.Common.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ImageLoader.Common.Services;

internal static class RgbPixelService
{
    private const int MaxSyncEntriesPerPacket =
        2500;

    private readonly record struct SyncEntry(
        ushort X,
        ushort Y,
        Color Color
    );

    private static readonly Dictionary<
        Point,
        Color
    > Colors = new();

    public static int Count =>
        Colors.Count;

    public static IEnumerable<
        KeyValuePair<
            Point,
            Color
        >
    > EnumerateColors()
    {
        return Colors;
    }

    public static void SetColor(
        int x,
        int y,
        Color color
    )
    {
        Colors[
            new Point(
                x,
                y
            )
        ] =
            new Color(
                color.R,
                color.G,
                color.B,
                255
            );
    }

    public static bool TryGetColor(
        int x,
        int y,
        out Color color
    )
    {
        return Colors.TryGetValue(
            new Point(
                x,
                y
            ),
            out color
        );
    }

    public static void RemoveColor(
        int x,
        int y
    )
    {
        Colors.Remove(
            new Point(
                x,
                y
            )
        );
    }

    public static void Clear()
    {
        Colors.Clear();
    }

    public static void RequestFullSync()
    {
        if (
            Main.netMode
            != NetmodeID.MultiplayerClient
        )
        {
            return;
        }

        ModPacket packet =
            global::ImageLoader.ImageLoader
                .Instance
                .GetPacket();

        packet.Write(
            (byte)global::ImageLoader.ImageLoader
                .MessageType
                .RequestRgbSync
        );

        packet.Send();
    }

    public static void SendFullSync(
        int toWho
    )
    {
        if (
            Main.netMode
            != NetmodeID.Server
        )
        {
            return;
        }

        CleanupStaleEntries();

        ModPacket reset =
            global::ImageLoader.ImageLoader
                .Instance
                .GetPacket();

        reset.Write(
            (byte)global::ImageLoader.ImageLoader
                .MessageType
                .RgbSyncReset
        );

        reset.Send(
            toWho
        );

        var entries =
            new List<SyncEntry>(
                Colors.Count
            );

        foreach (
            KeyValuePair<Point, Color> pair
            in Colors
        )
        {
            entries.Add(
                new SyncEntry(
                    (ushort)pair.Key.X,
                    (ushort)pair.Key.Y,
                    pair.Value
                )
            );
        }

        SendEntries(
            entries,
            toWho
        );
    }

    public static void BroadcastRegion(
        int startX,
        int startY,
        int width,
        int height,
        Color[] colors
    )
    {
        if (
            Main.netMode
            != NetmodeID.Server
            || colors is null
        )
        {
            return;
        }

        var entries =
            new List<SyncEntry>(
                width * height
            );

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

                Color color =
                    colors[index];

                if (
                    color.A
                        < ImagePlacementService
                            .TransparencyThreshold
                )
                {
                    continue;
                }

                int worldX =
                    startX + x;

                int worldY =
                    startY + y;

                if (
                    !WorldGen.InWorld(
                        worldX,
                        worldY
                    )
                )
                {
                    continue;
                }

                entries.Add(
                    new SyncEntry(
                        (ushort)worldX,
                        (ushort)worldY,
                        color
                    )
                );
            }
        }

        SendEntries(
            entries,
            -1
        );
    }

    private static void SendEntries(
        List<SyncEntry> entries,
        int toWho
    )
    {
        int offset = 0;

        while (
            offset
            < entries.Count
        )
        {
            int count =
                Math.Min(
                    MaxSyncEntriesPerPacket,
                    entries.Count
                    - offset
                );

            ModPacket packet =
                global::ImageLoader.ImageLoader
                    .Instance
                    .GetPacket(
                        3
                        + count * 7
                    );

            packet.Write(
                (byte)global::ImageLoader.ImageLoader
                    .MessageType
                    .RgbSyncChunk
            );

            packet.Write(
                (ushort)count
            );

            for (
                int i = 0;
                i < count;
                i++
            )
            {
                SyncEntry entry =
                    entries[
                        offset + i
                    ];

                packet.Write(
                    entry.X
                );

                packet.Write(
                    entry.Y
                );

                packet.Write(
                    entry.Color.R
                );

                packet.Write(
                    entry.Color.G
                );

                packet.Write(
                    entry.Color.B
                );
            }

            packet.Send(
                toWho
            );

            offset += count;
        }
    }

    public static void ReceiveReset()
    {
        Colors.Clear();
    }

    public static void ReceiveChunk(
        BinaryReader reader
    )
    {
        int count =
            reader.ReadUInt16();

        if (
            count < 0
            || count
            > MaxSyncEntriesPerPacket
        )
        {
            return;
        }

        for (
            int i = 0;
            i < count;
            i++
        )
        {
            int x =
                reader.ReadUInt16();

            int y =
                reader.ReadUInt16();

            byte red =
                reader.ReadByte();

            byte green =
                reader.ReadByte();

            byte blue =
                reader.ReadByte();

            if (
                !WorldGen.InWorld(
                    x,
                    y
                )
            )
            {
                continue;
            }

            SetColor(
                x,
                y,
                new Color(
                    red,
                    green,
                    blue,
                    255
                )
            );
        }
    }

    internal static byte[] SaveCompressed()
    {
        CleanupStaleEntries();

        using var output =
            new MemoryStream();

        using (
            var compression =
                new DeflateStream(
                    output,
                    CompressionLevel.Fastest,
                    leaveOpen: true
                )
        )
        {
            using var writer =
                new BinaryWriter(
                    compression
                );

            writer.Write(
                Colors.Count
            );

            foreach (
                KeyValuePair<
                    Point,
                    Color
                > pair
                in Colors
            )
            {
                writer.Write(
                    (ushort)pair.Key.X
                );

                writer.Write(
                    (ushort)pair.Key.Y
                );

                writer.Write(
                    pair.Value.R
                );

                writer.Write(
                    pair.Value.G
                );

                writer.Write(
                    pair.Value.B
                );
            }
        }

        return output.ToArray();
    }

    internal static void LoadCompressed(
        byte[] bytes
    )
    {
        Colors.Clear();

        if (
            bytes is null
            || bytes.Length == 0
        )
        {
            return;
        }

        try
        {
            using var input =
                new MemoryStream(
                    bytes,
                    writable: false
                );

            using var compression =
                new DeflateStream(
                    input,
                    CompressionMode.Decompress
                );

            using var reader =
                new BinaryReader(
                    compression
                );

            int count =
                reader.ReadInt32();

            if (
                count < 0
                || count
                > 10_000_000
            )
            {
                throw new InvalidDataException(
                    "Invalid RGB pixel count."
                );
            }

            for (
                int i = 0;
                i < count;
                i++
            )
            {
                int x =
                    reader.ReadUInt16();

                int y =
                    reader.ReadUInt16();

                byte red =
                    reader.ReadByte();

                byte green =
                    reader.ReadByte();

                byte blue =
                    reader.ReadByte();

                if (
                    !WorldGen.InWorld(
                        x,
                        y
                    )
                )
                {
                    continue;
                }

                SetColor(
                    x,
                    y,
                    new Color(
                        red,
                        green,
                        blue,
                        255
                    )
                );
            }
        }
        catch (
            Exception exception
        )
        {
            Colors.Clear();

            global::ImageLoader.ImageLoader
                .Instance?
                .Logger
                .Warn(
                    $"Could not load saved RGB pixels: {exception.Message}"
                );
        }
    }

    private static void CleanupStaleEntries()
    {
        int rgbTileType =
            ModContent.TileType<RgbPixelTile>();

        var stale =
            new List<Point>();

        foreach (
            KeyValuePair<Point, Color> pair
            in Colors
        )
        {
            Point point =
                pair.Key;

            if (
                !WorldGen.InWorld(
                    point.X,
                    point.Y
                )
            )
            {
                stale.Add(
                    point
                );

                continue;
            }

            Tile tile =
                Main.tile[
                    point.X,
                    point.Y
                ];

            if (
                !tile.HasTile
                || tile.TileType
                    != rgbTileType
            )
            {
                stale.Add(
                    point
                );
            }
        }

        foreach (
            Point point
            in stale
        )
        {
            Colors.Remove(
                point
            );
        }
    }
}

public sealed class RgbPixelWorldSystem :
    ModSystem
{
    private const string SaveKey =
        "RgbPixels";

    public override void OnWorldLoad()
    {
        RgbPixelService.Clear();
    }

    public override void OnWorldUnload()
    {
        RgbPixelService.Clear();
    }

    public override void SaveWorldData(
        TagCompound tag
    )
    {
        if (
            RgbPixelService.Count == 0
        )
        {
            return;
        }

        tag[
            SaveKey
        ] =
            RgbPixelService
                .SaveCompressed();
    }

    public override void LoadWorldData(
        TagCompound tag
    )
    {
        if (
            !tag.ContainsKey(
                SaveKey
            )
        )
        {
            RgbPixelService.Clear();

            return;
        }

        RgbPixelService.LoadCompressed(
            tag.GetByteArray(
                SaveKey
            )
        );
    }
}
