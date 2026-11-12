// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Notifications;
using osuTK;

namespace osu.Game.Overlays.Login
{
    public partial class SubProfilesSection : Container
    {
        private FillFlowContainer profilesList = null!;
        private LoadingSpinner spinner = null!;
        private AddProfileButton addProfileButton = null!;
        private OsuSpriteText balanceText = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        // Header
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 20,
                            Margin = new MarginPadding { Top = 4 },
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = "PLAYER PROFILES",
                                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                                    Colour = Colour4.FromHex("38b2ac"),
                                },
                                balanceText = new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Text = string.Empty,
                                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                                    Colour = colours.Yellow,
                                }
                            }
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                profilesList = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 6),
                                },
                                spinner = new LoadingSpinner
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = new Vector2(24),
                                }
                            }
                        },
                        addProfileButton = new AddProfileButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 32,
                            Margin = new MarginPadding { Top = 2 },
                        }
                    }
                }
            };

            addProfileButton.OnCreated = Refresh;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Refresh();
        }

        public void Refresh()
        {
            spinner.Show();
            profilesList.FadeTo(0.4f, 150);

            var req = new GetToriiSubProfilesRequest();
            req.Success += list => Schedule(() =>
            {
                spinner.Hide();
                profilesList.FadeTo(1f, 150);
                profilesList.Clear();

                balanceText.Text = $"{list.UserPoints:N0} coins";
                addProfileButton.NextSlotCost = list.NextSlotCost;

                foreach (var profile in list.Profiles)
                {
                    profilesList.Add(new ProfileCard(profile, onSwitched: () =>
                    {
                        Refresh();
                    }));
                }
            });

            req.Failure += _ => Schedule(() =>
            {
                spinner.Hide();
                profilesList.FadeTo(1f, 150);
            });

            api.Queue(req);
        }

        private partial class ProfileCard : Container
        {
            private readonly APIToriiSubProfile profile;
            private readonly System.Action onSwitched;

            [Resolved]
            private IAPIProvider api { get; set; } = null!;

            [Resolved]
            private INotificationOverlay? notifications { get; set; }

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public ProfileCard(APIToriiSubProfile profile, System.Action onSwitched)
            {
                this.profile = profile;
                this.onSwitched = onSwitched;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                RelativeSizeAxes = Axes.X;
                Height = 46;
                CornerRadius = 6;
                Masking = true;

                Colour4 tagBg = profile.IsMaster ? Colour4.FromHex("2d3748") : Colour4.FromHex("234e52");
                Colour4 tagText = profile.IsMaster ? Colour4.FromHex("cbd5e0") : Colour4.FromHex("38b2ac");

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = profile.IsActive ? colourProvider.Background3 : colourProvider.Background4,
                    },
                    // Left active indicator strip
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = profile.IsActive ? 3.5f : 0,
                        Colour = Colour4.FromHex("38b2ac"),
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 10, Vertical = 6 },
                        Children = new Drawable[]
                        {
                            // Left details
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 2),
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(6, 0),
                                        Children = new Drawable[]
                                        {
                                            // Tag pill
                                            new Container
                                            {
                                                AutoSizeAxes = Axes.Both,
                                                CornerRadius = 4,
                                                Masking = true,
                                                Children = new Drawable[]
                                                {
                                                    new Box
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                        Colour = tagBg,
                                                    },
                                                    new OsuSpriteText
                                                    {
                                                        Text = $"[{profile.Tag}]",
                                                        Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                                                        Colour = tagText,
                                                        Padding = new MarginPadding { Horizontal = 4, Vertical = 1 },
                                                    }
                                                }
                                            },
                                            new OsuSpriteText
                                            {
                                                Text = profile.Name,
                                                Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold),
                                                Colour = profile.IsActive ? Colour4.White : colourProvider.Content1,
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                            }
                                        }
                                    },
                                    new OsuSpriteText
                                    {
                                        Text = $"{profile.PP:N0} pp  •  {formatMode(profile.RestrictionMode)}",
                                        Font = OsuFont.GetFont(size: 11),
                                        Colour = colourProvider.Content2,
                                    }
                                }
                            },
                            // Right action / indicator
                            createRightControl(colourProvider)
                        }
                    }
                };
            }

            private Drawable createRightControl(OverlayColourProvider colourProvider)
            {
                if (profile.IsActive)
                {
                    return new Container
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        AutoSizeAxes = Axes.Both,
                        CornerRadius = 4,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Colour4.FromHex("1b4d4b"),
                            },
                            new OsuSpriteText
                            {
                                Text = "ACTIVE",
                                Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                                Colour = Colour4.FromHex("38b2ac"),
                                Padding = new MarginPadding { Horizontal = 6, Vertical = 2 },
                            }
                        }
                    };
                }

                return new RoundedButton
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Width = 60,
                    Height = 26,
                    Text = "Switch",
                    Action = performSwitch,
                };
            }

            private string formatMode(string mode)
            {
                switch (mode)
                {
                    case "dt_only": return "DT Only";
                    case "hd_only": return "Hidden Only";
                    case "nomod_only": return "NoMod";
                    case "custom": return "Custom";
                    default: return "Free";
                }
            }

            private void performSwitch()
            {
                APIRequest switchReq;
                if (profile.IsMaster)
                {
                    var req = new SwitchToMasterSubProfileRequest();
                    req.Success += resp => Schedule(() =>
                    {
                        api.SwitchSubProfile(resp.AccessToken);
                        onSwitched();
                        notifications?.Post(new SimpleNotification
                        {
                            Text = "Switched to Main profile.",
                            Icon = FontAwesome.Solid.User,
                        });
                    });
                    switchReq = req;
                }
                else
                {
                    var req = new SwitchToriiSubProfileRequest(profile.Id);
                    req.Success += resp => Schedule(() =>
                    {
                        api.SwitchSubProfile(resp.AccessToken);
                        onSwitched();
                        notifications?.Post(new SimpleNotification
                        {
                            Text = $"Switched to [{resp.Tag}] {resp.Name}.",
                            Icon = FontAwesome.Solid.UserTag,
                        });
                    });
                    switchReq = req;
                }

                switchReq.Failure += err => Schedule(() =>
                {
                    notifications?.Post(new SimpleNotification
                    {
                        Text = $"Failed to switch profile: {err.Message}",
                        Icon = FontAwesome.Solid.Times,
                    });
                });

                api.Queue(switchReq);
            }
        }

        public partial class AddProfileButton : RoundedButton, IHasPopover
        {
            public int NextSlotCost { get; set; }
            public System.Action? OnCreated { get; set; }

            public AddProfileButton()
            {
                Text = "+ Create New Profile";
                Action = this.ShowPopover;
            }

            public Popover GetPopover() => new CreateProfilePopover(() => OnCreated?.Invoke(), NextSlotCost);
        }
    }
}
