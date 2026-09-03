using System.IO;
using ImageLoader.Common.Config;
using ImageLoader.Common.Services;
using ImageLoader.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace ImageLoader.Common.Players;

public sealed class GalleryPlayer : ModPlayer
{
    public const float MinimumGalleryZoom =
        0.25f;

    public const float MaximumGalleryZoom =
        4f;

    private const float NormalSpeed =
        10f;

    private const float FastSpeed =
        24f;

    private const float PrecisionSpeed =
        4f;

    public bool GalleryMode
    {
        get;
        private set;
    }

    public float GalleryZoom
    {
        get;
        private set;
    } = 1f;

    private float _zoomBeforeGallery =
        1f;

    private Vector2 _galleryPosition;

    private Vector2 _returnPosition;

    private bool _positionInitialized;

    public override void OnEnterWorld()
    {
        if (
            Main.netMode
            == NetmodeID.MultiplayerClient
        )
        {
            RgbPixelService
                .RequestFullSync();
        }

        if (
            VoidWorldSystem.IsVoidWorld
        )
        {
            if (
                ModContent
                    .GetInstance<
                        ImageLoaderConfig
                    >()
                    .EnableGalleryMode
            )
            {
                SetGalleryMode(
                    true
                );
            }
            else if (
                Player.whoAmI
                == Main.myPlayer
            )
            {
                Main.NewText(
                    "This is a Void Gallery world, but Gallery Mode is disabled in Image Loader's configuration.",
                    Color.OrangeRed
                );
            }
        }
    }

    public override void ProcessTriggers(
        TriggersSet triggersSet
    )
    {
        if (
            global::ImageLoader.ImageLoader
                .ToggleGalleryKeybind?
                .JustPressed
            == true
        )
        {
            ToggleGalleryMode();
        }

        if (
            !GalleryMode
            || Player.whoAmI
                != Main.myPlayer
        )
        {
            return;
        }

        if (
            global::ImageLoader.ImageLoader
                .ZoomInKeybind?
                .JustPressed
            == true
        )
        {
            ChangeZoom(
                0.1f
            );
        }

        if (
            global::ImageLoader.ImageLoader
                .ZoomOutKeybind?
                .JustPressed
            == true
        )
        {
            ChangeZoom(
                -0.1f
            );
        }

        bool controlHeld =
            Main.keyState.IsKeyDown(
                Keys.LeftControl
            )
            || Main.keyState.IsKeyDown(
                Keys.RightControl
            );

        if (
            controlHeld
            && !UIHandler.IsMenuOpen
            && !PlayerInput.WritingText
        )
        {
            PlayerInput.LockVanillaMouseScroll(
                "ImageLoader: Gallery Zoom"
            );

            int scroll =
                PlayerInput
                    .ScrollWheelDeltaForUI;

            if (
                scroll > 0
            )
            {
                ChangeZoom(
                    0.1f
                );
            }
            else if (
                scroll < 0
            )
            {
                ChangeZoom(
                    -0.1f
                );
            }

            if (
                scroll != 0
            )
            {
                PlayerInput.ScrollWheelDelta =
                    0;

                PlayerInput.ScrollWheelDeltaForUI =
                    0;
            }
        }
    }

    public void ToggleGalleryMode()
    {
        SetGalleryMode(
            !GalleryMode
        );
    }

