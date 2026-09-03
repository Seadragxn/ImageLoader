using System;
using System.Collections.Generic;
using System.IO;
using ImageLoader.Common.Config;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;
using Terraria.GameContent.Generation;

namespace ImageLoader.Common.Systems;

public sealed class VoidWorldSystem : ModSystem
{
    private const string VoidWorldSaveKey =
        "ImageLoaderVoidWorld";

    public static bool IsVoidWorld
    {
        get;
        private set;
    }

    public override void PreWorldGen()
    {
        IsVoidWorld =
            ModContent
                .GetInstance<
                    ImageLoaderConfig
                >()
                .WorldGenerationMode

            == ImageLoaderWorldGenerationMode
                .VoidGallery;
    }

    public override void ModifyWorldGenTasks(
        List<GenPass> tasks,
        ref double totalWeight
    )
    {
        if (
            ModContent
                .GetInstance<
                    ImageLoaderConfig
                >()
                .WorldGenerationMode

            != ImageLoaderWorldGenerationMode
                .VoidGallery
        )
        {
            return;
        }

        foreach (
            GenPass pass
            in tasks
        )
        {
            pass.Disable();
        }

        tasks.Add(
            new PassLegacy(
                "Image Loader: Void Gallery",
                (
                    progress,
                    configuration
                ) =>
                {
                    progress.Message =
                        "Creating Image Loader void gallery";

                    for (
                        int worldX = 0;
                        worldX < Main.maxTilesX;
                        worldX++
                    )
                    {
                        for (
                            int worldY = 0;
                            worldY < Main.maxTilesY;
                            worldY++
                        )
                        {
                            Main.tile[
                                worldX,
                                worldY
                            ]
                            .ClearEverything();
                        }

                        progress.Value =
                            worldX
                            / (double)Main.maxTilesX;
                    }

                    ApplyVoidWorldPresentation();

                    IsVoidWorld =
                        true;

                    progress.Value =
                        1d;
                }
            )
        );

        totalWeight =
            1d;
    }

    public override void SaveWorldData(
        TagCompound tag
    )
    {
        if (
            IsVoidWorld
        )
        {
            tag[
                VoidWorldSaveKey
            ] = true;
        }
    }

    public override void LoadWorldData(
        TagCompound tag
    )
    {
        IsVoidWorld =
            tag.GetBool(
                VoidWorldSaveKey
            );

        if (
            IsVoidWorld
        )
        {
            ApplyVoidWorldPresentation();
        }
    }

    public override void NetSend(
        BinaryWriter writer
    )
    {
        writer.Write(
            IsVoidWorld
        );
    }

    public override void NetReceive(
        BinaryReader reader
    )
    {
        IsVoidWorld =
            reader.ReadBoolean();

        if (
            IsVoidWorld
        )
        {
            ApplyVoidWorldPresentation();
        }
    }

    public override void PostUpdateWorld()
    {
        if (
            !IsVoidWorld
        )
        {
            return;
        }

        Main.dayTime =
            true;

        Main.time =
            27000d;

        Main.raining =
            false;

        Main.rainTime =
            0d;

        Main.maxRaining =
            0f;

        Main.bloodMoon =
            false;

        Main.eclipse =
            false;
    }

    public override void ModifySunLightColor(
        ref Color tileColor,
        ref Color backgroundColor
    )
    {
        if (
            !IsVoidWorld
        )
        {
            return;
        }

        tileColor =
            Color.White;

        backgroundColor =
            Color.White;
    }

    public override void OnWorldUnload()
    {
        IsVoidWorld =
            false;
    }

    private static void ApplyVoidWorldPresentation()
    {
        Main.worldSurface =
            Math.Max(
                1d,
                Main.maxTilesY
                - 350d
            );

        Main.rockLayer =
            Math.Max(
                Main.worldSurface + 1d,
                Main.maxTilesY
                - 200d
            );

        Main.spawnTileX =
            Main.maxTilesX
            / 2;

        Main.spawnTileY =
            Main.maxTilesY
            / 2;

        Main.dungeonX =
            Main.spawnTileX;

        Main.dungeonY =
            Main.spawnTileY;
    }
}
