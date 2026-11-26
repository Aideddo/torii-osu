// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Beatmaps.Drawables.Cards
{
    /// <summary>
    /// Torii: the place of a <see cref="BeatmapCard"/> in a long list, which only holds the card while it's on screen (or was a moment
    /// ago) and a plain placeholder otherwise.
    /// </summary>
    /// <remarks>
    /// A card is about a megabyte of drawables, bindables and draw nodes. A list that creates one for everything it has ever scrolled
    /// past, as flinging through search results does at dozens per second, keeps the garbage collector busy for as long as it scrolls
    /// and leaves it more to walk on every collection after. Here a card is only created once its slot has stayed in view for a
    /// moment, and is let go some time after scrolling out.
    /// </remarks>
    public partial class BeatmapCardSlot : CompositeDrawable
    {
        /// <summary>
        /// How long a slot has to be in view before its card is created: long enough for a fling to pass by.
        /// </summary>
        private const double time_before_load = 200;

        private const double time_before_unload = 4000;

        public readonly APIBeatmapSet BeatmapSet;

        private readonly Box placeholder;
        private readonly DelayedLoadUnloadWrapper wrapper;

        public BeatmapCardSlot(APIBeatmapSet beatmapSet, BeatmapCardSize size)
        {
            BeatmapSet = beatmapSet;
            Size = SizeFor(size);

            InternalChildren = new Drawable[]
            {
                placeholder = new Box { RelativeSizeAxes = Axes.Both },
                wrapper = new DelayedLoadUnloadWrapper(() => BeatmapCard.Create(beatmapSet, size), time_before_load, time_before_unload)
                {
                    RelativeSizeAxes = Axes.Both,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            placeholder.Colour = colourProvider.Background3;
        }

        protected override void Update()
        {
            base.Update();

            // The card has rounded corners, which the placeholder would show behind.
            placeholder.Alpha = wrapper.DelayedLoadCompleted ? 0 : 1;
        }

        public static Vector2 SizeFor(BeatmapCardSize size)
        {
            switch (size)
            {
                case BeatmapCardSize.Nano:
                    return new Vector2(BeatmapCardNano.WIDTH, BeatmapCardNano.HEIGHT);

                case BeatmapCardSize.Extra:
                    return new Vector2(BeatmapCard.WIDTH, BeatmapCardExtra.HEIGHT);

                default:
                    return new Vector2(BeatmapCard.WIDTH, BeatmapCardNormal.HEIGHT);
            }
        }
    }
}
