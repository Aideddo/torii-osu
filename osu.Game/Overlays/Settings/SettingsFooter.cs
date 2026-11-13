// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osu.Game.Overlays.Dialog;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings
{
    public partial class SettingsFooter : FillFlowContainer
    {
        [BackgroundDependencyLoader]
        private void load(OsuGameBase game, RulesetStore rulesets)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Padding = new MarginPadding { Top = 20, Bottom = 30, Left = SettingsPanel.CONTENT_PADDING.Left, Right = SettingsPanel.CONTENT_PADDING.Right };

            FillFlowContainer modes;

            Children = new Drawable[]
            {
                modes = new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Direction = FillDirection.Full,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Spacing = new Vector2(5),
                    Padding = new MarginPadding { Bottom = 10 },
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = game.Name,
                    Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                },
                new BuildDisplay(game.Version)
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                },
                new CreditsButton
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Margin = new MarginPadding { Top = 8 },
                },
            };

            foreach (var ruleset in rulesets.AvailableRulesets)
            {
                try
                {
                    var icon = new ConstrainedIconContainer
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Icon = ruleset.CreateInstance().CreateIcon(),
                        Colour = Color4.Gray,
                        Size = new Vector2(20),
                    };

                    modes.Add(icon);
                }
                catch (Exception e)
                {
                    RulesetStore.LogRulesetFailure(ruleset, e);
                }
            }
        }

        internal partial class CreditsDialog : PopupDialog
        {
            public CreditsDialog()
            {
                HeaderText = "With thanks";
                BodyText = "GooGuTeam (g0v0) - server and client contributions.\n\n"
                           + "shigetiro - the client work Torii builds on.\n\n"
                           + "telecomadm1145 - Stable UI and 2B/Aspire improvements, integrated with permission.\n\n"
                           + "ppy and contributors - osu! and osu!framework.\n\n"
                           + "Third-party licences and copyright notices are included with Torii. Credits do not imply endorsement.";
                Icon = FontAwesome.Solid.Heart;
                Buttons = new PopupDialogButton[] { new PopupDialogOkButton { Text = "Close" } };
            }
        }

        private partial class CreditsButton : OsuAnimatedButton, IHasTooltip
        {
            public LocalisableString TooltipText => "Thanks to our contributors";

            public CreditsButton()
            {
                Size = new Vector2(30);
                Content.Child = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(14),
                    Icon = FontAwesome.Solid.Heart,
                    Colour = Color4.HotPink,
                };
            }

            [BackgroundDependencyLoader(true)]
            private void load(IDialogOverlay? dialogs)
            {
                Enabled.Value = dialogs != null;
                Action = () => dialogs?.Push(new CreditsDialog());
            }
        }

        private partial class BuildDisplay : OsuAnimatedButton, IHasContextMenu
        {
            private readonly string version;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            [Resolved]
            private OsuGame? game { get; set; }

            public BuildDisplay(string version)
            {
                this.version = version;

                Content.RelativeSizeAxes = Axes.Y;
                Content.AutoSizeAxes = AutoSizeAxes = Axes.X;
                Height = 20;
            }

            [BackgroundDependencyLoader]
            private void load(ChangelogOverlay? changelog)
            {
                Action = () => changelog?.ShowBuild(version);

                Add(new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 16),

                    Text = version,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Padding = new MarginPadding(5),
                    Colour = DebugUtils.IsDebugBuild ? colours.Red : Color4.White,
                });
            }

            public MenuItem[] ContextMenuItems => new MenuItem[]
            {
                new OsuMenuItem(SettingsStrings.CopyVersion, MenuItemType.Standard, () => game?.CopyToClipboard(version))
            };
        }
    }
}
