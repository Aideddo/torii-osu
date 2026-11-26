// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Overlays.Settings.Sections.VR;
using osu.Game.VR;

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>
    /// Torii: VR settings. Added to the settings overlay when running in VR, or where VR mode can be switched on (desktop): outside
    /// VR mode it only has that switch.
    /// </summary>
    public partial class VRSection : SettingsSection
    {
        public override LocalisableString Header => "VR";

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = FontAwesome.Solid.VrCardboard
        };

        [BackgroundDependencyLoader(true)]
        private void load(IVRModeControl? vrMode)
        {
            if (vrMode != null)
                Add(new VRModeSettings());

            if (VRRuntime.IsVR)
            {
                AddRange(new Drawable[]
                {
                    new VRPanelSettings(),
                    new VREnvironmentSettings(),
                    new VRControllerSettings(),
                });
            }
        }
    }
}
