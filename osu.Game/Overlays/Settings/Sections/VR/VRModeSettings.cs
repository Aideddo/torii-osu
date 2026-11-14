// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.VR;

namespace osu.Game.Overlays.Settings.Sections.VR
{
    /// <summary>
    /// Torii: switching a desktop game in and out of VR mode.
    /// </summary>
    public partial class VRModeSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "VR mode";

        /// <summary>
        /// How often availability is checked again while the settings are open, in milliseconds.
        /// </summary>
        private const double refresh_interval = 2000;

        [Resolved]
        private IVRModeControl vrMode { get; set; } = null!;

        private IBindable<bool> available = null!;
        private SettingsButtonV2 enableButton = null!;
        private OsuTextFlowContainer notice = null!;

        private double lastRefresh = double.MinValue;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            if (vrMode.Enabled)
            {
                Children = new Drawable[]
                {
                    new DangerousSettingsButtonV2
                    {
                        Text = "Disable VR mode",
                        TooltipText = "Restarts the game on the desktop.",
                        Keywords = new[] { "vr", "steamvr", "desktop", "exit", "leave" },
                        Action = vrMode.Disable,
                    },
                    createText("Closing SteamVR leaves VR mode too."),
                };

                return;
            }

            Children = new Drawable[]
            {
                enableButton = new SettingsButtonV2
                {
                    Text = "Enable VR mode",
                    TooltipText = "Restarts the game in your headset.",
                    Keywords = new[] { "vr", "steamvr", "headset" },
                    Action = vrMode.Enable,
                },
                notice = createText("Start SteamVR to be able to enable VR mode."),
            };

            notice.Colour = colours.Yellow;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (vrMode.Enabled)
                return;

            available = vrMode.Available.GetBoundCopy();
            available.BindValueChanged(a =>
            {
                enableButton.CanBeShown.Value = a.NewValue;
                notice.Alpha = a.NewValue ? 0 : 1;
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            // Only runs while the settings are open: checked as soon as they're opened, and kept up to date while they stay open.
            if (!vrMode.Enabled && Time.Current - lastRefresh >= refresh_interval)
            {
                lastRefresh = Time.Current;
                vrMode.Refresh();
            }
        }

        private static OsuTextFlowContainer createText(string text)
        {
            var flow = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 14))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = SettingsPanel.CONTENT_PADDING,
            };

            flow.AddText(text);
            return flow;
        }
    }
}
