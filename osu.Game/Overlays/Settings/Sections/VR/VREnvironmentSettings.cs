// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Platform.OpenXR;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.VR;

namespace osu.Game.Overlays.Settings.Sections.VR
{
    /// <summary>
    /// Torii: what surrounds the game in VR, and comfort options for it.
    /// </summary>
    public partial class VREnvironmentSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Environment";

        private FormEnumDropdown<VREnvironment> environmentDropdown = null!;
        private SettingsItemV2 environment = null!;
        private FormCheckBox hideBoundaryCheckBox = null!;
        private SettingsItemV2 hideBoundary = null!;

        private IBindable<bool>? supportsPassthrough;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, GameHost host)
        {
            Children = new Drawable[]
            {
                environment = new SettingsItemV2(environmentDropdown = new FormEnumDropdown<VREnvironment>
                {
                    Caption = "Environment",
                    HintText = "What surrounds the game panel.",
                    Current = config.GetBindable<VREnvironment>(OsuSetting.VREnvironment),
                })
                {
                    Keywords = new[] { "vr", "environment", "passthrough", "mixed reality", "room", "field" },
                },
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = "Playfield backing",
                    HintText = "Darkens the area behind gameplay.",
                    Current = config.GetBindable<float>(OsuSetting.VRPlayfieldBacking),
                    DisplayAsPercentage = true,
                    LabelFormat = v => v == 0 ? CommonStrings.Disabled : FormSliderBar<float>.DefaultLabelFormat(v, true),
                })
                {
                    Keywords = new[] { "vr", "backing", "dim", "readability", "passthrough" },
                },
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = "Flash reduction",
                    HintText = "Softens sudden flashes on the custom field's background wall.",
                    Current = config.GetBindable<float>(OsuSetting.VRFlashReduction),
                    DisplayAsPercentage = true,
                    LabelFormat = v => v == 0 ? CommonStrings.Disabled : FormSliderBar<float>.DefaultLabelFormat(v, true),
                })
                {
                    Keywords = new[] { "vr", "flash", "photosensitivity", "epilepsy", "strobe", "storyboard" },
                },
                hideBoundary = new SettingsItemV2(hideBoundaryCheckBox = new FormCheckBox
                {
                    Caption = "Hide boundary in passthrough",
                    HintText = "Hides the headset's boundary while your room is shown, where the headset allows it.",
                    Current = config.GetBindable<bool>(OsuSetting.VRHideBoundaryInPassthrough),
                })
                {
                    Keywords = new[] { "vr", "guardian", "boundary", "passthrough", "room" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Storyboard overlay in front of the playfield",
                    HintText = "Otherwise it's shown on the far background. Applies from the next play.",
                    Current = config.GetBindable<bool>(OsuSetting.VRStoryboardOverlayInFront),
                })
                {
                    Keywords = new[] { "vr", "storyboard", "overlay", "flash" },
                },
            };

            // Not every runtime can show the room (e.g. SteamVR): passthrough can't be chosen there, and its options are disabled, with
            // notes saying why.
            if (host is IOpenXRHost xrHost)
            {
                supportsPassthrough = xrHost.OpenXR.SupportsPassthrough.GetBoundCopy();
                supportsPassthrough.BindValueChanged(supported => Schedule(() =>
                {
                    bool available = supported.NewValue;
                    string where = xrHost.OpenXR.RuntimeName.StartsWith("SteamVR", StringComparison.Ordinal) ? "in SteamVR" : "with this VR runtime";

                    environmentDropdown.Items = Enum.GetValues<VREnvironment>().Where(e => available || e != VREnvironment.Passthrough);
                    environment.Note.Value = available ? null : new SettingsNote.Data($"Passthrough is unavailable {where}.", SettingsNote.Type.Informational);

                    hideBoundaryCheckBox.Current.Disabled = !available;
                    hideBoundary.Note.Value = available ? null : new SettingsNote.Data($"Unavailable {where}.", SettingsNote.Type.Informational);
                }), true);
            }
        }
    }
}
