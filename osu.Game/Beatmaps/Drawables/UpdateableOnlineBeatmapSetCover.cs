// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Performance;

namespace osu.Game.Beatmaps.Drawables
{
    public partial class UpdateableOnlineBeatmapSetCover : ModelBackedDrawable<IBeatmapSetOnlineInfo>
    {
        private readonly BeatmapSetCoverType coverType;

        public IBeatmapSetOnlineInfo OnlineInfo
        {
            get => Model;
            set => Model = value;
        }

        public new bool Masking
        {
            get => base.Masking;
            set => base.Masking = value;
        }

        protected override double LoadDelay { get; }

        private readonly double timeBeforeUnload;

        // Torii: no fade with the lightweight interface. Covers sit in cards cached in frame buffers, which a fade has redrawn on every
        // frame it lasts: with a screen of cards coming into view, that was a dozen buffers redrawn per frame.
        protected override double TransformDuration => LightweightInterface.Active ? 0 : 400;

        public UpdateableOnlineBeatmapSetCover(BeatmapSetCoverType coverType = BeatmapSetCoverType.Cover, double timeBeforeLoad = 500, double timeBeforeUnload = 1000)
        {
            LoadDelay = timeBeforeLoad;
            this.timeBeforeUnload = timeBeforeUnload;

            this.coverType = coverType;

            InternalChild = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = OsuColour.Gray(0.2f),
            };
        }

        protected override DelayedLoadWrapper CreateDelayedLoadWrapper(Func<Drawable> createContentFunc, double timeBeforeLoad)
            => new DelayedLoadUnloadWrapper(createContentFunc, timeBeforeLoad, timeBeforeUnload)
            {
                RelativeSizeAxes = Axes.Both,
            };

        protected override Drawable CreateDrawable(IBeatmapSetOnlineInfo model)
        {
            if (model == null)
                return null;

            return new OnlineBeatmapSetCover(model, coverType)
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                FillMode = FillMode.Fill,
            };
        }
    }
}
