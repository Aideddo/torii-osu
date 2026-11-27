// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.FirstRunSetup
{
    [LocalisableDescription(typeof(FirstRunSetupOverlayStrings), nameof(FirstRunSetupOverlayStrings.WelcomeTitle))]
    public partial class ScreenWelcome : WizardScreen
    {
        public bool ExperiencedMode { get; private set; }

        private ExperienceOptionCard newPlayerCard = null!;
        private ExperienceOptionCard experiencedPlayerCard = null!;

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager frameworkConfig)
        {
            Content.Children = new Drawable[]
            {
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    RowDimensions = new[]
                    {
                        // Avoid height changes when changing language.
                        new Dimension(GridSizeMode.AutoSize, minSize: 90),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                            {
                                Text = FirstRunSetupOverlayStrings.WelcomeDescription,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y
                            },
                        },
                    }
                },
                new OsuSpriteText
                {
                    Text = FirstRunSetupOverlayStrings.ExperienceModePrompt,
                    Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE, weight: FontWeight.SemiBold),
                    Colour = OverlayColourProvider.Content2,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 10),
                        new Dimension(),
                    },
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize, minSize: 82),
                    },
                    Content = new[]
                    {
                        new[]
                        {
                            newPlayerCard = new ExperienceOptionCard(
                                FirstRunSetupOverlayStrings.ExperienceModeNew,
                                FirstRunSetupOverlayStrings.ExperienceModeNewSub,
                                recommended: true)
                            {
                                Action = () => setExperienceMode(false),
                            },
                            Empty(),
                            experiencedPlayerCard = new ExperienceOptionCard(
                                FirstRunSetupOverlayStrings.ExperienceModeExperienced,
                                FirstRunSetupOverlayStrings.ExperienceModeExperiencedSub,
                                recommended: false)
                            {
                                Action = () => setExperienceMode(true),
                            },
                        },
                    }
                },
                new SettingsCheckbox
                {
                    LabelText = GeneralSettingsStrings.PreferOriginalMetadataLanguage,
                    Current = frameworkConfig.GetBindable<bool>(FrameworkSetting.ShowUnicode)
                },
                new LanguageSelectionFlow
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y
                }
            };

            setExperienceMode(false);
        }

        private void setExperienceMode(bool experienced)
        {
            ExperiencedMode = experienced;
            newPlayerCard.Selected = !experienced;
            experiencedPlayerCard.Selected = experienced;
        }

        public override LocalisableString? NextStepText => FirstRunSetupOverlayStrings.GetStarted;

        private partial class ExperienceOptionCard : OsuClickableContainer
        {
            private readonly LocalisableString title;
            private readonly LocalisableString subtitle;
            private readonly bool recommended;

            private Box backgroundBox = null!;
            private Container borderContainer = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private bool selected;

            public bool Selected
            {
                get => selected;
                set
                {
                    if (selected == value)
                        return;

                    selected = value;
                    if (IsLoaded)
                        updateState();
                }
            }

            public ExperienceOptionCard(LocalisableString title, LocalisableString subtitle, bool recommended)
            {
                this.title = title;
                this.subtitle = subtitle;
                this.recommended = recommended;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 10;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    borderContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 10,
                        BorderThickness = 2,
                        BorderColour = colourProvider.Highlight1,
                        Child = backgroundBox = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background4,
                        },
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(12),
                        Spacing = new Vector2(0, 4),
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = title,
                                        Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE, weight: FontWeight.Bold),
                                        Colour = colourProvider.Content1,
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                    },
                                    recommended
                                        ? new Container
                                        {
                                            AutoSizeAxes = Axes.Both,
                                            Masking = true,
                                            CornerRadius = 5,
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Children = new Drawable[]
                                            {
                                                new Box
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Colour = colourProvider.Highlight1,
                                                },
                                                new OsuSpriteText
                                                {
                                                    Text = FirstRunSetupOverlayStrings.ToriiRecommendedBadge,
                                                    Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                                                    Colour = Color4.Black,
                                                    Margin = new MarginPadding { Horizontal = 6, Vertical = 2 },
                                                }
                                            }
                                        }
                                        : Empty(),
                                }
                            },
                            new OsuTextFlowContainer(cp =>
                            {
                                cp.Font = OsuFont.Default.With(size: 13);
                                cp.Colour = colourProvider.Content2;
                            })
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Text = subtitle,
                            },
                        }
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                updateState();
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                base.OnHoverLost(e);
                updateState();
            }

            private void updateState()
            {
                borderContainer.BorderColour = selected ? colourProvider.Highlight1 : colourProvider.Background3;
                backgroundBox.Colour = selected ? colourProvider.Background3 : (IsHovered ? colourProvider.Background4 : colourProvider.Background5);
            }
        }

        private partial class LanguageSelectionFlow : FillFlowContainer
        {
            private Bindable<Language> language = null!;

            private ScheduledDelegate? updateSelectedDelegate;

            [BackgroundDependencyLoader]
            private void load(OsuGameBase game)
            {
                Direction = FillDirection.Full;
                Spacing = new Vector2(5);

                ChildrenEnumerable = Enum.GetValues<Language>()
                                         .Select(l => new LanguageButton(l)
                                         {
                                             Action = () => language.Value = l,
                                         });

                language = game.CurrentLanguage.GetBoundCopy();
                language.BindValueChanged(v =>
                {
                    // Changing language may cause a short period of blocking the UI thread while the new glyphs are loaded.
                    // Scheduling ensures the button animation plays smoothly after any blocking operation completes.
                    // Note that a delay is required (the alternative would be a double-schedule; delay feels better).
                    updateSelectedDelegate?.Cancel();
                    updateSelectedDelegate = Scheduler.AddDelayed(() => updateSelectedStates(v.NewValue), 50);
                }, true);
            }

            private void updateSelectedStates(Language language)
            {
                foreach (var c in Children.OfType<LanguageButton>())
                    c.Selected = c.Language == language;
            }

            private partial class LanguageButton : OsuClickableContainer
            {
                public readonly Language Language;

                private Box backgroundBox = null!;

                private OsuSpriteText text = null!;

                [Resolved]
                private OverlayColourProvider colourProvider { get; set; } = null!;

                private bool selected;

                public bool Selected
                {
                    get => selected;
                    set
                    {
                        if (selected == value)
                            return;

                        selected = value;

                        if (IsLoaded)
                            updateState();
                    }
                }

                public LanguageButton(Language language)
                {
                    Language = language;

                    Size = new Vector2(160, 50);
                    Masking = true;
                    CornerRadius = 10;
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    AddRange(new Drawable[]
                    {
                        backgroundBox = new Box
                        {
                            Alpha = 0,
                            Colour = colourProvider.Background5,
                            RelativeSizeAxes = Axes.Both,
                        },
                        text = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Colour = colourProvider.Light1,
                            Text = Language.GetDescription(),
                        }
                    });
                }

                protected override void LoadComplete()
                {
                    base.LoadComplete();
                    updateState();
                }

                protected override bool OnHover(HoverEvent e)
                {
                    if (!selected)
                        updateState();
                    return base.OnHover(e);
                }

                protected override void OnHoverLost(HoverLostEvent e)
                {
                    if (!selected)
                        updateState();
                    base.OnHoverLost(e);
                }

                private void updateState()
                {
                    if (selected)
                    {
                        const double selected_duration = 1000;

                        backgroundBox.FadeTo(1, selected_duration, Easing.OutQuint);
                        backgroundBox.FadeColour(colourProvider.Background2, selected_duration, Easing.OutQuint);
                        text.FadeColour(colourProvider.Content1, selected_duration, Easing.OutQuint);
                        text.ScaleTo(1.2f, selected_duration, Easing.OutQuint);
                    }
                    else
                    {
                        const double duration = 500;

                        backgroundBox.FadeTo(IsHovered ? 1 : 0, duration, Easing.OutQuint);
                        backgroundBox.FadeColour(colourProvider.Background5, duration, Easing.OutQuint);
                        text.FadeColour(colourProvider.Light1, duration, Easing.OutQuint);
                        text.ScaleTo(1, duration, Easing.OutQuint);
                    }
                }
            }
        }
    }
}
