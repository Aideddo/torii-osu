// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Beatmaps.Drawables.Cards
{
    public partial class BeatmapCardContent : CompositeDrawable
    {
        public Drawable MainContent
        {
            set => bodyMask.Child = value;
        }

        /// <remarks>
        /// Torii: only loaded once the card is first expanded. It's a row per difficulty that most cards never show, and loading it with
        /// every card was a good part of what a page of results costs to create (and of what the GC then has to walk).
        /// </remarks>
        public Drawable ExpandedContent
        {
            set => pendingExpandedContent = value;
        }

        private Drawable? pendingExpandedContent;

        public IBindable<bool> Expanded => expanded;

        private readonly BindableBool expanded = new BindableBool();

        private readonly Box background;
        private readonly Container content;
        private readonly Container bodyMask;
        private readonly BufferedContainer bodyBuffer;
        private readonly Container dropdownContent;
        private readonly OsuScrollContainer dropdownScroll;
        private readonly Container borderContainer;

        public BeatmapCardContent(float height)
        {
            RelativeSizeAxes = Axes.X;
            Height = height;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            InternalChild = content = new HoverHandlingContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                CornerRadius = BeatmapCard.CORNER_RADIUS,
                Masking = true,
                Unhovered = _ => updateFromHoverChange(),
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both
                    },
                    // Torii: the body is drawn into a frame buffer and redrawn only when something in it changes. A card is made of
                    // several masked containers, sprites and textures, each of which splits draw batches: without this a listing
                    // full of cards costs ~16 draw calls per card, every frame. The rounded corners are masked inside the buffer,
                    // so drawing the cached card needs no masking at all.
                    bodyBuffer = new BufferedContainer(cachedFrameBuffer: true)
                    {
                        // Until let in, see Update().
                        Alpha = 0,
                        RelativeSizeAxes = Axes.X,
                        Height = height,
                        TrackSubtreeChanges = true,
                        // Listings keep every card they've shown: don't keep buffers for those scrolled out of view.
                        ReleaseFrameBufferWhenNotDrawn = true,
                        Child = bodyMask = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            CornerRadius = BeatmapCard.CORNER_RADIUS,
                            Masking = true,
                        },
                    },
                    dropdownContent = new HoverHandlingContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Margin = new MarginPadding { Top = height },
                        Alpha = 0,
                        Hovered = _ =>
                        {
                            updateFromHoverChange();
                            return true;
                        },
                        Unhovered = _ => updateFromHoverChange(),
                        Child = dropdownScroll = new ExpandedContentScrollContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            ScrollbarVisible = false
                        }
                    },
                    borderContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        CornerRadius = BeatmapCard.CORNER_RADIUS,
                        Masking = true,
                        BorderThickness = 3,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                            AlwaysPresent = true
                        }
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            background.Colour = colourProvider.Background2;
            borderContainer.BorderColour = colourProvider.Highlight1;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Expanded.BindValueChanged(_ => updateState(), true);
            FinishTransforms(true);

            inputManager = GetContainingInputManager();
            this.FindClosestParent<BeatmapCard>()?.BindDisplayedStateChanged(Wake);

            if (!Expanded.Value)
            {
                scheduledUnmask?.Cancel();
                unmask();
            }
        }

        #region Torii: sleeping while nothing changes

        // A card that is only being looked at (or scrolled past) doesn't need its hundred-odd drawables updated, positioned and turned
        // into draw nodes every frame: once it has settled, its body is frozen and only the cached frame buffer is drawn. It is woken by
        // the pointer, by the state the card shows changing, on coming back into view, and now and then just in case.

        /// <summary>
        /// How long the body has to be left unchanged before it's frozen.
        /// </summary>
        private const double settle_time = 50;

        /// <summary>
        /// How often a frozen card is woken to pick up anything that changed without it being told.
        /// </summary>
        private const double check_interval = 2000;

        private InputManager? inputManager;
        private DelayedLoadWrapper[] coverWrappers = Array.Empty<DelayedLoadWrapper>();
        private int coversShown;

        private double wakeTime;

        /// <summary>
        /// Whether the body has been shown yet. Cards coming into view for the first time are let in one per frame: each has everything
        /// in it positioned, turned into draw nodes and drawn into its buffer, and a row of them at once is a dropped frame.
        /// </summary>
        private bool admitted;

        private static double lastAdmissionTime = double.MinValue;
        private double nextCheckTime;

        /// <summary>
        /// Makes the card update again, until it has settled. To be called when what it shows changes for a reason other than the pointer.
        /// </summary>
        public void Wake()
        {
            bodyBuffer.Frozen = false;
            wakeTime = Time.Current;
        }

        protected override void Update()
        {
            base.Update();

            // Only updated while in view.
            if (!admitted)
            {
                // The same time means the same frame.
                if (lastAdmissionTime == Time.Current)
                    return;

                lastAdmissionTime = Time.Current;
                admitted = true;
                bodyBuffer.Alpha = 1;
                Wake();
            }

            bool pointerOver = inputManager != null && content.ReceivePositionalInputAt(inputManager.CurrentState.Mouse.Position);

            if (bodyBuffer.Frozen)
            {
                if (pointerOver || Time.Current >= nextCheckTime)
                {
                    Wake();
                    return;
                }

                // Covers load a while after coming into view, and unload once out of it: they are kept running by hand, as waiting for
                // them awake would have every card entering view updating in full for as long as its covers take to arrive.
                int shown = 0;

                foreach (var wrapper in coverWrappers)
                {
                    wrapper.UpdateSubTree();
                    wrapper.UpdateSubTreeMasking();

                    if (wrapper.DelayedLoadCompleted)
                        shown++;
                }

                // One arrived (or left): to be drawn.
                if (shown != coversShown)
                    Wake();

                return;
            }

            if (pointerOver || Expanded.Value)
            {
                wakeTime = Time.Current;
                return;
            }

            double awakeFor = Time.Current - wakeTime;

            if (awakeFor < settle_time || Time.Current - bodyBuffer.LastRedrawTime < settle_time)
                return;

            coverWrappers = bodyMask.ChildrenOfType<DelayedLoadWrapper>().ToArray();
            coversShown = coverWrappers.Count(w => w.DelayedLoadCompleted);

            bodyBuffer.Frozen = true;
            // Spread out, so that a screen of cards frozen together isn't woken together.
            nextCheckTime = Time.Current + check_interval * (1 + Random.Shared.NextDouble());
        }

        #endregion

        private ScheduledDelegate? scheduledExpandedChange;

        public void ExpandAfterDelay() => queueExpandedStateChange(true, 100);

        public void CancelExpand() => scheduledExpandedChange?.Cancel();

        private void updateFromHoverChange() =>
            queueExpandedStateChange(content.IsHovered || dropdownContent.IsHovered, 100);

        private void queueExpandedStateChange(bool newState, int delay = 0)
        {
            if (Expanded.Disabled)
                return;

            scheduledExpandedChange?.Cancel();
            scheduledExpandedChange = Scheduler.AddDelayed(() => expanded.Value = newState, delay);
        }

        private ScheduledDelegate? scheduledUnmask;

        private void unmask()
        {
            // The (by now transparent) shadow is an edge effect, which needs masking.
            content.ClearTransforms(false, nameof(EdgeEffect));
            content.EdgeEffect = default;
            content.Masking = false;
        }

        private void updateState()
        {
            // Torii: the outer masking only matters for the expanded dropdown and the shadow; collapsed, the (already rounded) body is
            // all that's visible, and masking would split draw batches for nothing.
            scheduledUnmask?.Cancel();

            if (Expanded.Value && pendingExpandedContent != null)
            {
                LoadComponentAsync(pendingExpandedContent, loaded => dropdownScroll.Child = loaded);
                pendingExpandedContent = null;
            }

            if (Expanded.Value)
                content.Masking = true;
            else
                scheduledUnmask = Scheduler.AddDelayed(unmask, BeatmapCard.TRANSITION_DURATION);

            // Scale value is intentionally chosen to fit in the spacing of listing displays, as to not overlap horizontally with adjacent cards.
            // This avoids depth issues where a hovered (scaled) card to the right of another card would be beneath the card to the left.
            this.ScaleTo(Expanded.Value ? 1.03f : 1, 500, Easing.OutQuint);

            if (Expanded.Value)
            {
                background.FadeIn(BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
                dropdownContent.FadeIn(BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
                borderContainer.FadeIn(BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
            }
            else
            {
                background.FadeOut(BeatmapCard.TRANSITION_DURATION / 3f, Easing.OutQuint);
                dropdownContent.FadeOut(BeatmapCard.TRANSITION_DURATION / 3f, Easing.OutQuint);
                borderContainer.FadeOut(BeatmapCard.TRANSITION_DURATION / 3f, Easing.OutQuint);
            }

            content.TweenEdgeEffectTo(new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Offset = new Vector2(0, 2),
                Radius = 10,
                Colour = Colour4.Black.Opacity(Expanded.Value ? 0.3f : 0f),
                Hollow = true,
            }, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
        }
    }
}
