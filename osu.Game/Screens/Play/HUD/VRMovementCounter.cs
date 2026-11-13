// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation.HUD;
using osu.Game.Skinning;
using osu.Game.VR;
using osuTK;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// Torii: shows how far the VR controllers (both hands added together) have been moved.
    /// </summary>
    public partial class VRMovementCounter : RollingCounter<double>, ISerialisableDrawable, IVRSkinComponent
    {
        protected override double RollingDuration => 250;

        [SettingSource(typeof(VRMovementCounterStrings), nameof(VRMovementCounterStrings.Period), nameof(VRMovementCounterStrings.PeriodDescription))]
        public Bindable<MovementPeriod> Period { get; } = new Bindable<MovementPeriod>();

        [SettingSource(typeof(VRMovementCounterStrings), nameof(VRMovementCounterStrings.Alignment), nameof(VRMovementCounterStrings.AlignmentDescription))]
        public Bindable<TextAlignment> Alignment { get; } = new Bindable<TextAlignment>();

        [SettingSource(typeof(VRMovementCounterStrings), nameof(VRMovementCounterStrings.ShowLabel))]
        public BindableBool ShowLabel { get; } = new BindableBool(true);

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        /// <summary>
        /// Null outside of VR, where the counter stays at zero.
        /// </summary>
        [Resolved]
        private VRMovementTracker? tracker { get; set; }

        private TextComponent text = null!;

        private double lastSessionDistance;

        /// <summary>
        /// Distance moved while this play of the beatmap was running (not paused), in metres.
        /// </summary>
        private double beatmapDistance;

        [BackgroundDependencyLoader]
        private void load(OsuColour colour)
        {
            Colour = colour.BlueLighter;
            Current.Value = DisplayedCount = 0;

            lastSessionDistance = tracker?.SessionDistance ?? 0;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Period.BindValueChanged(p =>
            {
                text.Label = p.NewValue switch
                {
                    MovementPeriod.Session => @"session",
                    MovementPeriod.Today => @"today",
                    MovementPeriod.AllTime => @"all time",
                    _ => @"this map",
                };

                // Jump to the other figure rather than rolling to it.
                updateCurrent();
                DisplayedCount = Current.Value;
            }, true);

            ShowLabel.BindValueChanged(s => text.ShowLabel = s.NewValue, true);
            Alignment.BindValueChanged(a => text.Alignment = a.NewValue, true);
        }

        protected override void Update()
        {
            base.Update();

            if (tracker == null)
                return;

            double sessionDistance = tracker.SessionDistance;

            if (!gameplayClock.IsPaused.Value)
                beatmapDistance += sessionDistance - lastSessionDistance;

            lastSessionDistance = sessionDistance;

            updateCurrent();
        }

        private void updateCurrent() => Current.Value = Period.Value switch
        {
            MovementPeriod.Session => tracker?.SessionDistance ?? 0,
            MovementPeriod.Today => tracker?.TodayDistance ?? 0,
            MovementPeriod.AllTime => tracker?.AllTimeDistance ?? 0,
            _ => beatmapDistance,
        };

        protected override LocalisableString FormatCount(double count) => count >= 1000 ? $@"{count / 1000:0.00}km" : $@"{count:0.0}m";

        protected override IHasText CreateText() => text = new TextComponent();

        private partial class TextComponent : CompositeDrawable, IHasText
        {
            /// <summary>
            /// The distance followed by its unit, as formatted by <see cref="FormatCount"/>.
            /// </summary>
            public LocalisableString Text
            {
                get => text;
                set
                {
                    text = value;

                    string formatted = value.ToString();
                    int unitStart = formatted.Length;
                    while (unitStart > 0 && char.IsLetter(formatted[unitStart - 1]))
                        unitStart--;

                    number.Text = formatted[..unitStart];
                    unit.Text = formatted[unitStart..];
                }
            }

            public string Label
            {
                set => label.Text = value;
            }

            public bool ShowLabel
            {
                set => label.Alpha = value ? 1 : 0;
            }

            /// <summary>
            /// Which side the label and the distance line up with each other on.
            /// </summary>
            public TextAlignment Alignment
            {
                set
                {
                    Anchor anchor = value switch
                    {
                        TextAlignment.Centre => Anchor.TopCentre,
                        TextAlignment.Right => Anchor.TopRight,
                        _ => Anchor.TopLeft,
                    };

                    flow.Anchor = flow.Origin = anchor;
                    label.Anchor = label.Origin = anchor;
                    distance.Anchor = distance.Origin = anchor;
                }
            }

            private LocalisableString text;

            private readonly OsuSpriteText number;
            private readonly OsuSpriteText unit;
            private readonly OsuSpriteText label;
            private readonly FillFlowContainer flow;
            private readonly FillFlowContainer distance;

            public TextComponent()
            {
                AutoSizeAxes = Axes.Both;

                InternalChild = flow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        label = new OsuSpriteText
                        {
                            Font = OsuFont.Numeric.With(size: 8),
                        },
                        distance = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(2),
                            Children = new Drawable[]
                            {
                                number = new NumericSpriteText
                                {
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    Font = OsuFont.Numeric.With(size: 16, fixedWidth: true)
                                },
                                unit = new OsuSpriteText
                                {
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    Font = OsuFont.Numeric.With(size: 8),
                                    Padding = new MarginPadding { Bottom = 2f }, // align baseline better
                                }
                            }
                        }
                    }
                };
            }
        }

        public bool UsesFixedAnchor { get; set; }

        public enum MovementPeriod
        {
            [LocalisableDescription(typeof(VRMovementCounterStrings), nameof(VRMovementCounterStrings.PeriodBeatmap))]
            Beatmap,

            [LocalisableDescription(typeof(VRMovementCounterStrings), nameof(VRMovementCounterStrings.PeriodSession))]
            Session,

            [LocalisableDescription(typeof(VRMovementCounterStrings), nameof(VRMovementCounterStrings.PeriodAllTime))]
            AllTime,

            // (Added later: after the others so that saved layouts keep their meaning.)
            [LocalisableDescription(typeof(VRMovementCounterStrings), nameof(VRMovementCounterStrings.PeriodToday))]
            Today,
        }

        public enum TextAlignment
        {
            [LocalisableDescription(typeof(VRMovementCounterStrings), nameof(VRMovementCounterStrings.AlignmentLeft))]
            Left,

            [LocalisableDescription(typeof(VRMovementCounterStrings), nameof(VRMovementCounterStrings.AlignmentCentre))]
            Centre,

            [LocalisableDescription(typeof(VRMovementCounterStrings), nameof(VRMovementCounterStrings.AlignmentRight))]
            Right,
        }
    }
}