    public void SetGalleryMode(
        bool enabled,
        bool sendNetworkUpdate = true
    )
    {
        if (
            enabled
            && !ModContent
                .GetInstance<
                    ImageLoaderConfig
                >()
                .EnableGalleryMode
        )
        {
            if (
                Player.whoAmI
                == Main.myPlayer
            )
            {
                Main.NewText(
                    "Gallery Mode is disabled in Image Loader's configuration.",
                    Color.OrangeRed
                );
            }

            return;
        }

        if (
            GalleryMode
            == enabled
        )
        {
            return;
        }

        GalleryMode =
            enabled;

        if (
            Player.whoAmI
            == Main.myPlayer
        )
        {
            if (
                enabled
            )
            {
                _returnPosition =
                    Player.position;

                _galleryPosition =
                    Player.position;

                _positionInitialized =
                    true;

                Player.velocity =
                    Vector2.Zero;

                _zoomBeforeGallery =
                    Main.GameZoomTarget;

                GalleryZoom =
                    MathHelper.Clamp(
                        Main.GameZoomTarget,
                        MinimumZoom,
                        MaximumZoom
                    );

                Main.NewText(
                    "Gallery Mode ON | noclip WASD | Shift fast | Ctrl precise | Ctrl+wheel or PageUp/PageDown zoom | G exit",
                    Color.LightBlue
                );
            }
            else
            {
                if (
                    _positionInitialized
                )
                {
                    Player.position =
                        _returnPosition;

                    Player.velocity =
                        Vector2.Zero;

                    _positionInitialized =
                        false;
                }

                Main.GameZoomTarget =
                    _zoomBeforeGallery;

                Main.NewText(
                    "Gallery Mode OFF",
                    Color.LightGray
                );
            }
        }

        if (
            sendNetworkUpdate

            && Main.netMode
                == NetmodeID
                    .MultiplayerClient

            && Player.whoAmI
                == Main.myPlayer
        )
        {
            SendClientState();
        }
    }

    private float MinimumZoom
    {
        get
        {
            return MinimumGalleryZoom;
        }
    }

    private float MaximumZoom
    {
        get
        {
            return MaximumGalleryZoom;
        }
    }

    private void ChangeZoom(
        float amount
    )
    {
        GalleryZoom =
            MathHelper.Clamp(
                GalleryZoom
                    + amount,

                MinimumZoom,

                MaximumZoom
            );

        Main.GameZoomTarget =
            GalleryZoom;
    }

    public override void SetControls()
    {
        if (
            !GalleryMode
            || Player.whoAmI
                != Main.myPlayer
        )
        {
            return;
        }

        Player.controlUseItem =
            false;

        Player.controlUseTile =
            false;

        Player.controlJump =
            false;

        Player.controlHook =
            false;

        Player.controlMount =
            false;

        Player.controlLeft =
            false;

        Player.controlRight =
            false;

        Player.controlUp =
            false;

        Player.controlDown =
            false;
    }

    public override void PreUpdateMovement()
    {
        if (
            !GalleryMode
            || Player.whoAmI
                != Main.myPlayer
        )
        {
            return;
        }

        if (
            !_positionInitialized
        )
        {
            _galleryPosition =
                Player.position;

            _returnPosition =
                Player.position;

            _positionInitialized =
                true;
        }

        Vector2 movement =
            Vector2.Zero;

        if (
            Main.keyState.IsKeyDown(
                Keys.A
            )
        )
        {
            movement.X -=
                1f;
        }

        if (
            Main.keyState.IsKeyDown(
                Keys.D
            )
        )
        {
            movement.X +=
                1f;
        }

        if (
            Main.keyState.IsKeyDown(
                Keys.W
            )
        )
        {
            movement.Y -=
                1f;
        }

        if (
            Main.keyState.IsKeyDown(
                Keys.S
            )
        )
        {
            movement.Y +=
                1f;
        }

        float speed =
            NormalSpeed;

        bool fast =
            Main.keyState.IsKeyDown(
                Keys.LeftShift
            )
            || Main.keyState.IsKeyDown(
                Keys.RightShift
            );

        bool precise =
            Main.keyState.IsKeyDown(
                Keys.LeftControl
            )
            || Main.keyState.IsKeyDown(
                Keys.RightControl
            );

        if (
            fast
        )
        {
            speed =
                FastSpeed;
        }
        else if (
            precise
        )
        {
            speed =
                PrecisionSpeed;
        }

        if (
            movement
            != Vector2.Zero
        )
        {
            movement.Normalize();
        }

        _galleryPosition +=
            movement
            * speed;

        float minimumX =
            16f * 5f;

        float maximumX =
            16f
            * (
                Main.maxTilesX
                - 5
            )
            - Player.width;

        float minimumY =
            16f * 5f;

        float maximumY =
            16f
            * (
                Main.maxTilesY
                - 5
            )
            - Player.height;

        _galleryPosition.X =
            MathHelper.Clamp(
                _galleryPosition.X,
                minimumX,
                maximumX
            );

        _galleryPosition.Y =
            MathHelper.Clamp(
                _galleryPosition.Y,
                minimumY,
                maximumY
            );

        Player.position =
            _galleryPosition;

        Player.velocity =
            Vector2.Zero;

        Player.gravity =
            0f;

        Player.maxFallSpeed =
            0f;

        Player.fallStart =
            (int)(
                Player.position.Y
                / 16f
            );
    }

