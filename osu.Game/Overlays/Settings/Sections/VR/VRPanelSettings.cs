// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Overlays.Settings.Sections.VR
{
    /// <summary>
    /// Torii: size and distance of the VR game panel. Also changed by grabbing the panel with a controller's grip.
    /// </summary>
    public partial class VRPanelSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Panel";

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = "Panel size",
                    HintText = "How wide the panel is. It can also be grabbed with the grip button to move, turn or resize it.",
                    Current = config.GetBindable<float>(OsuSetting.VRPanelSize),
                    KeyboardStep = 1,
                    LabelFormat = v => $"{v:N0}°",
                })
                {
                    Keywords = new[] { "vr", "panel", "size", "scale", "window" },
                },
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = "Panel distance",
                    HintText = "How far the panel is from you.",
                    Current = config.GetBindable<float>(OsuSetting.VRPanelDistance),
                    KeyboardStep = 0.1f,
                    LabelFormat = v => $"{v:N1} m",
                })
                {
                    Keywords = new[] { "vr", "panel", "distance", "depth", "window" },
                },
                new SettingsButtonV2
                {
                    Text = "Reset panel position",
                    Keywords = new[] { "vr", "panel", "position", "placement", "tilt", "rotation", "reset", "center", "centre" },
                    Action = () => config.SetValue(OsuSetting.VRPanelPlacement, string.Empty),
                },
            };
        }
    }
}
