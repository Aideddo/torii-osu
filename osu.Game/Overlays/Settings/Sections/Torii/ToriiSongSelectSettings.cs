// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings.Sections.Torii
{
    public partial class ToriiSongSelectSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Torii Classic (Stable)";

        private readonly Dictionary<OsuSetting, BindableBool> staged = new Dictionary<OsuSetting, BindableBool>();
        private readonly Dictionary<OsuSetting, Bindable<bool>> persisted = new Dictionary<OsuSetting, Bindable<bool>>();
        private Bindable<UIThemeOption> theme = null!;
        private SettingsButtonV2 applyButton = null!;
        private OsuTextFlowContainer hint = null!;
        private OsuConfigManager config = null!;

        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogs { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, OsuColour colours)
        {
            this.config = config;
            Children = new Drawable[]
            {
                checkbox(OsuSetting.ToriiStableUISongSelect, "Classic Song Select, Carousel & Leaderboard"),
                checkbox(OsuSetting.ToriiStableUIResults, "Stable Results Screen"),
                checkbox(OsuSetting.ToriiStableUIMods, "Stable Mod Select Screen"),
                checkbox(OsuSetting.ToriiStableUIFooter, "Stable Footer & Buttons"),
                checkbox(OsuSetting.ToriiStableUIFonts, "Stable Classic Fonts"),
                hint = new OsuTextFlowContainer(t => t.Font = OsuFont.Style.Caption1)
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                },
                applyButton = new SettingsButtonV2
                {
                    Text = "Apply Changes to Theme",
                    BackgroundColour = colours.Pink,
                    Action = apply,
                    Enabled = { Value = false },
                    Keywords = new[] { "stable", "theme", "apply", "restart" },
                },
            };

            theme = config.GetBindable<UIThemeOption>(OsuSetting.UITheme);
            theme.BindValueChanged(_ => Schedule(updatePendingState));
            updatePendingState();
        }

        private SettingsItemV2 checkbox(OsuSetting setting, string caption)
        {
            var value = new BindableBool(config.Get<bool>(setting));
            staged.Add(setting, value);
            value.BindValueChanged(_ => updatePendingState());
            var saved = config.GetBindable<bool>(setting);
            persisted.Add(setting, saved);
            saved.BindValueChanged(_ => Schedule(updatePendingState));
            return new SettingsItemV2(new FormCheckBox { Caption = caption, Current = value })
            {
                ShowRevertToDefaultButton = false,
                Keywords = new[] { "stable", "legacy", "retro", "theme", caption },
            };
        }

        private void updatePendingState()
        {
            bool hasPendingChanges = staged.Any(pair => pair.Value.Value != config.Get<bool>(pair.Key));
            applyButton.Enabled.Value = hasPendingChanges;
            hint.Text = config.Get<UIThemeOption>(OsuSetting.UITheme) != UIThemeOption.StableUI
                ? "These settings apply when you select Torii Classic (Stable) in the theme settings."
                : "Changes take effect after restarting the game.";
        }

        private void apply()
        {
            if (!applyButton.Enabled.Value)
                return;

            foreach (var pair in staged)
                config.SetValue(pair.Key, pair.Value.Value);

            UIThemeRestartCoordinator.ApplyStableUI(config, game, dialogs);
            updatePendingState();
        }
    }
}
