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
using osu.Game.Overlays.Settings;
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
        /// Where the scene is watched from.
        /// </summary>
        public Bindable<VRReplayCamera> Camera { get; } = new Bindable<VRReplayCamera>();

        /// <summary>
        /// Whether the camera is being flown: the keys it's moved with should mean nothing else meanwhile (space would pause the
        /// replay, for one).
        /// </summary>
        public bool FlyingCamera => content != null && Camera.Value == VRReplayCamera.Free
                                                    && inputManager?.CurrentState.Mouse.Buttons.IsPressed(MouseButton.Right) == true;

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

        /// <summary>
        /// Where the camera is from the player when over their shoulder: to their right, above and behind.
        /// </summary>
        private static readonly Vector3 shoulder_offset = new Vector3(0.55f, 0.2f, 1f);

        /// <summary>
        /// How long the camera takes to catch up with the player when it goes where they go, in milliseconds (to within a third):
        /// a head is never still, and a view that isn't either is hard to watch.
        /// </summary>
        private const double shoulder_smoothing = 300, head_smoothing = 100;

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

        // Whether the camera that goes where the player goes has yet to be put there (rather than catch up from wherever it was).
        private bool cameraSnap = true;
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
            config.BindWith(OsuSetting.VRReplaySceneCamera, Camera);

            VRReplayExtrasEncoder.LoadAsync(store, scoreHash, Scheduler, loaded =>
            {
                extras = loaded;
                playerPose = loaded.GetPlayerPose();

                if (IsLoaded)
                    updateShowing();
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

            Camera.BindValueChanged(_ =>
            {
                cameraSnap = true;

                // Flown on from where it was left by the others.
                (cameraYaw, cameraPitch) = toYawPitch(Vector3.Transform(-Vector3.UnitZ, scene.CameraOrientation));
                updateShowing();
            });
            Showing.BindValueChanged(_ => updateShowing(), true);
        }

        /// <summary>
        /// The replay's settings for this: whether the scene is shown.
        /// </summary>
        public PlayerSettingsGroup CreateSettings() => new Settings(Showing, Camera);

        private partial class Settings : PlayerSettingsGroup
        {
            public Settings(Bindable<bool> showing, Bindable<VRReplayCamera> camera)
                : base(@"VR replay")
            {
                Children = new Drawable[]
                {
                    new PlayerCheckbox
                    {
                        LabelText = @"3D view",
                        Current = showing.GetBoundCopy(),
                    },
                    new SettingsEnumDropdown<VRReplayCamera>
                    {
                        LabelText = @"Camera",
                        Current = camera.GetBoundCopy(),
                    },
                };
            }
        }

        private void updateShowing()
        {
            // Not before it's known where everything was: there'd be nowhere to put the replay yet.
            // (Nor is the replay to be taken into the scene as soon as it's there to take. On Vulkan, drawn into the scene from its
            // very first frames with the mouse over the window, images still in use got destroyed, and the driver stopped drawing
            // for good. What's saved with the replay takes a few frames to load, which has been enough.)
            bool show = Showing.Value && extras != null;

            hint.FadeTo(show && Camera.Value == VRReplayCamera.Free ? 0.7f : 0, 200);
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

            VRPose? head = null;

            if (extras.TryGetFrameAt(time, out var frame))
            {
                scene.Hands[IVRControllers.LEFT] = createHand(frame.Left, extras.LeftPointer);
                scene.Hands[IVRControllers.RIGHT] = createHand(frame.Right, extras.RightPointer);
                scene.ActiveHand = frame.ActiveHand;

                head = frame.Head;
            }
            else
                scene.Hands[IVRControllers.LEFT] = scene.Hands[IVRControllers.RIGHT] = default;

            // (Seen through the player's eyes, the headset would be all there is to see.)
            scene.ShowHead = head != null && Camera.Value != VRReplayCamera.FollowHead;

            if (head is VRPose headPose)
            {
                scene.HeadPosition = headPose.Position;
                scene.HeadOrientation = headPose.Rotation;
            }

            switch (Camera.Value)
            {
                case VRReplayCamera.OverTheShoulder:
                    followShoulder(head);
                    break;

                // Without a head to follow: from over where their shoulder would be.
                case VRReplayCamera.FollowHead when head == null:
                    followShoulder(null);
                    break;

                case VRReplayCamera.FollowHead:
                    moveCamera(head!.Value, head_smoothing);
                    break;

                default:
                    updateCamera();
                    break;
            }
        }

        /// <summary>
        /// Keeps the camera behind the player's right shoulder, looking past them at the panel.
        /// </summary>
        /// <param name="head">Where the player's head is, if known.</param>
        private void followShoulder(VRPose? head)
        {
            var panelCentre = new VRPose(scene.Panel.Centre, scene.Panel.Orientation).Apply(new VRPose(new Vector3(0, 0, -scene.Panel.Radius), Quaternion.Identity)).Position;

            // Without a head to go by: as far in front of the panel as its axis.
            var position = head?.Position ?? playerPose?.Position ?? scene.Panel.Centre;

            // Facing the panel, not wherever the head is turned: the camera shouldn't swing about with every glance.
            var facing = panelCentre - position;
            float yaw = facing.X == 0 && facing.Z == 0 ? 0 : MathF.Atan2(-facing.X, -facing.Z);
            var player = new VRPose(position, Quaternion.CreateFromYawPitchRoll(yaw, 0, 0));

            var camera = player.Apply(new VRPose(shoulder_offset, Quaternion.Identity)).Position;
            var (cameraFacing, cameraTilt) = toYawPitch(Vector3.Lerp(position, panelCentre, 0.6f) - camera);

            moveCamera(new VRPose(camera, Quaternion.CreateFromYawPitchRoll(cameraFacing, cameraTilt, 0)), shoulder_smoothing);
        }

        /// <summary>
        /// Moves the camera towards <paramref name="target"/>, catching up with it over <paramref name="smoothing"/> milliseconds.
        /// </summary>
        private void moveCamera(VRPose target, double smoothing)
        {
            var current = new VRPose(scene.CameraPosition, scene.CameraOrientation);

            // Straight there to begin with, and after the replay was sought elsewhere.
            float amount = cameraSnap || Vector3.Distance(current.Position, target.Position) > 1 ? 1 : (float)(1 - Math.Exp(-Time.Elapsed / smoothing));
            cameraSnap = false;

            var moved = VRPose.Lerp(current, target, amount);

            scene.CameraPosition = moved.Position;
            scene.CameraOrientation = moved.Rotation;
        }

        /// <summary>
        /// The yaw and pitch of a camera looking along <paramref name="direction"/>.
        /// </summary>
        private static (float yaw, float pitch) toYawPitch(Vector3 direction)
        {
            if (direction.LengthSquared() < 1e-8f)
                return (0, 0);

            direction = Vector3.Normalize(direction);
            return (MathF.Atan2(-direction.X, -direction.Z), MathF.Asin(Math.Clamp(direction.Y, -1, 1)));
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

            scene.CameraPosition = player.Apply(new VRPose(shoulder_offset, Quaternion.Identity)).Position;

            (cameraYaw, cameraPitch) = toYawPitch(target - scene.CameraPosition);
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
