// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings.Entries;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Torii
{
    /// <summary>
    /// Torii-specific gameplay-flow tweaks. Houses the long-attempt Retry/Quit
    /// confirmation toggle and the mid-map break-skip toggles; intended as the
    /// home for any future "modify how the gameplay screens behave" preferences
    /// so they don't pollute Interface (visuals) or Server (networking).
    /// </summary>
    public partial class ToriiGameplaySettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Gameplay";

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                CreateItem(new CheckboxEntry
                {
                    Caption = "Confirm Retry/Quit on long attempts",
                    Current = config.GetBindable<bool>(OsuSetting.ToriiConfirmDangerousButtonsOnLongAttempts),
                    HintText = "After ~60 seconds of active gameplay, Retry and Quit on the pause and fail screens require a second click within 5 seconds. Continue is unaffected.",
                    Keywords = new[] { @"retry", @"quit", @"confirm", @"pause", @"fail", @"double", @"click", @"misclick", @"torii" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = ToriiSettingsStrings.ConstantHitsoundVolume,
                    Current = config.GetBindable<bool>(OsuSetting.ToriiConstantHitsoundVolume),
                    HintText = ToriiSettingsStrings.ConstantHitsoundVolumeHint,
                    Keywords = new[] { @"hitsound", @"hitsounds", @"volume", @"constant", @"quiet", @"silent", @"mcosu", @"torii" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = ToriiSettingsStrings.SkipBreaks,
                    Current = config.GetBindable<bool>(OsuSetting.ToriiSkipBreaksEnabled),
                    HintText = ToriiSettingsStrings.SkipBreaksHint,
                    Keywords = new[] { @"skip", @"break", @"breaks", @"mid", @"map", @"fast", @"forward", @"torii" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = ToriiSettingsStrings.SkipBreaksSingleConfirmation,
                    Current = config.GetBindable<bool>(OsuSetting.ToriiSkipBreaksSingleConfirmation),
                    HintText = ToriiSettingsStrings.SkipBreaksSingleConfirmationHint,
                    Keywords = new[] { @"skip", @"break", @"breaks", @"single", @"confirmation", @"double", @"press", @"misclick", @"torii" },
                }),
            };
        }
    }
}
