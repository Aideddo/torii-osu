// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
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
        private OsuTextFlowContainer statusText = null!;
        private RoundedButton retryButton = null!;
        private int refreshVersion;
        private IBindable<osu.Game.Rulesets.RulesetInfo>? trackedRuleset;

        [Resolved]
        private osu.Game.Configuration.ProfileModPolicyController profilePolicy { get; set; } = null!;

        [Resolved]
        private IBindable<osu.Game.Rulesets.RulesetInfo> ruleset { get; set; } = null!;

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
                        statusText = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 12))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                        retryButton = new RoundedButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 30,
                            Text = "Retry",
                            Action = Refresh,
                            Alpha = 0,
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
            trackedRuleset = ruleset.GetBoundCopy();
            trackedRuleset.BindValueChanged(_ => Refresh());
            Refresh();
        }

        protected override void Dispose(bool isDisposing)
        {
            trackedRuleset?.UnbindAll();
            base.Dispose(isDisposing);
        }

        public void Refresh()
        {
            int version = ++refreshVersion;
            statusText.Text = string.Empty;
            retryButton.Hide();
            addProfileButton.Enabled.Value = false;
            spinner.Show();
            profilesList.FadeTo(0.4f, 150);

            void display(APIToriiSubProfilesList list)
            {
                if (IsDisposed || version != refreshVersion)
                    return;
                spinner.Hide();
                profilesList.FadeTo(1f, 150);
                profilesList.Clear();

                balanceText.Text = $"{list.UserPoints:N0} coins";
                addProfileButton.NextSlotCost = list.NextSlotCost;
                addProfileButton.Enabled.Value = list.UserPoints >= list.NextSlotCost;
                statusText.Text = list.UserPoints < list.NextSlotCost
                    ? $"You need {list.NextSlotCost - list.UserPoints:N0} more coins for another profile."
                    : string.Empty;
#if DEBUG
                if (SubProfilePreview.Enabled)
                {
                    statusText.Text = "Local UI preview - sample profiles and coins. Your real account does not change.";
                    profilePolicy.Current.Value = list.Profiles.FirstOrDefault(p => p.IsActive)?.ModPolicy;
                }
#endif

                foreach (var profile in list.Profiles)
                {
                    profilesList.Add(new ProfileCard(profile, onSwitched: () =>
                    {
                        Refresh();
                    }));
                }
            }

#if DEBUG
            if (SubProfilePreview.Enabled)
            {
                display(SubProfilePreview.GetProfiles(ruleset.Value?.OnlineID ?? 0));
                return;
            }
#endif
            var req = new GetToriiSubProfilesRequest(ruleset.Value?.OnlineID ?? 0);
            req.Success += list => Schedule(() => display(list));

            req.Failure += _ => Schedule(() =>
            {
                if (IsDisposed || version != refreshVersion)
                    return;
                spinner.Hide();
                profilesList.FadeTo(1f, 150);
                statusText.Text = "Could not load your profiles.";
                retryButton.Show();
            });

            api.Queue(req);
        }

        private partial class ProfileCard : Container
        {
            private readonly APIToriiSubProfile profile;
            private readonly System.Action onSwitched;
            private RoundedButton switchButton = null!;
            private bool switching;

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
                                            new TruncatingSpriteText
                                            {
                                                Text = profile.Name,
                                                MaxWidth = 190,
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

                return switchButton = new RoundedButton
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
                if (profile.ModPolicy is { } policy)
                    return policy.Required.Length > 0 ? osu.Game.Configuration.ProfileModPolicy.Describe(policy.Required) : policy.AllowOtherMods ? "Any mods" : "No required mods";
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
#if DEBUG
                if (SubProfilePreview.Enabled)
                {
                    SubProfilePreview.Switch(profile.Id);
                    onSwitched();
                    return;
                }
#endif
                if (switching)
                    return;
                switching = true;
                switchButton.Enabled.Value = false;
                switchButton.Text = "...";
                APIRequest switchReq;
                if (profile.IsMaster)
                {
                    var req = new SwitchToMasterSubProfileRequest();
                    req.Success += resp => Schedule(() =>
                    {
                        api.SwitchSubProfile(resp, () => Schedule(() =>
                        {
                            onSwitched();
                            notifications?.Post(new SimpleNotification { Text = "Switched to Main profile.", Icon = FontAwesome.Solid.User });
                        }), error => Schedule(() => showSwitchFailure(error)));
                    });
                    switchReq = req;
                }
                else
                {
                    var req = new SwitchToriiSubProfileRequest(profile.Id);
                    req.Success += resp => Schedule(() =>
                    {
                        api.SwitchSubProfile(resp, () => Schedule(() =>
                        {
                            onSwitched();
                            notifications?.Post(new SimpleNotification { Text = $"Switched to {resp.Name}.", Icon = FontAwesome.Solid.User });
                        }), error => Schedule(() => showSwitchFailure(error)));
                    });
                    switchReq = req;
                }

                switchReq.Failure += err => Schedule(() =>
                {
                    showSwitchFailure(err);
                });

                api.Queue(switchReq);
            }

            private void showSwitchFailure(System.Exception error)
            {
                switching = false;
                switchButton.Enabled.Value = true;
                switchButton.Text = "Switch";
                notifications?.Post(new SimpleNotification { Text = $"Could not switch profile: {error.Message}", Icon = FontAwesome.Solid.Times });
            }
        }

        public partial class AddProfileButton : RoundedButton
        {
            [Resolved]
            private IDialogOverlay dialogs { get; set; } = null!;
            public int NextSlotCost { get; set; }
            public System.Action? OnCreated { get; set; }

            public AddProfileButton()
            {
                Text = "New profile";
                Action = () => dialogs.Push(new CreateProfileDialog(() => OnCreated?.Invoke(), NextSlotCost));
            }
        }
    }
}
