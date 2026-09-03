using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace ImageLoader.Common.Services;

internal static class SchematicRecognitionService
{
    private const int BinsPerAxis = 4;

    private const int BinCount =
        BinsPerAxis
        * BinsPerAxis;

    private const int MaximumFramesPerTile = 8;

    private const int MeanShortlistSize = 18;

    // This is intentionally conservative. A flattened screenshot contains
    // background art, walls, furniture and lighting that cannot safely be
    // represented by a single solid tile. Rejecting uncertain cells prevents
    // those pixels from turning into a noisy wall of unrelated blocks.
    private const float MaximumAcceptedScore = 2350f;

    private readonly record struct Profile(
        Vector3 Mean,
        float Variance,
        Vector3[] Bins
    );

    private sealed class TileCandidate
    {
        public readonly ushort TileType;

        public readonly Profile[] Profiles;

        public TileCandidate(
            ushort tileType,
            Profile[] profiles
        )
        {
            TileType =
                tileType;

            Profiles =
                profiles;
        }
    }

    private static TileCandidate[] _candidates;

    public static void Unload()
    {
        _candidates =
            null;
    }

    public static void Convert(
        Color[] sourcePixels,
        int sourceWidth,
        int sourceHeight,
        int outputWidth,
        int outputHeight,
        out ushort[] tileTypes,
        out Color[] previewColors,
        out int recognized,
        out int rejected
    )
    {
        EnsureCandidates();

        tileTypes =
            new ushort[
                outputWidth
                * outputHeight
            ];

        previewColors =
            new Color[
                tileTypes.Length
            ];

        recognized = 0;

        rejected = 0;

        for (
            int outputY = 0;
            outputY < outputHeight;
            outputY++
        )
        {
            for (
                int outputX = 0;
                outputX < outputWidth;
                outputX++
            )
            {
                int index =
                    outputY
                    * outputWidth
                    + outputX;

                Profile source =
                    BuildSourceProfile(
                        sourcePixels,
                        sourceWidth,
                        sourceHeight,
                        outputX,
                        outputY,
                        outputWidth,
                        outputHeight,
                        out float alphaCoverage
                    );

                if (
                    alphaCoverage
                    < ImagePlacementService
                        .TransparencyThreshold
                        / 255f
                )
                {
                    Reject(
                        tileTypes,
                        previewColors,
                        index
                    );

                    rejected++;

                    continue;
                }

                ushort match =
                    FindBestMatch(
                        source,
                        out float score
                    );

                // Flat screenshot regions are usually sky or painted
                // background. Require a tighter match for them because a
                // similarly coloured block is not enough evidence.
                float threshold =
                    source.Variance < 110f
                        ? MaximumAcceptedScore
                            * 0.58f
                        : MaximumAcceptedScore;

                if (
                    match
                        == ImagePlacementService
                            .Transparent
                    || score > threshold
                )
                {
                    Reject(
                        tileTypes,
                        previewColors,
                        index
                    );

                    rejected++;

                    continue;
                }

                tileTypes[index] =
                    match;

                previewColors[index] =
                    ImagePalette
                        .GetMapColor(
                            match
                        );

                recognized++;
            }
        }
    }

    private static void Reject(
        ushort[] tileTypes,
        Color[] previewColors,
        int index
    )
    {
        tileTypes[index] =
            ImagePlacementService
                .Transparent;

        previewColors[index] =
            Color.Transparent;
    }

    private static ushort FindBestMatch(
        Profile source,
        out float bestScore
    )
    {
        if (
            _candidates is null
            || _candidates.Length == 0
        )
        {
            bestScore =
                float.MaxValue;

            return ImagePlacementService
                .Transparent;
        }

        Span<int> shortlist =
            stackalloc int[
                MeanShortlistSize
            ];

        Span<float> shortlistScores =
            stackalloc float[
                MeanShortlistSize
            ];

        shortlist.Fill(
            -1
        );

        shortlistScores.Fill(
            float.MaxValue
        );

        for (
            int candidateIndex = 0;
            candidateIndex < _candidates.Length;
            candidateIndex++
        )
        {
            Profile representative =
                _candidates[
                    candidateIndex
                ]
                .Profiles[0];

            float meanScore =
                BrightnessAdjustedDistance(
                    source.Mean,
                    representative.Mean
                );

            for (
                int slot = 0;
                slot < shortlist.Length;
                slot++
            )
            {
                if (
                    meanScore
                    >= shortlistScores[slot]
                )
                {
                    continue;
                }

                for (
                    int move = shortlist.Length - 1;
                    move > slot;
                    move--
                )
                {
                    shortlist[move] =
                        shortlist[move - 1];

                    shortlistScores[move] =
                        shortlistScores[move - 1];
                }

                shortlist[slot] =
                    candidateIndex;

                shortlistScores[slot] =
                    meanScore;

                break;
            }
        }

        ushort bestTile =
            ImagePlacementService
                .Transparent;

        bestScore =
            float.MaxValue;

        foreach (
            int candidateIndex
            in shortlist
        )
        {
            if (
                candidateIndex < 0
            )
            {
                continue;
            }

            TileCandidate candidate =
                _candidates[
                    candidateIndex
                ];

            foreach (
                Profile profile
                in candidate.Profiles
            )
            {
                float score =
                    CompareProfiles(
                        source,
                        profile
                    );

                if (
                    score >= bestScore
                )
                {
                    continue;
                }

                bestScore =
                    score;

                bestTile =
                    candidate.TileType;
            }
        }

        return bestTile;
    }

