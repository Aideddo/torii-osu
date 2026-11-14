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
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.PlayerSettings;
using osuTK.Input;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: outside VR, shows a replay of a play made in VR (one which saved <see cref="VRReplayExtras"/>) as it happened: the
    /// replay on the player's game panel, a headset where their head was and their hands around it, seen through a camera to fly about with.
    /// Switched on and off in the replay's settings (see <see cref="CreateSettings"/>); off, the replay is shown as any other.
    /// </summary>
    /// <remarks>
    /// The scene is the player's own room: everything is where it was recorded. While it's shown, what the replay is drawn by is moved
    /// from the screen into the scene, and back after. What the replay is controlled with can't be used there, so it's shown flat in
    /// front of the scene instead: the replay's settings (put in <see cref="Overlay"/> by the player) and a bar to seek with.
    /// </remarks>
    public partial class VRReplayScene : CompositeDrawable
    {
        /// <summary>
        /// Whether the scene is shown.
        /// </summary>
        public Bindable<bool> Showing { get; } = new Bindable<bool>();

        /// <summary>
        /// Whether the camera is being flown: the keys it's moved with should mean nothing else meanwhile (space would pause the
        /// replay, for one).
        /// </summary>
        public bool FlyingCamera => content != null && inputManager?.CurrentState.Mouse.Buttons.IsPressed(MouseButton.Right) == true;

        /// <summary>
        /// Shown flat in front of the scene (and of the replay, when the scene isn't shown), with input from the mouse as usual.
        /// </summary>
        public Container Overlay { get; } = new Container { RelativeSizeAxes = Axes.Both };

        /// <summary>
        /// Metres per second.
        /// </summary>
        private const float move_speed = 1.5f;


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
        private SongProgress progress = null!;
        private OsuSpriteText hint = null!;

        private Drawable? content;
        private VRReplayExtras? extras;
        private VRPose? playerPose;

        private float cameraYaw;
        private float cameraPitch;
        private bool cameraPlaced;
        private osuTK.Vector2? lastMousePosition;

        private InputManager? inputManager;

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
                    Depth = 1,
                    Alpha = 0,
                    HeadModel = VRModels.Headset,
                    ControllerModels = (VRModels.ControllerLeft, VRModels.ControllerRight),
                },
                hint = new OsuSpriteText
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Margin = new MarginPadding(10),
                    Font = OsuFont.Default.With(size: 12),
                    Text = @"hold right mouse: look, W A S D: move, space: up, shift: down, R: reset",
                    Alpha = 0,
                },
                Overlay.With(o => o.Depth = -1),
            };

            config.BindWith(OsuSetting.VRReplaySceneView, Showing);

            VRReplayExtrasEncoder.LoadAsync(store, scoreHash, Scheduler, loaded =>
            {
                extras = loaded;
                playerPose = loaded.GetPlayerPose();
            });
        }

        /// <summary>
        /// Gives its content the clock the replay is shown by, as what's shown with the replay has it.
        /// </summary>
        private partial class ReplayClockDependencies : Container
        {
            private readonly DrawableRuleset drawableRuleset;

            public ReplayClockDependencies(DrawableRuleset drawableRuleset)
            {
                this.drawableRuleset = drawableRuleset;
                RelativeSizeAxes = Axes.Both;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.CacheAs(drawableRuleset.FrameStableClock);
                dependencies.CacheAs<osu.Game.Screens.Play.IGameplayClock>(drawableRuleset.FrameStableClock);
                return dependencies;
            }
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

            // The replay's own bar is on the panel in the scene, out of reach.
            // (Added only now: the replay's clock isn't there to be had until the replay has loaded.)
            AddInternal(new ReplayClockDependencies(drawableRuleset)
            {
                Child = progress = new ArgonSongProgress
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Alpha = 0,
                },
            });

            Showing.BindValueChanged(_ => updateShowing(), true);
        }

        /// <summary>
        /// The replay's settings for this: whether the scene is shown.
        /// </summary>
        public PlayerSettingsGroup CreateSettings() => new Settings(Showing);

        private partial class Settings : PlayerSettingsGroup
        {
            public Settings(Bindable<bool> showing)
                : base(@"VR replay")
            {
                Child = new PlayerCheckbox
                {
                    LabelText = @"3D view",
                    Current = showing.GetBoundCopy(),
                };
            }
        }

        private void updateShowing()
        {
            bool show = Showing.Value;

            hint.FadeTo(show ? 0.7f : 0, 200);
            progress.FadeTo(show ? 1 : 0, 200);

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
            var state = inputManager!.CurrentState;
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

                // Along the floor whichever way the camera looks up or down; up and down have keys of their own.
                var heading = Quaternion.CreateFromYawPitchRoll(cameraYaw, 0, 0);
                var move = Vector3.Zero;

                if (state.Keyboard.Keys.IsPressed(Key.W)) move += Vector3.Transform(-Vector3.UnitZ, heading);
                if (state.Keyboard.Keys.IsPressed(Key.S)) move += Vector3.Transform(Vector3.UnitZ, heading);
                if (state.Keyboard.Keys.IsPressed(Key.D)) move += Vector3.Transform(Vector3.UnitX, heading);
                if (state.Keyboard.Keys.IsPressed(Key.A)) move += Vector3.Transform(-Vector3.UnitX, heading);
                if (state.Keyboard.Keys.IsPressed(Key.Space)) move += Vector3.UnitY;
                if (state.Keyboard.ShiftPressed) move -= Vector3.UnitY;

                if (move != Vector3.Zero)
                    scene.CameraPosition += Vector3.Normalize(move) * move_speed * (float)(Time.Elapsed / 1000);

                if (state.Keyboard.Keys.IsPressed(Key.R))
                    cameraPlaced = false;
            }
            else
                lastMousePosition = null;

            scene.CameraOrientation = Quaternion.CreateFromYawPitchRoll(cameraYaw, cameraPitch, 0);
        }
    }
}
