// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.VR;

namespace osu.Game.Overlays.Settings.Sections.VR
{
    /// <summary>
    /// Torii: VR controller pointer tuning.
    /// </summary>
    public partial class VRControllerSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Controllers";

        private readonly Bindable<VRControllerModel> controllerModel = new Bindable<VRControllerModel>();

        private SettingsItemV2 penTilt = null!;
        private SettingsItemV2 penOffsetForward = null!;
        private SettingsItemV2 penOffsetUp = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.VRControllerModel, controllerModel);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Show controller models",
                    Current = config.GetBindable<bool>(OsuSetting.VRShowControllers),
                })
                {
                    Keywords = new[] { "vr", "controller", "model", "hands" },
                },
                new SettingsItemV2(new FormEnumDropdown<VRControllerModel>
                {
                    Caption = "Controller model",
                    Current = controllerModel,
                })
                {
                    Keywords = new[] { "vr", "controller", "model", "pen", "stylus", "tablet", "hands" },
                },
                penTilt = new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = "Pen tilt",
                    HintText = "How far the pen points down from the controller's aim.",
                    Current = config.GetBindable<float>(OsuSetting.VRPenTilt),
                    KeyboardStep = 1,
                    LabelFormat = v => $"{v:N0}°",
                })
                {
                    Keywords = new[] { "vr", "pen", "tilt", "angle", "pointer" },
                },
                penOffsetForward = new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = "Pen tip distance",
                    HintText = "How far ahead of the controller the pen's tip is.",
                    Current = config.GetBindable<float>(OsuSetting.VRPenOffsetForward),
                    KeyboardStep = 0.5f,
                    LabelFormat = v => $"{v:0.#} cm",
                })
                {
                    Keywords = new[] { "vr", "pen", "offset", "tip", "length", "pointer" },
                },
                penOffsetUp = new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = "Pen height",
                    HintText = "Moves the pen up or down in your hand.",
                    Current = config.GetBindable<float>(OsuSetting.VRPenOffsetUp),
                    KeyboardStep = 0.5f,
                    LabelFormat = v => $"{v:0.#} cm",
                })
                {
                    Keywords = new[] { "vr", "pen", "offset", "height", "pointer" },
                },
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = "Haptic feedback",
                    HintText = "Controller vibration during gameplay.",
                    Current = config.GetBindable<float>(OsuSetting.VRHapticStrength),
                    DisplayAsPercentage = true,
                    LabelFormat = v => v == 0 ? CommonStrings.Disabled : FormSliderBar<float>.DefaultLabelFormat(v, true),
                })
                {
                    Keywords = new[] { "vr", "haptics", "vibration", "rumble", "controller" },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            controllerModel.BindValueChanged(m =>
            {
                bool pen = m.NewValue == VRControllerModel.Pen;
                penTilt.CanBeShown.Value = pen;
                penOffsetForward.CanBeShown.Value = pen;
                penOffsetUp.CanBeShown.Value = pen;
            }, true);
        }
    }
}
