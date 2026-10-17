// Copyright (c) GooGuTeam & Torii. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.ToriiBriefing
{
    /// <summary>
    /// The primary confirmation / dismiss action button on the Torii Results briefing dialog.
    /// </summary>
    internal partial class ResultsPrimaryActionButton : OsuClickableContainer
    {
        private readonly LocalisableString label;
        private readonly Color4 accent;

        private Box background = null!;

        public ResultsPrimaryActionButton(LocalisableString label, Color4 accent)
        {
            this.label = label;
            this.accent = accent;

            Width = 320;
            Height = 44;
            Masking = true;
            CornerRadius = BriefingTheme.CornerSm;
            CornerExponent = BriefingTheme.SquircleExponent;
            MaskingSmoothness = 1.2f;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientVertical(accent.Lighten(0.08f), accent.Darken(0.12f)),
                    Alpha = 0.95f,
                },
                // Top highlight
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 16,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(
                            Color4.White.Opacity(0.24f),
                            Color4.White.Opacity(0)),
                    },
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.Check,
                            Size = new Vector2(12),
                            Colour = BriefingTheme.SurfaceBase,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = label,
                            Font = OsuFont.GetFont(size: BriefingTheme.TypeBody, weight: FontWeight.Bold),
                            Colour = BriefingTheme.SurfaceBase,
                        },
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeTo(1f, BriefingTheme.HoverDuration, Easing.OutQuint);
            this.ScaleTo(1.02f, BriefingTheme.HoverDuration, Easing.OutQuint);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeTo(0.95f, BriefingTheme.HoverDuration, Easing.OutQuint);
            this.ScaleTo(1f, BriefingTheme.HoverDuration, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            this.ScaleTo(0.98f, 60, Easing.OutQuint);
            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            this.ScaleTo(IsHovered ? 1.02f : 1f, 120, Easing.OutQuint);
            base.OnMouseUp(e);
        }
    }
}
