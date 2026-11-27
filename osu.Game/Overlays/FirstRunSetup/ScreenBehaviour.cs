// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Mods.Input;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Rendering;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Overlays.FirstRunSetup
{
    [LocalisableDescription(typeof(FirstRunSetupOverlayStrings), nameof(FirstRunSetupOverlayStrings.Behaviour))]
    public partial class ScreenBehaviour : WizardScreen
    {
        [Cached(typeof(ISettingsRenderer))]
        private readonly ISettingsRenderer renderer = LazerSettingsRenderer.DEFAULT;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private RulesetStore? rulesetStore { get; set; }

        private readonly List<(Func<bool> isDefault, Action setDefault)> trackedBindables = new List<(Func<bool>, Action)>();
        private SettingsSubsection? osuRulesetSettings;
        private OsuSpriteText statusText = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            var scoreDisplayMode = track(config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode));
            var modHotkeyStyle = track(config.GetBindable<ModSelectHotkeyStyle>(OsuSetting.ModSelectHotkeyStyle));
            var modSearchFocus = track(config.GetBindable<bool>(OsuSetting.ModSelectTextSearchStartsActive));
            var holdActivationDelay = track(config.GetBindable<double>(OsuSetting.UIHoldActivationDelay));

            var showHealthWhenCantFail = track(config.GetBindable<bool>(OsuSetting.ShowHealthDisplayWhenCantFail));
            var fadePlayfieldLowHealth = track(config.GetBindable<bool>(OsuSetting.FadePlayfieldWhenHealthLow));
            var hitLighting = track(config.GetBindable<bool>(OsuSetting.HitLighting));
            var dimLevel = track(config.GetBindable<double>(OsuSetting.DimLevel));
            var blurLevel = track(config.GetBindable<double>(OsuSetting.BlurLevel));

            var alwaysFirstComboBreak = track(config.GetBindable<bool>(OsuSetting.AlwaysPlayFirstComboBreak));
            var positionalHitsounds = track(config.GetBindable<float>(OsuSetting.PositionalHitsoundsLevel));
            var beatmapSkins = track(config.GetBindable<bool>(OsuSetting.BeatmapSkins));
            var beatmapColours = track(config.GetBindable<bool>(OsuSetting.BeatmapColours));
            var beatmapHitsounds = track(config.GetBindable<bool>(OsuSetting.BeatmapHitsounds));

            var keyDebounceEnabled = track(config.GetBindable<bool>(OsuSetting.ToriiKeyDebounceEnabled));
            var keyDebounceThreshold = track(config.GetBindable<double>(OsuSetting.ToriiKeyDebounceThresholdMs));

            var osuRulesetInfo = rulesetStore?.GetRuleset(0);
            if (osuRulesetInfo != null)
                osuRulesetSettings = osuRulesetInfo.CreateInstance().CreateSettings();

            Content.Children = new Drawable[]
            {
                new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Text = FirstRunSetupOverlayStrings.BehaviourDescription,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        new RoundedButton
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Width = 340,
                            Text = FirstRunSetupOverlayStrings.ToriiRecommendedButton,
                            Action = applyToriiRecommended,
                        },
                        statusText = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                            Colour = OverlayColourProvider.Highlight1,
                            Alpha = 0,
                        },
                    }
                },
                createCategoryHeader(@"General & Mod Select"),
                new SettingsItemV2(new FormEnumDropdown<ScoringMode>
                {
                    Caption = GameplaySettingsStrings.ScoreDisplayMode,
                    Current = scoreDisplayMode,
                }),
                new SettingsItemV2(new FormEnumDropdown<ModSelectHotkeyStyle>
                {
                    Caption = UserInterfaceStrings.ModSelectHotkeyStyle,
                    Current = modHotkeyStyle,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.ModSelectTextSearchStartsActive,
                    Current = modSearchFocus,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = UserInterfaceStrings.HoldToConfirmActivationTime,
                    Current = holdActivationDelay,
                    KeyboardStep = 50,
                }),
                createCategoryHeader(@"Gameplay & Visuals (torii)"),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = GameplaySettingsStrings.ShowHealthDisplayWhenCantFail,
                    Current = showHealthWhenCantFail,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = GameplaySettingsStrings.FadePlayfieldWhenHealthLow,
                    Current = fadePlayfieldLowHealth,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = GraphicsSettingsStrings.HitLighting,
                    Current = hitLighting,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = GameplaySettingsStrings.BackgroundDim,
                    Current = dimLevel,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = GameplaySettingsStrings.BackgroundBlur,
                    Current = blurLevel,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                }),
                osuRulesetSettings ?? Empty(),
                createCategoryHeader(@"Audio & Skins"),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = GameplaySettingsStrings.AlwaysPlayFirstComboBreak,
                    Current = alwaysFirstComboBreak,
                }),
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = AudioSettingsStrings.PositionalLevel,
                    Current = positionalHitsounds,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                    LabelFormat = v => v == 0 ? CommonStrings.Disabled : FormSliderBar<float>.DefaultLabelFormat(v, true),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SkinSettingsStrings.BeatmapSkins,
                    Current = beatmapSkins,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SkinSettingsStrings.BeatmapColours,
                    Current = beatmapColours,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SkinSettingsStrings.BeatmapHitsounds,
                    Current = beatmapHitsounds,
                }),
                createCategoryHeader(@"Input & Anti-Chatter"),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"Filter double-taps (rapid trigger / chatter)",
                    HintText = @"Drops a gameplay key press that fires within the threshold of that key's last release. Gameplay keys only.",
                    Current = keyDebounceEnabled,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Threshold (ms)",
                    Current = keyDebounceThreshold,
                    TransferValueOnCommit = true,
                }),
            };
        }

        private Bindable<T> track<T>(Bindable<T> bindable)
        {
            trackedBindables.Add((() => bindable.IsDefault, bindable.SetDefault));
            return bindable;
        }

        private Drawable createCategoryHeader(string title) => new OsuSpriteText
        {
            Text = title,
            Font = OsuFont.Default.With(size: HEADER_FONT_SIZE, weight: FontWeight.Bold),
            Colour = OverlayColourProvider.Content1,
            Margin = new MarginPadding { Top = 10, Bottom = 2 },
        };

        private void applyToriiRecommended()
        {
            bool allAlreadyDefault = trackedBindables.All(b => b.isDefault());

            if (osuRulesetSettings != null)
            {
                foreach (var boolControl in osuRulesetSettings.ChildrenOfType<IHasCurrentValue<bool>>())
                {
                    if (!boolControl.Current.IsDefault)
                    {
                        allAlreadyDefault = false;
                        break;
                    }
                }
            }

            if (allAlreadyDefault)
            {
                showStatus(FirstRunSetupOverlayStrings.ToriiRecommendedAlreadyActive, FontAwesome.Solid.InfoCircle);
                return;
            }

            foreach (var (_, setDefault) in trackedBindables)
                setDefault();

            if (osuRulesetSettings != null)
            {
                foreach (var item in osuRulesetSettings.ChildrenOfType<ISettingsItem>())
                    item.ApplyDefault();
            }

            showStatus(FirstRunSetupOverlayStrings.ToriiRecommendedApplied, FontAwesome.Solid.Check);
        }

        private void showStatus(LocalisableString message, IconUsage icon)
        {
            statusText.Text = message;
            statusText.FadeOutFromOne(4000, Easing.InQuint);

            notificationOverlay?.Post(new SimpleNotification
            {
                Text = message,
                Icon = icon,
            });
        }
    }
}
