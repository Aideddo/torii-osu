// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Overlays;

namespace osu.Game.Beatmaps.Drawables.Cards
{
    public partial class BeatmapCardContentBackground : CompositeDrawable
    {
        public BindableBool Dimmed { get; } = new BindableBool();

        private readonly Box background;
        private readonly DelayedLoadUnloadWrapper cover;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public BeatmapCardContentBackground(IBeatmapSetOnlineInfo onlineInfo, bool keepLoaded = false)
        {
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                cover = new DelayedLoadUnloadWrapper(() => createCover(onlineInfo), keepLoaded ? 0 : 500, keepLoaded ? double.MaxValue : 1000)
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.Transparent
                }
            };
        }

        private static Drawable createCover(IBeatmapSetOnlineInfo onlineInfo) => new OnlineBeatmapSetCover(onlineInfo)
        {
            RelativeSizeAxes = Axes.Both,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            FillMode = FillMode.Fill
        };

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            background.Colour = colourProvider.Background2;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Dimmed.BindValueChanged(_ => Schedule(updateState));

            // Torii: the initial state is applied here and now. Scheduled like later changes, it ran after FinishTransforms() and so
            // animated: every card coming into view spent its first 360 ms fading in a cover that hadn't loaded yet, which with cards
            // cached in frame buffers meant redrawing each of them on every one of those frames.
            updateState();
            FinishTransforms(true);
        }

        private void updateState()
        {
            background.FadeColour(Dimmed.Value ? colourProvider.Background4 : colourProvider.Background2, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);

            var gradient = ColourInfo.GradientHorizontal(Colour4.White.Opacity(0), Colour4.White.Opacity(0.2f));
            cover.FadeColour(gradient, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
        }
    }
}
