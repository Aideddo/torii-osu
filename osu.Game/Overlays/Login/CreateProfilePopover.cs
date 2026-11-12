// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays.Notifications;
using osuTK;

namespace osu.Game.Overlays.Login
{
    public partial class CreateProfilePopover : OsuPopover
    {
        private readonly Action onProfileCreated;
        private readonly int nextSlotCost;

        private LabelledTextBox nameTextBox = null!;
        private LabelledTextBox tagTextBox = null!;

        private OsuCheckbox allowOptionalHdCheckbox = null!;
        private OsuCheckbox strictModeCheckbox = null!;
        private OsuCheckbox pureNoModCheckbox = null!;

        private RoundedButton createButton = null!;
        private LoadingSpinner spinner = null!;

        private readonly HashSet<string> requiredMods = new HashSet<string>();
        private readonly List<ModChip> modChips = new List<ModChip>();

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public CreateProfilePopover(Action onProfileCreated, int nextSlotCost = 0)
        {
            this.onProfileCreated = onProfileCreated;
            this.nextSlotCost = nextSlotCost;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            const float content_width = 330;

            Body.BorderThickness = 2;
            Body.BorderColour = Colour4.FromHex("38b2ac");

            Child = new FillFlowContainer
            {
                Width = content_width,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = "Create Custom Profile",
                        Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                        Colour = Colour4.FromHex("38b2ac"),
                        Margin = new MarginPadding { Bottom = 2 },
                    },
                    new OsuSpriteText
                    {
                        Text = "Create a dedicated persona with its own rank and PP.",
                        Font = OsuFont.GetFont(size: 12),
                        Colour = colourProvider.Content2,
                        Margin = new MarginPadding { Bottom = 6 },
                    },
                    nameTextBox = new LabelledTextBox
                    {
                        Label = "Profile Name",
                        PlaceholderText = "e.g. DT Grinder, Hidden Only",
                        TabbableContentContainer = this,
                    },
                    tagTextBox = new LabelledTextBox
                    {
                        Label = "Profile Tag",
                        PlaceholderText = "e.g. DT, HD, NM (max 6 chars)",
                        TabbableContentContainer = this,
                    },
                    new OsuSpriteText
                    {
                        Text = "Required Mods (Click to toggle)",
                        Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold),
                        Colour = colourProvider.Content1,
                        Margin = new MarginPadding { Top = 4 },
                    },
                    createModChipsFlow(),
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 4),
                        Margin = new MarginPadding { Top = 4, Bottom = 4 },
                        Children = new Drawable[]
                        {
                            allowOptionalHdCheckbox = new OsuCheckbox
                            {
                                LabelText = "Allow optional Hidden (HD)",
                                Current = { Value = true }
                            },
                            strictModeCheckbox = new OsuCheckbox
                            {
                                LabelText = "Strict mode (disallow unselected mods)",
                                Current = { Value = true }
                            },
                            pureNoModCheckbox = new OsuCheckbox
                            {
                                LabelText = "Pure NoMod (block all difficulty mods)",
                                Current = { Value = false }
                            },
                        }
                    },
                    // Warning Container
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        CornerRadius = 6,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Colour4.FromHex("2d2010"),
                            },
                            new LinkFlowContainer(s =>
                            {
                                s.Font = OsuFont.GetFont(size: 11);
                                s.Colour = colours.YellowLight;
                            })
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding(8),
                                Text = "This profile cannot be changed (might be able to in the future depending on changes) but please make sure you got exactly what you wanted.",
                            }
                        }
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(6, 0),
                        Margin = new MarginPadding { Top = 2 },
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = "Slot Cost:",
                                Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                                Colour = colourProvider.Content2,
                            },
                            new OsuSpriteText
                            {
                                Text = nextSlotCost == 0 ? "Free (First Profile)" : $"{nextSlotCost:N0} Coins",
                                Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                                Colour = nextSlotCost == 0 ? colours.Green : colours.Yellow,
                            }
                        }
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 36,
                        Margin = new MarginPadding { Top = 4 },
                        Children = new Drawable[]
                        {
                            createButton = new RoundedButton
                            {
                                RelativeSizeAxes = Axes.Both,
                                Text = "Create Profile",
                                Action = performCreate,
                            },
                            spinner = new LoadingSpinner
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Size = new Vector2(20),
                            }
                        }
                    }
                }
            };

            pureNoModCheckbox.Current.BindValueChanged(e =>
            {
                if (e.NewValue)
                {
                    requiredMods.Clear();
                    foreach (var chip in modChips)
                        chip.Selected.Value = false;
                    allowOptionalHdCheckbox.Current.Value = false;
                    strictModeCheckbox.Current.Value = true;
                }
            });

            nameTextBox.Current.BindValueChanged(onInputsChanged);
            tagTextBox.Current.BindValueChanged(onInputsChanged);
            validateInputs();
        }

        private Drawable createModChipsFlow()
        {
            var availableMods = new[] { "DT", "HD", "HR", "EZ", "FL", "HT" };
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 4),
            };

            foreach (var mod in availableMods)
            {
                var chip = new ModChip(mod)
                {
                    Action = () =>
                    {
                        if (pureNoModCheckbox.Current.Value)
                            pureNoModCheckbox.Current.Value = false;

                        if (requiredMods.Contains(mod))
                            requiredMods.Remove(mod);
                        else
                            requiredMods.Add(mod);

                        validateInputs();
                    }
                };
                modChips.Add(chip);
                flow.Add(chip);
            }

            return flow;
        }

        private void onInputsChanged(ValueChangedEvent<string> _) => validateInputs();

        private void validateInputs()
        {
            bool valid = !string.IsNullOrWhiteSpace(nameTextBox.Current.Value)
                         && !string.IsNullOrWhiteSpace(tagTextBox.Current.Value)
                         && tagTextBox.Current.Value.Trim().Length >= 2;

            createButton.Enabled.Value = valid;
        }

        private void performCreate()
        {
            string name = nameTextBox.Current.Value.Trim();
            string tag = tagTextBox.Current.Value.Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(tag))
                return;

            createButton.Enabled.Value = false;
            spinner.Show();

            string restrictionMode = "custom";
            var reqList = requiredMods.ToList();
            var allowedList = new List<string>();

            if (pureNoModCheckbox.Current.Value)
            {
                restrictionMode = "nomod_only";
                reqList.Clear();
            }
            else
            {
                if (allowOptionalHdCheckbox.Current.Value && !reqList.Contains("HD"))
                    allowedList.Add("HD");

                if (!strictModeCheckbox.Current.Value && reqList.Count == 0)
                    restrictionMode = "free";
            }

            var request = new CreateToriiSubProfileRequest(name, tag, restrictionMode, reqList, allowedList);
            request.Success += resp => Schedule(() =>
            {
                spinner.Hide();
                this.HidePopover();

                api.SwitchSubProfile(resp.AccessToken);
                onProfileCreated?.Invoke();

                notifications?.Post(new SimpleNotification
                {
                    Text = $"Successfully created profile [{resp.Tag}] {resp.Name}!",
                    Icon = FontAwesome.Solid.CheckCircle,
                });
            });

            request.Failure += err => Schedule(() =>
            {
                spinner.Hide();
                createButton.Enabled.Value = true;

                notifications?.Post(new SimpleNotification
                {
                    Text = $"Failed to create profile: {err.Message}",
                    Icon = FontAwesome.Solid.Times,
                });
            });

            api.Queue(request);
        }

        private partial class ModChip : ClickableContainer
        {
            public readonly BindableBool Selected = new BindableBool();
            public readonly string ModAcronym;

            private Box bg = null!;
            private Container borderContainer = null!;
            private SpriteText label = null!;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public ModChip(string modAcronym)
            {
                ModAcronym = modAcronym;
                Size = new Vector2(46, 28);
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                CornerRadius = 6;
                Masking = true;

                Children = new Drawable[]
                {
                    bg = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    borderContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        CornerRadius = 6,
                        Masking = true,
                        BorderThickness = 1.5f,
                        BorderColour = Colour4.Transparent,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                            AlwaysPresent = true,
                        }
                    },
                    label = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = ModAcronym,
                        Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                        Colour = colourProvider.Content2,
                    }
                };

                Selected.BindValueChanged(e =>
                {
                    if (e.NewValue)
                    {
                        bg.Colour = Colour4.FromHex("1b4d4b");
                        borderContainer.BorderColour = Colour4.FromHex("38b2ac");
                        label.Colour = Colour4.White;
                    }
                    else
                    {
                        bg.Colour = colourProvider.Background4;
                        borderContainer.BorderColour = Colour4.Transparent;
                        label.Colour = colourProvider.Content2;
                    }
                }, true);
            }

            protected override bool OnClick(ClickEvent e)
            {
                Selected.Toggle();
                return base.OnClick(e);
            }
        }
    }
}
