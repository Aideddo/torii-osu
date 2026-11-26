// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osuTK.Graphics;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: a dark backing behind gameplay in VR, so hit objects stay readable over the room (passthrough) or the background layer.
    /// Not present outside VR.
    /// </summary>
    public partial class VRPlayfieldBacking : CompositeDrawable
    {
        private readonly Bindable<float> opacity = new Bindable<float>();

        public VRPlayfieldBacking()
        {
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;

            InternalChild = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Black,
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            if (!VRRuntime.IsVR)
                return;

            config.BindWith(OsuSetting.VRPlayfieldBacking, opacity);
            opacity.BindValueChanged(o => this.FadeTo(o.NewValue, 200), true);
        }
    }
}