    private static float CompareProfiles(
        Profile source,
        Profile candidate
    )
    {
        float sourceBrightness =
            Luminance(
                source.Mean
            );

        float candidateBrightness =
            Math.Max(
                1f,
                Luminance(
                    candidate.Mean
                )
            );

        float brightnessScale =
            MathHelper.Clamp(
                sourceBrightness
                / candidateBrightness,
                0.38f,
                1.8f
            );

        float score = 0f;

        for (
            int index = 0;
            index < BinCount;
            index++
        )
        {
            Vector3 difference =
                source.Bins[index]
                - candidate.Bins[index]
                    * brightnessScale;

            score +=
                difference.LengthSquared()
                / 3f;
        }

        score /=
            BinCount;

        float scaledVariance =
            candidate.Variance
            * brightnessScale
            * brightnessScale;

        score +=
            Math.Abs(
                source.Variance
                - scaledVariance
            )
            * 1.4f;

        return score;
    }

    private static float BrightnessAdjustedDistance(
        Vector3 source,
        Vector3 candidate
    )
    {
        float scale =
            MathHelper.Clamp(
                Luminance(source)
                / Math.Max(
                    1f,
                    Luminance(candidate)
                ),
                0.38f,
                1.8f
            );

        Vector3 difference =
            source
            - candidate
                * scale;

        return difference
            .LengthSquared();
    }

    private static float Luminance(
        Vector3 color
    )
    {
        return color.X * 0.2126f
            + color.Y * 0.7152f
            + color.Z * 0.0722f;
    }

    private static Profile BuildSourceProfile(
        Color[] pixels,
        int sourceWidth,
        int sourceHeight,
        int outputX,
        int outputY,
        int outputWidth,
        int outputHeight,
        out float alphaCoverage
    )
    {
        double left =
            outputX
            * sourceWidth
            / (double)outputWidth;

        double right =
            (outputX + 1)
            * sourceWidth
            / (double)outputWidth;

        double top =
            outputY
            * sourceHeight
            / (double)outputHeight;

        double bottom =
            (outputY + 1)
            * sourceHeight
            / (double)outputHeight;

        var bins =
            new Vector3[
                BinCount
            ];

        var binWeights =
            new float[
                BinCount
            ];

        float alphaWeight = 0f;

        float pixelWeight = 0f;

        for (
            int binY = 0;
            binY < BinsPerAxis;
            binY++
        )
        {
            for (
                int binX = 0;
                binX < BinsPerAxis;
                binX++
            )
            {
                double sampleX =
                    left
                    + (
                        binX + 0.5d
                    )
                    * (
                        right - left
                    )
                    / BinsPerAxis;

                double sampleY =
                    top
                    + (
                        binY + 0.5d
                    )
                    * (
                        bottom - top
                    )
                    / BinsPerAxis;

                int sourceX =
                    Math.Clamp(
                        (int)sampleX,
                        0,
                        sourceWidth - 1
                    );

                int sourceY =
                    Math.Clamp(
                        (int)sampleY,
                        0,
                        sourceHeight - 1
                    );

                Color color =
                    pixels[
                        sourceY
                        * sourceWidth
                        + sourceX
                    ];

                int index =
                    binY
                    * BinsPerAxis
                    + binX;

                float alpha =
                    color.A
                    / 255f;

                bins[index] =
                    new Vector3(
                        color.R,
                        color.G,
                        color.B
                    );

                binWeights[index] =
                    alpha;

                alphaWeight +=
                    alpha;

                pixelWeight +=
                    1f;
            }
        }

        alphaCoverage =
            pixelWeight <= 0f
                ? 0f
                : alphaWeight
                    / pixelWeight;

        return BuildProfile(
            bins,
            binWeights
        );
    }

