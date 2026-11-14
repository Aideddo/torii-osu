// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Platform.OpenXR;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: what the game's desktop window shows while the game itself is in the headset: that VR mode is on, over the current
    /// beatmap's background, and how to leave it. Drawn into the window rather than the headset (see
    /// <see cref="OpenXRHostController.DESKTOP_VIEW"/>); the window keeps taking keyboard, mouse and tablet input for the game.
    /// While the headset isn't connected yet it says that the game is waiting for it instead, and Escape goes back out of VR mode.
    /// </summary>
    public partial class VRDesktopView : RenderTargetRedirectContainer
    {
        // Laid out over the whole game, but not part of it: only shown in the window.
        public override bool HandlePositionalInput => false;
        // Other than Escape while waiting for the headset, the only thing to be seen then.
        public override bool HandleNonPositionalInput => waiting == true;

        [Resolved]
        private IVRModeControl vrMode { get; set; } = null!;

        private OpenXRHostController? openXR;

        private OsuSpriteText title = null!;
        private OsuSpriteText description = null!;
        private OsuSpriteText inputHint = null!;
        private OsuSpriteText leaveHint = null!;

        private bool? waiting;

        private Container backgroundContainer = null!;
        private BeatmapBackground? background;

        private IBindable<WorkingBeatmap> beatmap = null!;

        [BackgroundDependencyLoader]
        private void load(GameHost host, IBindable<WorkingBeatmap> beatmap, OsuColour colours)
        {
            this.beatmap = beatmap.GetBoundCopy();

            RelativeSizeAxes = Axes.Both;
            Target = host.GetExternalRenderTarget(OpenXRHostController.DESKTOP_VIEW);
            openXR = (host as IOpenXRHost)?.OpenXR;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                },
                backgroundContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = OsuColour.Gray(0.35f),
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 14),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Icon = FontAwesome.Solid.VrCardboard,
                            Size = new Vector2(110),
                        },
                        title = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = OsuFont.Torus.With(size: 72, weight: FontWeight.SemiBold),
                        },
                        description = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = OsuFont.Torus.With(size: 34),
                        },
                        inputHint = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = "Keyboard, mouse and tablet input still work while this window is focused.",
                            Font = OsuFont.Torus.With(size: 28),
                            Colour = OsuColour.Gray(0.8f),
                        },
                        leaveHint = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Margin = new MarginPadding { Top = 30 },
                            Font = OsuFont.Torus.With(size: 28, weight: FontWeight.SemiBold),
                            Colour = colours.Yellow,
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            beatmap.BindValueChanged(b => showBackground(b.NewValue), true);
        }

        protected override void Update()
        {
            base.Update();

            bool nowWaiting = openXR?.WaitingForHeadset == true;

            if (nowWaiting == waiting)
                return;

            waiting = nowWaiting;

            title.Text = nowWaiting ? "Waiting for your headset" : "VR mode is enabled";
            description.Text = nowWaiting ? "Connect your headset to SteamVR: the game will be shown in it." : "The game is shown in your headset.";
            inputHint.Alpha = nowWaiting ? 0 : 1;
            leaveHint.Text = nowWaiting
                ? "Press Escape to go back to playing on the desktop."
                : "To leave VR mode, close SteamVR (or use Settings > VR in the headset).";
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (waiting == true && e.Key == Key.Escape && !e.Repeat)
            {
                vrMode.Disable();
                return true;
            }

            return base.OnKeyDown(e);
        }

        private void showBackground(WorkingBeatmap working)
        {
            LoadComponentAsync(new BeatmapBackground(working)
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            }, loaded =>
            {
                // A later beatmap's background may have loaded first.
                if (loaded.Beatmap != beatmap.Value)
                {
                    loaded.Dispose();
                    return;
                }

                background?.FadeOut(400).Expire();
                backgroundContainer.Add(background = loaded);
                loaded.FadeInFromZero(400);
            });
        }
    }
}
