// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Overlays.Login
{
    public partial class ProfileModSummary : FillFlowContainer
    {
        public ProfileModSummary()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 6);
        }

        public void SetMods(APIMod[] mods, RulesetInfo? ruleset, bool required, string emptyText)
        {
            Clear();
            Add(new OsuSpriteText
            {
                Text = required ? "Required mods" : "Optional mods",
                Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
            });
            if (mods.Length == 0 || ruleset == null)
            {
                Add(new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = emptyText,
                });
                return;
            }
            var instance = ruleset.CreateInstance();
            foreach (var configured in mods)
            {
                var mod = configured.ToMod(instance);
                Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    CornerRadius = 6,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.FromHex("30303b") },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding(8),
                            ColumnDimensions = new[] { new Dimension(GridSizeMode.Absolute, 64), new Dimension() },
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new ModIcon(mod, showTooltip: false, showExtendedInformation: false) { Scale = new Vector2(0.6f) },
                                    new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 17, weight: FontWeight.SemiBold))
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = configured.Acronym == "DA" ? ProfileModPolicy.Describe(new[] { configured }) : mod.Name,
                                    },
                                },
                            },
                        },
                    },
                });
            }
        }
    }
}