    private static void EnsureCandidates()
    {
        if (
            _candidates is
            {
                Length: > 0
            }
        )
        {
            return;
        }

        var candidates =
            new List<TileCandidate>();

        foreach (
            ushort tileType
            in ImagePalette
                .GetAllowedTileTypes()
        )
        {
            try
            {
                if (
                    tileType
                    >= TextureAssets.Tile.Length
                )
                {
                    continue;
                }

                Texture2D texture =
                    TextureAssets
                        .Tile[tileType]
                        .Value;

                Color[] texturePixels =
                    new Color[
                        texture.Width
                        * texture.Height
                    ];

                texture.GetData(
                    texturePixels
                );

                var profiles =
                    new List<Profile>();

                for (
                    int frameY = 0;
                    frameY + 16 <= texture.Height
                        && profiles.Count
                            < MaximumFramesPerTile;
                    frameY += 18
                )
                {
                    for (
                        int frameX = 0;
                        frameX + 16 <= texture.Width
                            && profiles.Count
                                < MaximumFramesPerTile;
                        frameX += 18
                    )
                    {
                        if (
                            TryBuildTextureProfile(
                                texturePixels,
                                texture.Width,
                                frameX,
                                frameY,
                                out Profile profile
                            )
                        )
                        {
                            profiles.Add(
                                profile
                            );
                        }
                    }
                }

                if (
                    profiles.Count > 0
                )
                {
                    candidates.Add(
                        new TileCandidate(
                            tileType,
                            profiles.ToArray()
                        )
                    );
                }
            }
            catch
            {
                // A few special or third-party tile assets cannot be read
                // back. They are simply excluded from schematic matching.
            }
        }

        _candidates =
            candidates.ToArray();
    }

    private static bool TryBuildTextureProfile(
        Color[] pixels,
        int textureWidth,
        int frameX,
        int frameY,
        out Profile profile
    )
    {
        var bins =
            new Vector3[
                BinCount
            ];

        var weights =
            new float[
                BinCount
            ];

        for (
            int binY = 0;
            binY < BinsPerAxis;
            binY++
        )
        {
            for (
                int binX = 0;
                binX < BinsPerAxis;
                binX++
            )
            {
                Vector3 total =
                    Vector3.Zero;

                float totalWeight = 0f;

                for (
                    int y = 0;
                    y < 4;
                    y++
                )
                {
                    for (
                        int x = 0;
                        x < 4;
                        x++
                    )
                    {
                        Color color =
                            pixels[
                                (
                                    frameY
                                    + binY * 4
                                    + y
                                )
                                * textureWidth
                                + frameX
                                + binX * 4
                                + x
                            ];

                        float alpha =
                            color.A
                            / 255f;

                        total +=
                            new Vector3(
                                color.R,
                                color.G,
                                color.B
                            )
                            * alpha;

                        totalWeight +=
                            alpha;
                    }
                }

                int index =
                    binY
                    * BinsPerAxis
                    + binX;

                if (
                    totalWeight > 0f
                )
                {
                    bins[index] =
                        total
                        / totalWeight;
                }

                weights[index] =
                    totalWeight
                    / 16f;
            }
        }

        float coverage = 0f;

        foreach (
            float weight
            in weights
        )
        {
            coverage +=
                weight;
        }

        coverage /=
            BinCount;

        if (
            coverage < 0.72f
        )
        {
            profile =
                default;

            return false;
        }

        profile =
            BuildProfile(
                bins,
                weights
            );

        return true;
    }

    private static Profile BuildProfile(
        Vector3[] bins,
        float[] weights
    )
    {
        Vector3 mean =
            Vector3.Zero;

        float totalWeight = 0f;

        for (
            int index = 0;
            index < bins.Length;
            index++
        )
        {
            mean +=
                bins[index]
                * weights[index];

            totalWeight +=
                weights[index];
        }

        if (
            totalWeight > 0f
        )
        {
            mean /=
                totalWeight;
        }

        float variance = 0f;

        for (
            int index = 0;
            index < bins.Length;
            index++
        )
        {
            Vector3 difference =
                bins[index]
                - mean;

            variance +=
                difference.LengthSquared()
                / 3f
                * weights[index];
        }

        if (
            totalWeight > 0f
        )
        {
            variance /=
                totalWeight;
        }

        return new Profile(
            mean,
            variance,
            bins
        );
    }
}
