using System.Collections.Generic;
using ImageLoader.Common.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;

namespace ImageLoader.Common.Services;

internal static class ImagePalette
{
    private readonly record struct Entry(
        ushort TileType,
        Color Color
    );

    private static Entry[] _entries;

    public static void Unload()
    {
        _entries = null;
    }

    public static ushort FindClosestTile(
        Color source,
        out Color matchedColor
    )
    {
        EnsureBuilt();

        long bestDistance =
            long.MaxValue;

        Entry best =
            _entries[0];

        foreach (
            Entry entry
            in _entries
        )
        {
            long distance =
                PerceptualDistance(
                    source,
                    entry.Color
                );

            if (
                distance
                >= bestDistance
            )
            {
                continue;
            }

            bestDistance =
                distance;

            best =
                entry;

            if (
                distance == 0
            )
            {
                break;
            }
        }

        matchedColor =
            best.Color;

        return best.TileType;
    }

    public static bool IsAllowedBlock(
        int tileType
    )
    {
        if (
            tileType
            == ModContent.TileType<
                RgbPixelTile
            >()
        )
        {
            return false;
        }

        return tileType >= 0
            && tileType
                < TileLoader.TileCount

            && tileType
                < Main.tileSolid.Length

            && tileType
                < Main.tileFrameImportant.Length

            && Main.tileSolid[
                tileType
            ]

            && !Main.tileFrameImportant[
                tileType
            ]

            && (
                tileType
                    >= TileID.Sets.Platforms.Length

                || !TileID.Sets.Platforms[
                    tileType
                ]
            )

            && (
                tileType
                    >= TileID.Sets.Falling.Length

                || !TileID.Sets.Falling[
                    tileType
                ]
            );
    }

    public static ushort[] GetAllowedTileTypes()
    {
        EnsureBuilt();

        var tileTypes =
            new ushort[
                _entries.Length
            ];

        for (
            int index = 0;
            index < _entries.Length;
            index++
        )
        {
            tileTypes[index] =
                _entries[index]
                    .TileType;
        }

        return tileTypes;
    }

    public static Color GetMapColor(
        ushort tileType
    )
    {
        EnsureBuilt();

        foreach (
            Entry entry
            in _entries
        )
        {
            if (
                entry.TileType
                == tileType
            )
            {
                return entry.Color;
            }
        }

        return Color.Gray;
    }

    private static void EnsureBuilt()
    {
        if (
            _entries is
            {
                Length: > 0
            }
        )
        {
            return;
        }

        var entries =
            new List<Entry>();

        for (
            int tileType = 0;
            tileType
            < TileLoader.TileCount;
            tileType++
        )
        {
            if (
                !IsAllowedBlock(
                    tileType
                )
            )
            {
                continue;
            }

            try
            {
                int lookup =
                    MapHelper.TileToLookup(
                        tileType,
                        0
                    );

                if (
                    lookup <= 0
                    || lookup
                    > ushort.MaxValue
                )
                {
                    continue;
                }

                MapTile mapTile =
                    MapTile.Create(
                        (ushort)lookup,
                        byte.MaxValue,
                        0
                    );

                Color color =
                    MapHelper
                        .GetMapTileXnaColor(
                            ref mapTile
                        );

                if (
                    color.A > 0
                )
                {
                    entries.Add(
                        new Entry(
                            (ushort)tileType,
                            color
                        )
                    );
                }
            }
            catch
            {
                // Some special or third-party tiles
                // do not expose a usable map entry.
            }
        }

        if (
            entries.Count == 0
        )
        {
            entries.Add(
                new Entry(
                    TileID.Stone,
                    new Color(
                        120,
                        120,
                        120
                    )
                )
            );
        }

        _entries =
            entries.ToArray();
    }

    private static long PerceptualDistance(
        Color left,
        Color right
    )
    {
        int redMean =
            (
                left.R
                + right.R
            )
            / 2;

        int red =
            left.R
            - right.R;

        int green =
            left.G
            - right.G;

        int blue =
            left.B
            - right.B;

        return
            (
                (
                    512L
                    + redMean
                )
                * red
                * red
                >> 8
            )
            + 4L
                * green
                * green
            + (
                (
                    767L
                    - redMean
                )
                * blue
                * blue
                >> 8
            );
    }
}
