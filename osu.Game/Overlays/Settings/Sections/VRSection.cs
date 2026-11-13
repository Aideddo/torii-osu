// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Overlays.Settings.Sections.VR;

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>
    /// Torii: VR settings. Only added to the settings overlay when running in VR.
    /// </summary>
    public partial class VRSection : SettingsSection
    {
        public override LocalisableString Header => "VR";

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = FontAwesome.Solid.VrCardboard
        };

        public VRSection()
        {
            Children = new Drawable[]
            {
                new VRPanelSettings(),
                new VREnvironmentSettings(),
                new VRControllerSettings(),
            };
        }
    }
}
