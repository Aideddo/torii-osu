// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Platform.OpenXR;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osuTK.Input;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: outside VR, shows a replay of a play made in VR (one which saved <see cref="VRReplayExtras"/>) as it happened: the
    /// replay on the player's game panel, a headset where their head was and their hands around it, seen through a camera to fly about with.
    /// Switched on and off with a button; off, the replay is shown as any other.
    /// </summary>
    /// <remarks>
    /// The scene is the player's own room: everything is where it was recorded. While it's shown, what the replay is drawn by is moved
    /// from the screen into the scene, and back after.
    /// </remarks>
    public partial class VRReplayScene : CompositeDrawable
    {
        /// <summary>
        /// Whether the scene is shown.
        /// </summary>
        public IBindable<bool> Showing => showing;

        private readonly Bindable<bool> showing = new Bindable<bool>();

        /// <summary>
        /// Metres per second.
        /// </summary>
        private const float move_speed = 1.2f;

        /// <summary>
        /// Radians per pixel the mouse is dragged.
        /// </summary>
        private const float look_speed = 0.004f;

        private readonly string scoreHash;
        private readonly Func<Drawable> takeContent;
        private readonly Action<Drawable> returnContent;

        [Resolved]
        private ReplayExtrasStore store { get; set; } = null!;

        [Resolved]
        private DrawableRuleset drawableRuleset { get; set; } = null!;

        private SceneViewContainer scene = null!;
        private RoundedButton button = null!;
        private OsuSpriteText hint = null!;

        private Drawable? content;
        private VRReplayExtras? extras;
        private VRPose? playerPose;

        private float cameraYaw;
        private float cameraPitch;
        private bool cameraPlaced;
        private osuTK.Vector2? lastMousePosition;

        private InputManager inputManager = null!;

        /// <param name="scoreHash">The hash of the replay.</param>
        /// <param name="takeContent">Takes what the replay is drawn by from where it's shown, to be shown in the scene.</param>
        /// <param name="returnContent">Puts it back.</param>
        public VRReplayScene(string scoreHash, Func<Drawable> takeContent, Action<Drawable> returnContent)
        {
            this.scoreHash = scoreHash;
            this.takeContent = takeContent;
            this.returnContent = returnContent;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            InternalChildren = new Drawable[]
            {
                scene = new Scene
                {
                    Alpha = 0,
                    HeadModel = VRModels.Headset,
                    ControllerModels = (VRModels.ControllerLeft, VRModels.ControllerRight),
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new osuTK.Vector2(6),
                    Margin = new MarginPadding { Left = 10 },
                    Children = new Drawable[]
                    {
                        button = new RoundedButton
                        {
                            Size = new osuTK.Vector2(110, 30),
                            Alpha = 0.7f,
                            Action = () => showing.Value = !showing.Value,
                        },
                        hint = new OsuSpriteText
                        {
                            Font = OsuFont.Default.With(size: 12),
                            Text = @"hold right mouse: look, W A S D Q E: move, shift: faster, R: reset",
                            Alpha = 0,
                        },
                    },
                },
            };

            config.BindWith(OsuSetting.VRReplaySceneView, showing);

            VRReplayExtrasEncoder.LoadAsync(store, scoreHash, Scheduler, loaded =>
            {
                extras = loaded;
                playerPose = loaded.GetPlayerPose();
            });
        }

        /// <summary>
        /// What's shown in the scene isn't where the mouse is: it gets no input from it. (Keys still reach it.)
        /// </summary>
        private partial class Scene : SceneViewContainer
        {
            public override bool PropagatePositionalInputSubTree => false;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager()!;

            showing.BindValueChanged(_ => updateShowing(), true);
        }

        private void updateShowing()
        {
            bool show = showing.Value;

            button.Text = show ? @"Flat view" : @"3D view";
            hint.FadeTo(show ? 0.7f : 0, 200);

            if (show == (content != null))
                return;

            if (show)
            {
                scene.Add(content = takeContent());
                scene.Show();
            }
            else
            {
                scene.Remove(content!, false);
                scene.Hide();

                returnContent(content!);
                content = null;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (content == null || extras == null)
                return;

            // The time the replay's cursor is shown for.
            double time = drawableRuleset.FrameStableClock.CurrentTime;

            if (extras.GetPanelAt(time) is VRPanelGeometry recordedPanel)
            {
                // The window needn't have the shape of the panel the play was made on.
                var panel = recordedPanel.WithAspect(DrawWidth / Math.Max(DrawHeight, 1));

                scene.Panel.Centre = panel.Pose.Position;
                scene.Panel.Orientation = panel.Pose.Rotation;
                scene.Panel.Radius = panel.Radius;
                scene.Panel.HorizontalAngle = panel.HorizontalAngle;
                scene.Panel.Aspect = panel.Aspect;
                scene.Panel.Cylinder = panel.Curved;

                if (!cameraPlaced)
                    placeCamera(panel);
            }

            scene.HeldModel = extras.ControllerModel == VRControllerModel.Pen ? VRModels.Pen : null;
            scene.FloorHeight = (playerPose?.Position.Y ?? scene.Panel.Centre.Y) - 1.6f;

            if (extras.TryGetFrameAt(time, out var frame))
            {
                scene.Hands[IVRControllers.LEFT] = createHand(frame.Left, extras.LeftPointer);
                scene.Hands[IVRControllers.RIGHT] = createHand(frame.Right, extras.RightPointer);
                scene.ActiveHand = frame.ActiveHand;

                scene.ShowHead = frame.Head != null;

                if (frame.Head is VRPose head)
                {
                    scene.HeadPosition = head.Position;
                    scene.HeadOrientation = head.Rotation;
                }
            }
            else
            {
                scene.Hands[IVRControllers.LEFT] = scene.Hands[IVRControllers.RIGHT] = default;
                scene.ShowHead = false;
            }

            updateCamera();
        }

        // (The scene is the player's own room: hands are where they were recorded.)
        private static XrHandState createHand(in VRReplayHand hand, VRPose? pointer)
            => OpenXRHostIntegration.ToHandState(hand.Grip is VRPose grip ? VRGhostHand.From(hand, grip, pointer) : null);

        /// <summary>
        /// Puts the camera behind the player's right shoulder, looking past them at the panel.
        /// </summary>
        private void placeCamera(VRPanelGeometry panel)
        {
            cameraPlaced = true;

            // Without a head to go by: as far in front of the panel as its axis.
            var player = playerPose ?? panel.Pose;
            var target = Vector3.Lerp(player.Position, panel.SurfaceCentre.Position, 0.6f);

            scene.CameraPosition = player.Apply(new VRPose(new Vector3(0.55f, 0.2f, 1f), Quaternion.Identity)).Position;

            var forward = Vector3.Normalize(target - scene.CameraPosition);
            cameraYaw = MathF.Atan2(-forward.X, -forward.Z);
            cameraPitch = MathF.Asin(Math.Clamp(forward.Y, -1, 1));
        }

        private void updateCamera()
        {
            var state = inputManager.CurrentState;
            var mouse = state.Mouse.Position;

            // Read as they are, not as events: the right mouse button and these keys may mean something to the replay's own controls.
            if (state.Mouse.Buttons.IsPressed(MouseButton.Right))
            {
                if (lastMousePosition is osuTK.Vector2 last)
                {
                    cameraYaw -= (mouse.X - last.X) * look_speed;
                    cameraPitch = Math.Clamp(cameraPitch - (mouse.Y - last.Y) * look_speed, -1.5f, 1.5f);
                }

                lastMousePosition = mouse;

                var orientation = Quaternion.CreateFromYawPitchRoll(cameraYaw, cameraPitch, 0);
                var move = Vector3.Zero;

                if (state.Keyboard.Keys.IsPressed(Key.W)) move += Vector3.Transform(-Vector3.UnitZ, orientation);
                if (state.Keyboard.Keys.IsPressed(Key.S)) move += Vector3.Transform(Vector3.UnitZ, orientation);
                if (state.Keyboard.Keys.IsPressed(Key.D)) move += Vector3.Transform(Vector3.UnitX, orientation);
                if (state.Keyboard.Keys.IsPressed(Key.A)) move += Vector3.Transform(-Vector3.UnitX, orientation);
                if (state.Keyboard.Keys.IsPressed(Key.E)) move += Vector3.UnitY;
                if (state.Keyboard.Keys.IsPressed(Key.Q)) move -= Vector3.UnitY;

                if (move != Vector3.Zero)
                {
                    float speed = move_speed * (state.Keyboard.ShiftPressed ? 3 : 1);
                    scene.CameraPosition += Vector3.Normalize(move) * speed * (float)(Time.Elapsed / 1000);
                }

                if (state.Keyboard.Keys.IsPressed(Key.R))
                    cameraPlaced = false;
            }
            else
                lastMousePosition = null;

            scene.CameraOrientation = Quaternion.CreateFromYawPitchRoll(cameraYaw, cameraPitch, 0);
        }
    }
}
