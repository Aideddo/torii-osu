// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class GeneralSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.General;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                CreateItem(new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.CursorRotation,
                    Current = config.GetBindable<bool>(OsuSetting.CursorRotation),
                    Keywords = [@"spin"],
                }),
                CreateItem(new SliderEntry<float>
                {
                    Caption = UserInterfaceStrings.MenuCursorSize,
                    Current = config.GetBindable<float>(OsuSetting.MenuCursorSize),
                    KeyboardStep = 0.01f,
                    LabelFormat = v => $"{v:0.##}x"
                }),
                // Torii: three-way menu cursor style.
                //   LazerDefault → upstream menu cursor (the textured
                //                  arrow with the additive flash).
                //   SkinCursor   → user's skin gameplay cursor
                //                  (cursor.png + cursormiddle.png),
                //                  same pipeline as the playfield —
                //                  switches live when skin changes,
                //                  scales with GameplayCursorSize.
                //   ToriiCursor  → translucent pink ring with white
                //                  centre dot, scaled with
                //                  GameplayCursorSize. Used regardless
                //                  of skin (overrides skin cursor).
                // All three honour the GameplayCursorSize bindable
                // end-to-end and show the equipped cosmetic trail.
                new SettingsItemV2(new FormEnumDropdown<osu.Game.Graphics.Cursor.MenuCursorStyle>
                {
                    Caption = @"Menu cursor style",
                    Current = config.GetBindable<osu.Game.Graphics.Cursor.MenuCursorStyle>(OsuSetting.MenuCursorStyle),
                })
                {
                    Keywords = [@"cursor", @"gameplay", @"skin", @"torii"],
                },
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = UserInterfaceStrings.Parallax,
                    Current = config.GetBindable<float>(OsuSetting.MenuParallaxScale),
                    DisplayAsPercentage = true,
                    LabelFormat = v => v == 0 ? CommonStrings.Disabled : FormSliderBar<float>.DefaultLabelFormat(v, true),
                }),
                CreateItem(new SliderEntry<double>
                {
                    Caption = UserInterfaceStrings.HoldToConfirmActivationTime,
                    Current = config.GetBindable<double>(OsuSetting.UIHoldActivationDelay),
                    KeyboardStep = 50,
                    LabelFormat = v => $"{v:N0} ms",
                    Keywords = [@"delay"],
                    ClassicDefault = 0,
                }),
            };
        }
    }
}
