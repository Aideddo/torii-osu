// Copyright (c) GooGuTeam & Torii. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.ToriiBriefing
{
    /// <summary>
    /// Briefing dialog welcoming players to the Modern V2 (g0v0) results screen.
    /// Introduces the refreshed layout designed by GooGuTeam (g0v0) with visual score metrics,
    /// expanded hit statistics, and direct buttons to switch to Lazer Classic or Torii (Stable)
    /// without needing to navigate to settings.
    /// </summary>
    public partial class ToriiResultsNoticeOverlay : VisibilityContainer, IKeyBindingHandler<GlobalAction>
    {
        public Action? OnDismiss { get; set; }

        private readonly Bindable<bool> useV2ResultsScreen;
        private readonly Bindable<bool> useLegacyResults;
        private readonly Bindable<bool> noticeSeen;

        private Container panel = null!;
        private Sample? samplePopIn;
        private Sample? samplePopOut;

        public ToriiResultsNoticeOverlay(Bindable<bool> useV2ResultsScreen, Bindable<bool> useLegacyResults, Bindable<bool> noticeSeen)
        {
            this.useV2ResultsScreen = useV2ResultsScreen;
            this.useLegacyResults = useLegacyResults;
            this.noticeSeen = noticeSeen;

            RelativeSizeAxes = Axes.Both;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;
        protected override bool OnClick(ClickEvent e) => true;
        protected override bool OnMouseDown(MouseDownEvent e) => true;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            samplePopIn = audio.Samples.Get(@"UI/overlay-pop-in");
            samplePopOut = audio.Samples.Get(@"UI/overlay-pop-out");

            FillFlowContainer content;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.65f,
                },
                panel = new BriefingGlass
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 560,
                    AutoSizeAxes = Axes.Y,
                    CornerSize = BriefingTheme.CornerLg,
                    SpecularStrength = 0.18f,
                    SpecularHeight = 70,
                    ShadowOpacity = 0.4f,
                    ShadowRadius = 30,
                    RelativeContentSize = Axes.X,
                    Child = content = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, BriefingTheme.SpacingMd),
                        Padding = new MarginPadding(BriefingTheme.SpacingXl),
                    },
                },
            };

            content.AddRange(new Drawable[]
            {
                // Kicker: badge torii-gate + label
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(BriefingTheme.SpacingSm, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.ToriiGate,
                            Size = new Vector2(BriefingTheme.TypeBody),
                            Colour = BriefingTheme.AccentPink,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = "TORII",
                            Font = OsuFont.GetFont(size: BriefingTheme.TypeCaption, weight: FontWeight.Bold),
                            Spacing = new Vector2(BriefingTheme.CaptionTracking * BriefingTheme.TypeCaption, 0),
                            Colour = BriefingTheme.AccentPink,
                        },
                    },
                },
                new OsuSpriteText
                {
                    Text = "New Results Screen",
                    Font = OsuFont.GetFont(size: BriefingTheme.TypeTitle, weight: FontWeight.SemiBold),
                },
                new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: BriefingTheme.TypeBody))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Colour = Color4.White.Opacity(BriefingTheme.InkSecondary),
                    Text = "We're introducing a modern results screen featuring a refreshed layout, visual score metrics, expanded hit statistics, and quick access to leaderboards.\n\nPrefer the classic Lazer design or osu!stable style? You can switch anytime in Settings or right here:",
                },
                // Card for in-situ layout choices
                new BriefingGlass
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    CornerSize = BriefingTheme.CornerMd,
                    SurfaceLift = 1.25f,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding(BriefingTheme.SpacingMd),
                        Child = new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.Distributed),
                                new Dimension(GridSizeMode.Absolute, 10),
                                new Dimension(GridSizeMode.Distributed),
                            },
                            Content = new[]
                            {
                                new Drawable?[]
                                {
                                    new ResultsLayoutChoiceButton(FontAwesome.Solid.LayerGroup, "Lazer Classic", BriefingTheme.AccentPink)
                                    {
                                        Action = () =>
                                        {
                                            useV2ResultsScreen.Value = false;
                                            useLegacyResults.Value = false;
                                            dismiss();
                                        }
                                    },
                                    null,
                                    new ResultsLayoutChoiceButton(FontAwesome.Solid.History, "Torii (Stable)", BriefingTheme.AccentCyan)
                                    {
                                        Action = () =>
                                        {
                                            useLegacyResults.Value = true;
                                            useV2ResultsScreen.Value = false;
                                            dismiss();
                                        }
                                    },
                                }
                            }
                        },
                    },
                },
                // Primary action button
                new ResultsPrimaryActionButton("Got it, try the new screen", BriefingTheme.AccentPink)
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Action = dismiss,
                },
            });
        }

        private void dismiss()
        {
            noticeSeen.Value = true;
            Hide();
            OnDismiss?.Invoke();
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            if (e.Action == GlobalAction.Back)
            {
                dismiss();
                return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected override void PopIn()
        {
            samplePopIn?.Play();
            this.FadeIn(BriefingTheme.HoverDuration, Easing.OutQuint);
            panel.ScaleTo(0.94f).ScaleTo(1f, BriefingTheme.EntranceDuration, Easing.OutBack)
                 .MoveToY(20).MoveToY(0, BriefingTheme.EntranceDuration, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            noticeSeen.Value = true;
            samplePopOut?.Play();
            this.FadeOut(BriefingTheme.DismissDuration, Easing.OutQuint);
            panel.ScaleTo(0.97f, BriefingTheme.DismissDuration, Easing.OutQuint);
        }
    }
}
