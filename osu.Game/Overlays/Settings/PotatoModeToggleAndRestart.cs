// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings
{
    /// <summary>
    /// Potato Mode toggle for Settings → Torii → Interface. Potato Mode is
    /// an extreme-performance preset for weak PCs; it's read once at
    /// startup (see <see cref="osu.Game.Performance.PotatoMode"/>) and
    /// gates heavy-visual subsystems at construction, so flipping it
    /// mid-run would leave a torn mix of cheap and expensive surfaces.
    /// Changing it therefore prompts for a restart, mirroring the
    /// <see cref="UIThemeDropdownAndRestart"/> dropdown.
    /// </summary>
    public partial class PotatoModeToggleAndRestart : Container
    {
        // Resolved optionally because the test-scene host doesn't register
        // OsuGame / IDialogOverlay. In that case the checkbox binds without
        // the restart prompt — the test can flip the setting freely.
        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            var potatoBindable = config.GetBindable<bool>(OsuSetting.ToriiPotatoMode);

            InternalChild = new SettingsItemV2(new FormCheckBox
            {
                Caption = "Potato mode (extreme performance)",
                HintText = "For weak PCs. Strips almost all motion and effects: no animated triangles, "
                           + "no beat-synced pulsing anywhere (carousel cards, logo, menu buttons), no "
                           + "logo visualiser, no user auras or animated name colours, no cursor trails, "
                           + "no storyboards, no background blur, no hit lighting, no kiai flashes, no "
                           + "star fountains, no menu parallax and no seasonal backgrounds. Fully dims "
                           + "gameplay backgrounds, hides + silences the server pulse widget, and "
                           + "switches to the legacy audio engine to avoid stutters. Frees up GPU, CPU "
                           + "and memory for max FPS. Your own graphics settings are left untouched. "
                           + "Changing this option restarts the game.",
                Current = potatoBindable,
            })
            {
                Keywords = new[] { @"potato", @"extreme", @"mode", @"performance", @"fps", @"low end", @"weak", @"lag", @"stutter", @"perf", @"quality", @"battery" },
            };

            PotatoModeRestartCoordinator.EnsureRegistered(potatoBindable, game, dialogOverlay);
        }
    }

    /// <summary>
    /// Torii: checkbox for <see cref="OsuSetting.ToriiLightweightInterface"/>, which like Potato Mode is read once at startup.
    /// </summary>
    public partial class LightweightInterfaceToggleAndRestart : Container
    {
        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        private Bindable<bool> setting = null!;
        private bool reverting;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            setting = config.GetBindable<bool>(OsuSetting.ToriiLightweightInterface);

            InternalChild = new SettingsItemV2(new FormCheckBox
            {
                Caption = "Lightweight interface",
                HintText = "Gives up a few effects in menus that cost a lot of frame time for what they add: beatmap covers appear "
                           + "without fading in, and song select panels lose their frosted glass, beat pulse and moving triangles. "
                           + "On by default on mobile and standalone headsets. "
                           + "Much milder than Potato mode, which includes it. Changing this option restarts the game.",
                Current = setting,
            })
            {
                Keywords = new[] { @"lightweight", @"performance", @"fps", @"lag", @"stutter", @"perf", @"quest", @"vr", @"mobile", @"menu" },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            setting.BindValueChanged(change =>
            {
                if (reverting || dialogOverlay == null)
                    return;

                dialogOverlay.Push(new ConfirmDialog(
                    "The lightweight interface changes how menus are built, so the game needs to restart. It will close now, please open it again.",
                    () => game?.AttemptExit(),
                    () =>
                    {
                        reverting = true;
                        setting.Value = change.OldValue;
                        reverting = false;
                    }));
            });
        }
    }

    /// <summary>
    /// Process-wide one-shot subscription that owns the restart-confirm
    /// flow for <see cref="OsuSetting.ToriiPotatoMode"/>. Mirrors
    /// <c>UIThemeRestartCoordinator</c>: the toggle lives at a single call
    /// site today, but the central registration keeps the restart prompt
    /// from firing twice if it's ever mounted in two places (e.g. a future
    /// quick-toggle alongside the settings panel).
    /// </summary>
    internal static class PotatoModeRestartCoordinator
    {
        private static bool registered;
        private static readonly object register_lock = new object();

        // evita el loop infinito: al cancelar revertimos el valor, lo que re-dispara
        // BindValueChanged; este flag hace que ese re-disparo no vuelva a abrir el dialog.
        private static bool suppress;

        public static void EnsureRegistered(Bindable<bool> potatoBindable, OsuGame? game, IDialogOverlay? dialogOverlay)
        {
            if (registered)
                return;

            lock (register_lock)
            {
                if (registered)
                    return;

                potatoBindable.BindValueChanged(change =>
                {
                    // No-op on the initial bind (fires once with the current
                    // value); only a real user flip should prompt a restart.
                    // suppress: ignoramos el re-disparo que causa el revert de Cancel.
                    if (suppress || change.NewValue == change.OldValue)
                        return;

                    // Route through the confirm dialog rather than a
                    // Velopack auto-restart for the same reason the UI-theme
                    // toggle does: auto-restart can't resolve an executable
                    // in unpackaged (run-from-source) builds and surfaces a
                    // stray error toast. Closing cleanly and asking the user
                    // to reopen works for both packaged and unpackaged.
                    dialogOverlay?.Push(new ConfirmDialog(
                        "Potato mode changes a lot of visuals, so the game needs to restart. It will close now, please open it again.",
                        () => game?.AttemptExit(),
                        () =>
                        {
                            suppress = true;
                            potatoBindable.Value = change.OldValue;
                            suppress = false;
                        }));
                });

                registered = true;
            }
        }
    }
}
