// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens;
using osu.Game.Screens.Menu;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: once the game has started outside VR mode (and the first-run setup is out of the way), offers to switch to VR mode if it's
    /// available. After leaving VR mode it says why instead.
    /// </summary>
    public partial class VRModePrompt : Component
    {
        private readonly IVRModeControl vrMode;
        private readonly FirstRunSetupOverlay firstRunSetup;

        [Resolved]
        private IDialogOverlay dialogOverlay { get; set; } = null!;

        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

        [Resolved]
        private IPerformFromScreenRunner performer { get; set; } = null!;

        private readonly Bindable<bool> showFirstRunSetup = new Bindable<bool>();
        private IBindable<Visibility> firstRunSetupState = null!;

        private bool handled;

        public VRModePrompt(IVRModeControl vrMode, FirstRunSetupOverlay firstRunSetup)
        {
            this.vrMode = vrMode;
            this.firstRunSetup = firstRunSetup;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.ShowFirstRunSetup, showFirstRunSetup);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (vrMode.Enabled)
                return;

            // Setup comes first: the offer waits until it's been completed (or was, on an earlier run) and its overlay is closed.
            firstRunSetupState = firstRunSetup.State.GetBoundCopy();
            firstRunSetupState.BindValueChanged(_ => Scheduler.AddOnce(check));
            showFirstRunSetup.BindValueChanged(_ => Scheduler.AddOnce(check), true);
        }

        private void check()
        {
            if (handled || showFirstRunSetup.Value || firstRunSetupState.Value == Visibility.Visible)
                return;

            handled = true;

            // Over the main menu, like the first-run setup: not on top of whatever the game was started for (e.g. an import).
            performer.PerformFromScreen(_ =>
            {
                if (vrMode.StartupMessage is string message)
                {
                    notifications.Post(new SimpleNotification
                    {
                        Icon = FontAwesome.Solid.VrCardboard,
                        Text = message,
                    });
                }

                if (!vrMode.OfferOnStartup)
                    return;

                vrMode.Refresh();

                Logger.Log($"VR mode is {(vrMode.Available.Value ? "available: offering to switch to it" : "not available (SteamVR isn't running)")}.");

                if (vrMode.Available.Value)
                    dialogOverlay.Push(new VRModeDialog(vrMode));
            }, new[] { typeof(MainMenu) });
        }

        private partial class VRModeDialog : PopupDialog
        {
            public VRModeDialog(IVRModeControl vrMode)
            {
                Icon = FontAwesome.Solid.VrCardboard;
                HeaderText = "SteamVR is running";
                BodyText = "Switch to VR mode? The game restarts and continues in your headset.\nYou can also switch later from Settings > VR.";

                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogOkButton
                    {
                        Text = "Switch to VR mode",
                        Action = vrMode.Enable,
                    },
                    new PopupDialogCancelButton
                    {
                        Text = "Stay on the desktop",
                    },
                };
            }
        }
    }
}