    public override void PostUpdate()
    {
        if (
            !GalleryMode
        )
        {
            return;
        }

        Player.noFallDmg =
            true;

        Player.lavaImmune =
            true;

        Player.fireWalk =
            true;

        Player.immune =
            true;

        Player.immuneTime =
            2;

        Player.statLife =
            Player.statLifeMax2;

        Player.breath =
            Player.breathMax;

        if (
            Player.whoAmI
            == Main.myPlayer
        )
        {
            if (
                _positionInitialized
            )
            {
                Player.position =
                    _galleryPosition;

                Player.velocity =
                    Vector2.Zero;

                Player.gravity =
                    0f;

                Player.maxFallSpeed =
                    0f;
            }

            Main.GameZoomTarget =
                GalleryZoom;
        }
    }

    public override bool PreItemCheck()
    {
        return !GalleryMode;
    }

    public override bool ImmuneTo(
        PlayerDeathReason damageSource,
        int cooldownCounter,
        bool dodgeable
    )
    {
        return GalleryMode;
    }

    public override bool PreKill(
        double damage,
        int hitDirection,
        bool pvp,
        ref bool playSound,
        ref bool genDust,
        ref PlayerDeathReason damageSource
    )
    {
        if (
            !GalleryMode
        )
        {
            return true;
        }

        Player.statLife =
            Player.statLifeMax2;

        playSound =
            false;

        genDust =
            false;

        return false;
    }

    public override void ModifyDrawInfo(
        ref PlayerDrawSet drawInfo
    )
    {
        if (
            GalleryMode
        )
        {
            drawInfo.hideEntirePlayer =
                true;
        }
    }

    public override void SyncPlayer(
        int toWho,
        int fromWho,
        bool newPlayer
    )
    {
        if (
            Main.netMode
            != NetmodeID.Server
        )
        {
            return;
        }

        SendServerState(
            Player.whoAmI,
            GalleryMode,
            toWho,
            fromWho
        );
    }

    private void SendClientState()
    {
        ModPacket packet =
            global::ImageLoader.ImageLoader
                .Instance
                .GetPacket();

        packet.Write(
            (byte)global::ImageLoader.ImageLoader
                .MessageType
                .GalleryState
        );

        packet.Write(
            GalleryMode
        );

        packet.Send();
    }

    internal static void ReceiveClientState(
        BinaryReader reader,
        int whoAmI
    )
    {
        if (
            whoAmI < 0
            || whoAmI
                >= Main.maxPlayers
            || !Main.player[
                whoAmI
            ].active
        )
        {
            return;
        }

        bool requested =
            reader.ReadBoolean();

        bool enabled =
            requested

            && ModContent
                .GetInstance<
                    ImageLoaderConfig
                >()
                .EnableGalleryMode;

        GalleryPlayer gallery =
            Main.player[
                whoAmI
            ]
            .GetModPlayer<
                GalleryPlayer
            >();

        gallery.GalleryMode =
            enabled;

        SendServerState(
            whoAmI,
            enabled,
            -1,
            whoAmI
        );
    }

    internal static void ReceiveServerState(
        BinaryReader reader
    )
    {
        int playerIndex =
            reader.ReadByte();

        bool enabled =
            reader.ReadBoolean();

        if (
            playerIndex < 0
            || playerIndex
                >= Main.maxPlayers
        )
        {
            return;
        }

        Main.player[
            playerIndex
        ]
        .GetModPlayer<
            GalleryPlayer
        >()
        .GalleryMode =
            enabled;
    }

    private static void SendServerState(
        int playerIndex,
        bool enabled,
        int toWho,
        int ignoreWho
    )
    {
        ModPacket packet =
            global::ImageLoader.ImageLoader
                .Instance
                .GetPacket();

        packet.Write(
            (byte)global::ImageLoader.ImageLoader
                .MessageType
                .GalleryState
        );

        packet.Write(
            (byte)playerIndex
        );

        packet.Write(
            enabled
        );

        packet.Send(
            toWho,
            ignoreWho
        );
    }
}
