// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps.Timing;
using osu.Game.Graphics;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.Ghost;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// torii: the ghost's cursor, shown only while the map is on a break.
    /// </summary>
    /// <remarks>
    /// Restricting it to breaks is what makes this free of consequences rather than a
    /// balance question: there is nothing to hit during a break, so watching where the
    /// opponent's hand went cannot help you hit anything. What it does give you is the
    /// bit that makes a race feel like a race, seeing the other player idling, drifting
    /// back to the centre, or already moving early for the next pattern.
    /// </remarks>
    public partial class GhostCursor : CompositeDrawable
    {
        private const float visible_alpha = 0.4f;
        private const double fade_duration = 300;

        [Resolved(CanBeNull = true)]
        private GhostRaceState? ghostRace { get; set; }

        [Resolved(CanBeNull = true)]
        private GameplayState? gameplayState { get; set; }

        [Resolved(CanBeNull = true)]
        private IGameplayClock? gameplayClock { get; set; }

        private readonly List<OsuReplayFrame> frames = new List<OsuReplayFrame>();
        private double[] frameTimes = Array.Empty<double>();
        private IReadOnlyList<BreakPeriod> breaks = Array.Empty<BreakPeriod>();

        private Container cursor = null!;
        private bool wasVisible;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = cursor = new Container
            {
                Origin = Anchor.Centre,
                Alpha = 0,
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Circle
                    {
                        Origin = Anchor.Centre,
                        Anchor = Anchor.Centre,
                        Size = new Vector2(18),
                        Colour = Color4.White.Opacity(0.35f),
                    },
                    new CircularContainer
                    {
                        Origin = Anchor.Centre,
                        Anchor = Anchor.Centre,
                        Size = new Vector2(18),
                        Masking = true,
                        BorderThickness = 2,
                        BorderColour = colours.PinkLighter,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                            AlwaysPresent = true,
                        },
                    },
                },
            };

            if (ghostRace?.GhostReplay == null)
                return;

            // Only osu! frames carry a cursor position. Anything else in the replay is
            // not something this can draw, so it is filtered out once here rather than
            // being type-checked every frame.
            frames.AddRange(ghostRace.GhostReplay!.Frames.OfType<OsuReplayFrame>().OrderBy(f => f.Time));
            frameTimes = frames.Select(f => f.Time).ToArray();

            breaks = gameplayState?.Beatmap.Breaks ?? (IReadOnlyList<BreakPeriod>)Array.Empty<BreakPeriod>();
        }

        protected override void Update()
        {
            base.Update();

            if (frames.Count == 0 || gameplayClock == null)
                return;

            double time = gameplayClock.CurrentTime;

            bool shouldBeVisible = breaks.Any(b => time >= b.StartTime && time <= b.EndTime);

            if (shouldBeVisible != wasVisible)
            {
                cursor.FadeTo(shouldBeVisible ? visible_alpha : 0, fade_duration, Easing.OutQuint);
                wasVisible = shouldBeVisible;
            }

            // Keep tracking the position even while hidden, so the cursor fades in
            // where the ghost actually is instead of sliding in from wherever it was
            // when the last break ended.
            cursor.Position = positionAt(time);
        }

        private Vector2 positionAt(double time)
        {
            int i = Array.BinarySearch(frameTimes, time);

            if (i >= 0)
                return frames[i].Position;

            i = ~i;

            if (i == 0)
                return frames[0].Position;
            if (i >= frames.Count)
                return frames[^1].Position;

            var previous = frames[i - 1];
            var next = frames[i];

            double span = next.Time - previous.Time;

            if (span <= 0)
                return next.Position;

            return Vector2.Lerp(previous.Position, next.Position, (float)((time - previous.Time) / span));
        }
    }
}
