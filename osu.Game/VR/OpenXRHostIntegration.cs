// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform.OpenXR;
using osu.Game.Configuration;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: applies the VR settings to an OpenXR host, writes back panel layout changes made by hand, and feeds it music data.
    /// Used by every VR build (the Quest, and desktop runs against a runtime such as Monado).
    /// </summary>
    public partial class OpenXRHostIntegration : CompositeComponent
    {
        private readonly OpenXRHostController xr;

        // Config bindables are bound copies held weakly by the config manager: they must be kept alive here, or the bindings
        // silently stop firing once they're garbage collected.
        private readonly List<IBindable> bindables = new List<IBindable>();

        private Bindable<float> panelSize = null!;
        private Bindable<float> panelDistance = null!;
        private Bindable<string> panelPlacement = null!;

        private IBindable<bool> supportsPassthrough = null!;

        private Bindable<VRControllerModel>? controllerModel;
        private Bindable<float>? penTilt;
        private Bindable<float>? penOffsetForward;
        private Bindable<float>? penOffsetUp;

        public OpenXRHostIntegration(OpenXRHostController xr)
        {
            this.xr = xr;
        }

        // The scene's requests are per-frame: it's told about every frame from here.
        private Scene? scene;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, IVRScene? vrScene)
        {
            scene = vrScene as Scene;

            var environment = config.GetBindable<VREnvironment>(OsuSetting.VREnvironment);

            bind(environment, v => xr.Environment = v switch
            {
                VREnvironment.Passthrough => XrEnvironmentMode.Passthrough,
                VREnvironment.Field => XrEnvironmentMode.Field,
                _ => XrEnvironmentMode.Void,
            });

            // Runtimes without passthrough (e.g. SteamVR) would show nothing around the panel: fall back to the default environment.
            // (The settings don't offer passthrough there either.)
            supportsPassthrough = xr.SupportsPassthrough.GetBoundCopy();
            supportsPassthrough.BindValueChanged(supported => Schedule(() =>
            {
                if (!supported.NewValue && environment.Value == VREnvironment.Passthrough)
                    environment.SetDefault();
            }), true);

            // In passthrough the room is the background, so the background dim setting dims the room (never fully black).
            bind(config.GetBindable<double>(OsuSetting.DimLevel), v => xr.PassthroughOpacity = 1 - (float)v * 0.9f);

            bind(config.GetBindable<bool>(OsuSetting.VRHideBoundaryInPassthrough), v => xr.SuppressBoundaryInPassthrough = v);
            bind(config.GetBindable<float>(OsuSetting.VRFlashReduction), v => xr.FlashReduction = v);
            bind(config.GetBindable<bool>(OsuSetting.VRShowControllers), v => xr.ShowControllers = v);

            // The pen replaces the controllers in both hands; the pointer comes out of its tip.
            bind(controllerModel = config.GetBindable<VRControllerModel>(OsuSetting.VRControllerModel), _ => updatePen());
            bind(penTilt = config.GetBindable<float>(OsuSetting.VRPenTilt), _ => updatePen());
            bind(penOffsetForward = config.GetBindable<float>(OsuSetting.VRPenOffsetForward), _ => updatePen());
            bind(penOffsetUp = config.GetBindable<float>(OsuSetting.VRPenOffsetUp), _ => updatePen());
            scene?.DefaultControllers.BindValueChanged(_ => updatePen());

            // What a controller looks like where the runtime has no model of it, and in the hands of a replay's player.
            xr.GenericControllerModels = (VRModels.ControllerLeft, VRModels.ControllerRight);

            // Panel layout: settings apply to the headset, and resizing / pushing the panel by hand writes back to the settings.
            bind(panelSize = config.GetBindable<float>(OsuSetting.VRPanelSize), v => xr.PanelHorizontalAngle = v * MathF.PI / 180);
            bind(panelDistance = config.GetBindable<float>(OsuSetting.VRPanelDistance), v => xr.PanelDistance = v);
            bind(panelPlacement = config.GetBindable<string>(OsuSetting.VRPanelPlacement), applyPlacement);

            xr.PanelLayoutChanged += onPanelLayoutChanged;

            // A panel placed by the game for a while (see IVRScene.UsePanel) goes back to where the settings have it.
            scene?.PanelOverridden.BindValueChanged(overridden =>
            {
                if (overridden.NewValue)
                    return;

                xr.PanelHorizontalAngle = panelSize.Value * MathF.PI / 180;
                xr.PanelDistance = panelDistance.Value;
                applyPlacement(panelPlacement.Value);
            });

            // Music data for the custom field's floor.
            AddInternal(new VREnvironmentAudio { Output = (bands, pulse, kiai) => xr.SetEnvironmentAudio(bands, pulse, kiai) });
        }

        private void updatePen()
        {
            // Bindables are bound one at a time, so some may not be yet.
            if (controllerModel == null || penTilt == null || penOffsetForward == null || penOffsetUp == null)
                return;

            // (A replay being watched has the user hold plain controllers.)
            bool pen = controllerModel.Value == VRControllerModel.Pen && scene?.DefaultControllers.Value != true;

            xr.HeldModel = pen ? VRModels.Pen : null;
            xr.ControllerInput.PointerTilt = pen ? penTilt.Value * MathF.PI / 180 : 0;
            xr.ControllerInput.PointerOffset = pen ? new System.Numerics.Vector3(0, penOffsetUp.Value / 100, -penOffsetForward.Value / 100) : default;
        }

        protected override void Update()
        {
            base.Update();

            // This updates ahead of the screens making requests: what they asked for last frame is known by now.
            scene?.EndRequests();
        }

        private void onPanelLayoutChanged() => Schedule(() =>
        {
            // Moved while placed by the game: not where the user keeps it.
            if (scene?.PanelOverridden.Value == true)
                return;

            panelSize.Value = xr.PanelHorizontalAngle * 180 / MathF.PI;
            panelDistance.Value = xr.PanelDistance;
            panelPlacement.Value = VRPanelPlacement.Format(xr.PanelOffset, xr.PanelRotation);
        });

        private void applyPlacement(string value)
        {
            if (VRPanelPlacement.TryParse(value, out var offset, out var rotation))
            {
                xr.PanelOffset = offset;
                xr.PanelRotation = rotation;
            }
            else
                xr.ResetPanelPlacement();
        }

        private void bind<T>(Bindable<T> bindable, Action<T> apply)
        {
            bindables.Add(bindable);
            bindable.BindValueChanged(v => apply(v.NewValue), true);
        }

        protected override void Dispose(bool isDisposing)
        {
            xr.PanelLayoutChanged -= onPanelLayoutChanged;
            base.Dispose(isDisposing);
        }

        /// <summary>
        /// Exposes an OpenXR host's controllers to the game.
        /// </summary>
        public class Controllers : IVRControllers
        {
            private readonly OpenXRInputHandler input;

            public Controllers(OpenXRInputHandler input)
            {
                this.input = input;
            }

            public int ActiveHand => input.ActiveHand;

            public bool IsTriggerDown(int hand) => input.IsTriggerDown(hand);

            public bool SuppressPointerButtons
            {
                get => input.SuppressPointerButtons;
                set => input.SuppressPointerButtons = value;
            }

            public void Vibrate(int hand, float amplitude, double duration, float frequency = 0) => input.Vibrate(hand, amplitude, duration, frequency);

            public double GetDistanceMoved(int hand) => input.GetDistanceMoved(hand);
        }

        /// <summary>
        /// Lets the game draw into an OpenXR host's scene.
        /// </summary>
        public class Scene : IVRScene
        {
            /// <summary>
            /// Whether the user holds plain controllers whatever their settings, as requested for the current frame.
            /// </summary>
            public readonly BindableBool DefaultControllers = new BindableBool();

            /// <summary>
            /// Whether the panel is placed by the game rather than by the user's settings, as requested for the current frame.
            /// </summary>
            public readonly BindableBool PanelOverridden = new BindableBool();

            private readonly OpenXRHostController xr;

            private bool defaultControllersRequested;
            private bool ghostHandsRequested;
            private bool panelRequested;
            private VRPanelGeometry? requestedPanel;

            public Scene(OpenXRHostController xr)
            {
                this.xr = xr;
            }

            public void ShowGhostHands(VRGhostHand? left, VRGhostHand? right, int activeHand, VRControllerModel model)
            {
                ghostHandsRequested = true;

                xr.GhostHands.HeldModel = model == VRControllerModel.Pen ? VRModels.Pen : null;
                xr.GhostHands.Show(ToHandState(left), ToHandState(right), activeHand);
            }

            public void UsePanel(VRPanelGeometry panel)
            {
                panelRequested = true;
                PanelOverridden.Value = true;

                if (panel == requestedPanel)
                    return;

                requestedPanel = panel;

                // The host places the panel relative to its anchor.
                var tracking = xr.GetTrackingState();
                var anchor = tracking.Frame > 0 ? new VRPose(tracking.AnchorPosition, tracking.AnchorOrientation) : VRPose.IDENTITY;
                var placement = panel.Pose.RelativeTo(anchor);

                xr.PanelOffset = placement.Position;
                xr.PanelRotation = placement.Rotation;
                xr.PanelDistance = panel.Radius;
                xr.PanelHorizontalAngle = panel.HorizontalAngle;
            }

            public void UseDefaultControllers()
            {
                defaultControllersRequested = true;
                DefaultControllers.Value = true;
            }

            /// <summary>
            /// Ends what wasn't requested again since the last call. To be called once per update frame, ahead of anything making requests.
            /// </summary>
            public void EndRequests()
            {
                if (!ghostHandsRequested)
                    xr.GhostHands.Hide();

                DefaultControllers.Value = defaultControllersRequested;

                if (!panelRequested)
                {
                    requestedPanel = null;
                    PanelOverridden.Value = false;
                }

                ghostHandsRequested = defaultControllersRequested = panelRequested = false;
            }

        }

        /// <summary>
        /// A hand as the framework draws one: untracked if there is none.
        /// </summary>
        public static XrHandState ToHandState(VRGhostHand? hand) => hand is not VRGhostHand h
            ? default
            : new XrHandState
            {
                GripValid = true,
                GripPosition = h.Grip.Position,
                GripOrientation = h.Grip.Rotation,
                AimValid = true,
                AimPosition = h.Pointer.Position,
                AimOrientation = h.Pointer.Rotation,
                AimDirection = System.Numerics.Vector3.Transform(-System.Numerics.Vector3.UnitZ, h.Pointer.Rotation),
                Trigger = h.Trigger,
                Squeeze = h.Squeeze,
            };

        /// <summary>
        /// Exposes an OpenXR host's tracking to the game.
        /// </summary>
        public class Tracking : IVRTracking
        {
            private readonly OpenXRHostController xr;

            public Tracking(OpenXRHostController xr)
            {
                this.xr = xr;
            }

            public string RuntimeName => xr.RuntimeName;

            public VRTrackingState GetState()
            {
                var state = xr.GetTrackingState();

                return new VRTrackingState
                {
                    Frame = state.Frame,
                    Head = state.HeadValid ? new VRPose(state.HeadPosition, state.HeadOrientation) : null,
                    Left = convert(state.LeftHand),
                    Right = convert(state.RightHand),
                    ActiveHand = state.ActiveHand,
                    Panel = new VRPanelGeometry(new VRPose(state.PanelCentre, state.PanelOrientation), state.PanelRadius, state.PanelHorizontalAngle, state.PanelAspect,
                        state.PanelCylinder),
                };
            }

            private static VRHandTracking convert(in XrHandState hand) => new VRHandTracking
            {
                Grip = hand.GripValid ? new VRPose(hand.GripPosition, hand.GripOrientation) : null,
                Pointer = hand.AimValid ? new VRPose(hand.AimPosition, hand.AimOrientation) : null,
                Trigger = hand.Trigger,
                Squeeze = hand.Squeeze,
            };
        }
    }
}
