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
    /// An encapsulated option button used in the results screen briefing dialog
    /// to switch between results screen layouts (e.g. Lazer Classic or Torii Stable).
    /// </summary>
    internal partial class ResultsLayoutChoiceButton : OsuClickableContainer
    {
        private readonly IconUsage icon;
        private readonly LocalisableString label;
        private readonly Color4 accent;

        private Box background = null!;
        private Container borderContainer = null!;

        public ResultsLayoutChoiceButton(IconUsage icon, LocalisableString label, Color4 accent)
        {
            this.icon = icon;
            this.label = label;
            this.accent = accent;

            RelativeSizeAxes = Axes.X;
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
                    Colour = accent,
                    Alpha = 0.12f,
                },
                // Specular top highlight
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
                            Color4.White.Opacity(0.12f),
                            Color4.White.Opacity(0)),
                    },
                },
                borderContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = BriefingTheme.CornerSm,
                    CornerExponent = BriefingTheme.SquircleExponent,
                    MaskingSmoothness = 1.2f,
                    BorderThickness = 1.2f,
                    BorderColour = accent.Opacity(0.38f),
                    Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
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
                            Icon = icon,
                            Size = new Vector2(14),
                            Colour = accent,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = label,
                            Font = OsuFont.GetFont(size: BriefingTheme.TypeBody, weight: FontWeight.SemiBold),
                            Colour = Color4.White,
                        },
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeTo(0.24f, BriefingTheme.HoverDuration, Easing.OutQuint);
            borderContainer.TransformTo(nameof(BorderColour), (ColourInfo)accent.Opacity(0.85f), BriefingTheme.HoverDuration, Easing.OutQuint);
            this.ScaleTo(1.02f, BriefingTheme.HoverDuration, Easing.OutQuint);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeTo(0.12f, BriefingTheme.HoverDuration, Easing.OutQuint);
            borderContainer.TransformTo(nameof(BorderColour), (ColourInfo)accent.Opacity(0.38f), BriefingTheme.HoverDuration, Easing.OutQuint);
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
