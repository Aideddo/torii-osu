// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Rendering;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    public abstract partial class PlaySliderBody : SnakingSliderBody
    {
        protected IBindable<float> ScaleBindable { get; private set; } = null!;

        protected IBindable<Color4> AccentColourBindable { get; private set; } = null!;

        private IBindable<int> pathVersion = null!;

        [Resolved(CanBeNull = true)]
        private OsuRulesetConfigManager? config { get; set; }

        private readonly Bindable<bool> configSnakingOut = new Bindable<bool>();
        private readonly Bindable<int> viewportSize = new Bindable<int>();

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, DrawableHitObject drawableObject)
        {
            var drawableSlider = (DrawableSlider)drawableObject;

            ScaleBindable = drawableSlider.ScaleBindable.GetBoundCopy();
            ScaleBindable.BindValueChanged(scale => PathRadius = OsuHitObject.OBJECT_RADIUS * scale.NewValue, true);

            pathVersion = drawableSlider.PathVersion.GetBoundCopy();
            pathVersion.BindValueChanged(_ => Scheduler.AddOnce(Refresh));

            AccentColourBindable = drawableObject.AccentColour.GetBoundCopy();
            AccentColourBindable.BindValueChanged(accent => AccentColour = GetBodyAccentColour(skin, accent.NewValue), true);

            config?.BindWith(OsuRulesetSetting.SnakingInSliders, SnakingIn);
            config?.BindWith(OsuRulesetSetting.SnakingOutSliders, configSnakingOut);
            config?.BindWith(OsuRulesetSetting.SliderViewportSize, viewportSize);

            SnakingOut.BindTo(configSnakingOut);

            BorderColour = GetBorderColour(skin);
        }

        protected virtual Color4 GetBorderColour(ISkinSource skin) => Color4.White;

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            float limit = viewportSize.Value > 0 ? viewportSize.Value : renderer.MaxTextureSize;
            Vector2 start = ToScreenSpace(Vector2.Zero);
            Vector2 screenSize = new Vector2(
                (ToScreenSpace(new Vector2(Path.Width, 0)) - start).Length,
                (ToScreenSpace(new Vector2(0, Path.Height)) - start).Length);
            Vector2 scale = GetViewportScale(screenSize, limit);
            Vector2 anchor = ToLocalSpace(new Vector2(Math.Max(0, start.X), Math.Max(0, start.Y)));
            if (!float.IsFinite(anchor.X) || !float.IsFinite(anchor.Y))
                anchor = Vector2.Zero;

            // Compress only the rendered stroke, retaining the snaking offset and gameplay geometry.
            Path.Scale = scale;
            Path.Position = SnakedPathPosition * scale + anchor * (Vector2.One - scale);
        }

        public static Vector2 GetViewportScale(Vector2 screenSize, float limit)
        {
            if (!float.IsFinite(limit) || limit <= 0)
                return Vector2.One;
            return new Vector2(
                float.IsFinite(screenSize.X) && screenSize.X > 0 ? Math.Min(1, limit / screenSize.X) : 1,
                float.IsFinite(screenSize.Y) && screenSize.Y > 0 ? Math.Min(1, limit / screenSize.Y) : 1);
        }

        protected virtual Color4 GetBodyAccentColour(ISkinSource skin, Color4 hitObjectAccentColour) => hitObjectAccentColour;
    }
}
