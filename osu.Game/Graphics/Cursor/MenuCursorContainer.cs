// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Cosmetics;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Graphics.Cursor
{
    public partial class MenuCursorContainer : CursorContainer
    {
        private readonly IBindable<bool> screenshotCursorVisibility = new Bindable<bool>(true);
        public override bool IsPresent => screenshotCursorVisibility.Value && base.IsPresent;

        private bool hideCursorOnNonMouseInput;

        public bool HideCursorOnNonMouseInput
        {
            get => hideCursorOnNonMouseInput;
            set
            {
                if (hideCursorOnNonMouseInput == value)
                    return;

                hideCursorOnNonMouseInput = value;
                updateState();
            }
        }

        protected override Drawable CreateCursor() => activeCursor = new Cursor();

        private Cursor activeCursor = null!;

        private DragRotationState dragRotationState;
        private Vector2 positionMouseDown;
        private Vector2 lastMovePosition;

        private Bindable<bool> cursorRotate = null!;
        private Sample tapSample = null!;

        private MouseInputDetector mouseInputDetector = null!;

        // Live cursor trail. Recreated whenever the user switches
        // MenuCursorStyle or the active skin changes — same lifecycle
        // contract as the gameplay-cursor visual itself, so the trail
        // texture / disjoint-mode flag never drifts away from what
        // the active skin chain currently exposes.
        private SkinnableMenuCursorTrail? cursorTrail;

        // Torii: an equipped store cursor-trail cosmetic replaces the skin trail
        // here too, so the chosen trail shows in menus (not just gameplay).
        [Resolved(canBeNull: true)]
        private ToriiCosmeticsManager? cosmetics { get; set; }

        private Drawable? cosmeticTrail;

        private Bindable<MenuCursorStyle> menuCursorStyle = null!;

        private InputManager? inputManager;
        private IHasContextMenu? lastContextTarget;
        private bool lastContextTargetHasItems;
        private double lastContextCheckTime;

        // Resolved here (not just inside the inner Cursor) so the
        // trail rebuild can observe skin changes at the container
        // level — the trail lives as a sibling of the cursor visual,
        // not inside it.
        [Resolved(canBeNull: true)]
        private ISkinSource? skinSource { get; set; }

        private bool visible;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, ScreenshotManager? screenshotManager, AudioManager audio)
        {
            cursorRotate = config.GetBindable<bool>(OsuSetting.CursorRotation);
            menuCursorStyle = config.GetBindable<MenuCursorStyle>(OsuSetting.MenuCursorStyle);

            if (screenshotManager != null)
                screenshotCursorVisibility.BindTo(screenshotManager.CursorVisibility);

            tapSample = audio.Samples.Get(@"UI/cursor-tap");

            Add(mouseInputDetector = new MouseInputDetector());
        }

        [Resolved]
        private OsuGame? game { get; set; }

        private readonly IBindable<bool> lastInputWasMouse = new BindableBool();
        private readonly IBindable<bool> gameActive = new BindableBool(true);
        private readonly IBindable<bool> gameIdle = new BindableBool();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager();

            lastInputWasMouse.BindTo(mouseInputDetector.LastInputWasMouseSource);
            lastInputWasMouse.BindValueChanged(_ => updateState(), true);

            if (game != null)
            {
                gameIdle.BindTo(game.IsIdle);
                gameIdle.BindValueChanged(_ => updateState());

                gameActive.BindTo(game.IsActive);
                gameActive.BindValueChanged(_ => updateState());
            }

            // Initial trail build + react to either the user picking a
            // different style OR the user switching skins. Same hooks
            // the inner Cursor uses for its head-rebuild — keeping the
            // two in lock-step prevents a stale-trail-after-skin-swap
            // glitch where the trail texture is from skin A but the
            // cursor head from skin B for a frame.
            //
            // Both paths funnel through scheduleRebuild — ISkinSource.SourceChanged
            // can fire off the update thread, and rebuildCursorTrail mutates
            // the children collection (Add / Remove), which must happen on
            // the update thread. Same Schedule-wrapping shape as
            // SkinnableGameplayCursor.onSkinSourceChanged.
            menuCursorStyle.BindValueChanged(_ => scheduleRebuildCursorTrail(), true);

            if (skinSource != null)
                skinSource.SourceChanged += scheduleRebuildCursorTrail;

            if (cosmetics != null)
            {
                cosmetics.EquippedTrailId.BindValueChanged(_ => scheduleRebuildCursorTrail());
                cosmetics.CustomisationChanged += onCustomisationChanged;
            }
        }

        // Live slider tweak from the store: re-apply length/density to the
        // equipped menu trail in place (no rebuild) so it updates as you drag.
        private void onCustomisationChanged(string id) => Schedule(() =>
        {
            if (cosmeticTrail != null && cosmetics != null && id == cosmetics.EquippedTrailId.Value)
                cosmetics.ApplyCustomisationTo(cosmeticTrail, id);
        });

        protected override void Dispose(bool isDisposing)
        {
            if (skinSource != null)
                skinSource.SourceChanged -= scheduleRebuildCursorTrail;

            if (cosmetics != null)
                cosmetics.CustomisationChanged -= onCustomisationChanged;

            base.Dispose(isDisposing);
        }

        private void scheduleRebuildCursorTrail() => Schedule(rebuildCursorTrail);

        /// <summary>
        /// Tear down the existing trail and, if we're in a gameplay-shaped
        /// cursor mode, build a fresh one from the current skin chain.
        /// LazerDefault mode intentionally has no trail — it would feel
        /// out of place with the textured-arrow menu cursor and isn't
        /// part of upstream's menu UX.
        /// </summary>
        private void rebuildCursorTrail()
        {
            if (cursorTrail != null)
            {
                Remove(cursorTrail, true);
                cursorTrail = null;
            }

            if (cosmeticTrail != null)
            {
                Remove(cosmeticTrail, true);
                cosmeticTrail = null;
            }

            // An equipped store cosmetic replaces the skin trail and shows for
            // ANY cursor style (so it's visible in menus, not only gameplay).
            var equipped = cosmetics?.CreateEquippedTrail();
            if (equipped != null)
            {
                equipped.RelativeSizeAxes = Axes.Both;
                equipped.Depth = 1;
                equipped.Alpha = visible ? 1 : 0;
                Add(cosmeticTrail = equipped);
                return;
            }

            bool gameplayShaped = menuCursorStyle.Value == MenuCursorStyle.SkinCursor
                                  || menuCursorStyle.Value == MenuCursorStyle.ToriiCursor;

            if (!gameplayShaped)
                return;

            // Depth = 1 (higher = drawn FIRST = behind) so the cursor
            // head — added at default depth 0 by the framework via
            // CreateCursor — stays on top of the trail. Mirrors the
            // child ordering inside OsuCursorContainer.fadeContainer
            // where the trail is placed BEFORE the cursor in the
            // children list.
            //
            // RelativeSizeAxes.Both so the trail's local space matches
            // the screen — required for ToLocalSpace(screenPos) to
            // produce sane coordinates inside MenuCursorTrail.AddTrail.
            Add(cursorTrail = new SkinnableMenuCursorTrail
            {
                RelativeSizeAxes = Axes.Both,
                Depth = 1,
                Alpha = visible ? 1 : 0,
            });
        }

        protected override void UpdateState(ValueChangedEvent<Visibility> state) => updateState();

        private void updateState()
        {
            bool combinedVisibility = getCursorVisibility();

            if (visible == combinedVisibility)
                return;

            visible = combinedVisibility;

            if (visible)
                PopIn();
            else
                PopOut();
        }

        private bool getCursorVisibility()
        {
            // do not display when explicitly set to hidden state.
            if (State.Value == Visibility.Hidden)
                return false;

            // only hide cursor when game is focused, otherwise it should always be displayed.
            if (gameActive.Value)
            {
                // do not display when last input is not mouse.
                if (hideCursorOnNonMouseInput && !lastInputWasMouse.Value)
                    return false;

                // do not display when game is idle.
                if (gameIdle.Value)
                    return false;
            }

            return true;
        }

        protected override void Update()
        {
            base.Update();

            if (dragRotationState != DragRotationState.NotDragging
                && Vector2.Distance(positionMouseDown, lastMovePosition) > 60)
            {
                // make the rotation centre point floating.
                positionMouseDown = Interpolation.ValueAt(0.04f, positionMouseDown, lastMovePosition, 0, Clock.ElapsedFrameTime);
            }

            // Push the gameplay cursor head's CURRENT visual state into the
            // trail every frame. Mirror of OsuCursorContainer.Update — the
            // trail particles need to track the cursor's expand state (so
            // they grow when the user clicks) and the cursor's rotation (so
            // disjoint trail dots stay aligned with a spinning cursor head).
            // Using CursorScale (not NewPartScale only) so the existing
            // in-flight particles also refresh against the new cursor scale,
            // matching the playfield trail's responsiveness to size changes.
            if (cursorTrail != null && activeCursor.UsesGameplayCursor && activeCursor.GameplayCursor != null)
            {
                cursorTrail.NewPartScale = activeCursor.GameplayCursor.CurrentExpandedScale;
                cursorTrail.CursorScale = activeCursor.GameplayCursor.CurrentExpandedScale;
                cursorTrail.PartRotation = activeCursor.GameplayCursor.CurrentRotation;
            }

            bool showRightClickHint = visible && !activeCursor.UsesGameplayCursor && hasRightClickAction();
            activeCursor.SetRightClickHint(showRightClickHint);
        }

        private bool hasRightClickAction()
        {
            if (inputManager == null)
                return false;

            bool blockedAboveBackground = false;

            foreach (var d in inputManager.HoveredDrawables)
            {
                if (d is IHasContextMenu contextTarget)
                {
                    bool hasItems;

                    if (ReferenceEquals(contextTarget, lastContextTarget) && Clock.CurrentTime - lastContextCheckTime < 150)
                    {
                        hasItems = lastContextTargetHasItems;
                    }
                    else
                    {
                        MenuItem[]? items = contextTarget.ContextMenuItems;

                        if (items == null)
                            continue;

                        hasItems = items.Length > 0;
                        lastContextTarget = contextTarget;
                        lastContextTargetHasItems = hasItems;
                        lastContextCheckTime = Clock.CurrentTime;
                    }

                    return hasItems;
                }

                if (d is OverlayContainer { State.Value: Visibility.Visible }
                    || d is ClickableContainer
                    || d is MainMenuButton
                    || d is OsuLogo
                    || d is Popover
                    || d is Menu)
                {
                    blockedAboveBackground = true;
                }

                if (d is BackgroundEditorHoldTrigger)
                    return !blockedAboveBackground;
            }

            lastContextTarget = null;
            return false;
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (dragRotationState != DragRotationState.NotDragging)
            {
                lastMovePosition = e.MousePosition;

                float distance = Vector2Extensions.Distance(lastMovePosition, positionMouseDown);

                // don't start rotating until we're moved a minimum distance away from the mouse down location,
                // else it can have an annoying effect.
                if (dragRotationState == DragRotationState.DragStarted && distance > 80)
                    dragRotationState = DragRotationState.Rotating;

                // don't rotate when distance is zero to avoid NaN
                if (dragRotationState == DragRotationState.Rotating && distance > 0)
                {
                    Vector2 offset = e.MousePosition - positionMouseDown;
                    float degrees = float.RadiansToDegrees(MathF.Atan2(-offset.X, offset.Y)) + 24.3f;

                    // Always rotate in the direction of least distance
                    float diff = (degrees - activeCursor.Rotation) % 360;
                    if (diff < -180) diff += 360;
                    if (diff > 180) diff -= 360;
                    degrees = activeCursor.Rotation + diff;

                    activeCursor.RotateTo(degrees, 120, Easing.OutQuint);
                }
            }

            return base.OnMouseMove(e);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (State.Value == Visibility.Visible)
            {
                if (activeCursor.UsesGameplayCursor)
                {
                    // Gameplay-cursor mode: scale UP via the cursor's
                    // own Expand animation (matches the in-game cursor
                    // press feel — pressed_scale = 1.2× with
                    // OutElasticHalf curve, see SkinnableGameplayCursor).
                    // Skip the menu-style scale-down on the outer
                    // wrapper and skip the additive flash (the
                    // gameplay cursor doesn't use it).
                    activeCursor.Expand();
                }
                else
                {
                    // Menu cursor: original lazer behaviour — outer
                    // wrapper scales down to 0.9, additive layer
                    // flashes pink.
                    activeCursor.Scale = new Vector2(1);
                    activeCursor.ScaleTo(0.90f, 800, Easing.OutQuint);

                    activeCursor.AdditiveLayer.Alpha = 0;
                    activeCursor.AdditiveLayer.FadeInFromZero(800, Easing.OutQuint);
                }

                if (cursorRotate.Value && dragRotationState != DragRotationState.Rotating)
                {
                    // if cursor is already rotating don't reset its rotate origin
                    dragRotationState = DragRotationState.DragStarted;
                    positionMouseDown = e.MousePosition;
                }

                playTapSample();
            }

            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (!e.HasAnyButtonPressed)
            {
                if (activeCursor.UsesGameplayCursor)
                {
                    // Release the gameplay cursor's expand state.
                    activeCursor.Contract();
                }
                else
                {
                    activeCursor.AdditiveLayer.FadeOutFromOne(500, Easing.OutQuint);
                    activeCursor.ScaleTo(1, 500, Easing.OutElastic);
                }

                if (dragRotationState != DragRotationState.NotDragging)
                {
                    activeCursor.RotateTo(0, 400 * (0.5f + Math.Abs(activeCursor.Rotation / 960)), Easing.OutElasticQuarter);
                    dragRotationState = DragRotationState.NotDragging;
                }

                if (State.Value == Visibility.Visible)
                    playTapSample(0.8);
            }

            base.OnMouseUp(e);
        }

        protected override void PopIn()
        {
            activeCursor.FadeTo(1, 250, Easing.OutQuint);
            activeCursor.ScaleTo(1, 400, Easing.OutQuint);

            // Trail follows the cursor visibility. Without this, gameplay
            // would Hide() the MenuCursorContainer (which fades activeCursor
            // here in PopOut) but the trail — which is added as a sibling
            // via Add(...), not inside activeCursor — stays at Alpha=1 and
            // keeps drawing over the playfield cursor. That's the
            // "gameplay cursor looks weird, something covering it" bug.
            cursorTrail?.FadeTo(1, 250, Easing.OutQuint);
            cosmeticTrail?.FadeTo(1, 250, Easing.OutQuint);

            if (dragRotationState == DragRotationState.NotDragging)
                activeCursor.RotateTo(0, 400, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            activeCursor.FadeTo(0, 250, Easing.OutQuint);
            activeCursor.ScaleTo(0.6f, 250, Easing.In);

            // Mirror PopIn — see the note there. Critical to fade this
            // when gameplay takes over so OsuCursorContainer's trail isn't
            // composited under our menu trail.
            cursorTrail?.FadeTo(0, 250, Easing.OutQuint);
            cosmeticTrail?.FadeTo(0, 250, Easing.OutQuint);

            if (dragRotationState == DragRotationState.NotDragging)
                activeCursor.RotateTo(0, 400, Easing.OutQuint);
        }

        private void playTapSample(double baseFrequency = 1f)
        {
            const float random_range = 0.02f;
            SampleChannel channel = tapSample.GetChannel();

            // Scale to [-0.75, 0.75] so that the sample isn't fully panned left or right (sounds weird)
            channel.Balance.Value = ((activeCursor.X / DrawWidth) * 2 - 1) * OsuGameBase.SFX_STEREO_STRENGTH;
            channel.Frequency.Value = baseFrequency - (random_range / 2f) + RNG.NextDouble(random_range);
            channel.Volume.Value = baseFrequency;

            channel.Play();
        }

        public partial class Cursor : Container
        {
            private Container cursorContainer = null!;
            private SkinnableGameplayCursor? gameplayCursor;
            private Container? rightClickIndicator;
            private bool rightClickHintVisible;

            private Bindable<float> menuCursorScale = null!;
            private Bindable<MenuCursorStyle> menuCursorStyle = null!;
            private const float base_scale = 0.15f;

            // Always non-null even in gameplay-cursor mode (we put a
            // texture-less stub in there — see buildCursor below) so
            // the parent class's OnMouseDown / OnMouseUp can poke
            // .Alpha without mode-aware null guards.
            public Sprite AdditiveLayer = null!;

            // Cached deps so we can rebuild the cursor visual when the
            // user toggles the "use gameplay cursor in menus" setting
            // mid-session — no restart required.
            private TextureStore textures = null!;
            private OsuColour colour = null!;
            private Texture? rightClickMouseTexture;

            // True while the parent is dispatching a gameplay-style
            // press (OnMouseDown). The parent reads this through the
            // public property below to swap its scale-down animation
            // for the gameplay-style scale-up Expand.
            public bool UsesGameplayCursor => gameplayCursor != null;

            /// <summary>
            /// The active gameplay-cursor head, or <c>null</c> when in
            /// <see cref="MenuCursorStyle.LazerDefault"/> mode. Exposed so
            /// the outer <see cref="MenuCursorContainer.Update"/> can read
            /// the cursor's CURRENT scale + rotation each frame and push
            /// them into <see cref="SkinnableMenuCursorTrail"/> — same
            /// wiring shape as <c>OsuCursorContainer.Update</c>.
            /// </summary>
            public SkinnableGameplayCursor? GameplayCursor => gameplayCursor;

            public Cursor()
            {
                // Auto-size by default; the gameplay branch overrides
                // to a fixed Size + centred Origin in buildCursor().
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config, TextureStore textures, OsuColour colour, IRenderer renderer)
            {
                this.textures = textures;
                this.colour = colour;

                rightClickMouseTexture = createRightClickMouseTexture(renderer, colour);

                menuCursorScale = config.GetBindable<float>(OsuSetting.MenuCursorSize);
                menuCursorStyle = config.GetBindable<MenuCursorStyle>(OsuSetting.MenuCursorStyle);

                // Live-rebuild on style change so the user sees the
                // swap immediately when they pick a different style.
                menuCursorStyle.BindValueChanged(_ => buildCursor(), true);

                // MenuCursorSize matters only in LazerDefault mode
                // (the other modes delegate scaling to
                // SkinnableGameplayCursor which reads GameplayCursorSize
                // internally).
                menuCursorScale.BindValueChanged(scale =>
                {
                    if (menuCursorStyle.Value == MenuCursorStyle.LazerDefault && cursorContainer != null!)
                        cursorContainer.Scale = new Vector2(scale.NewValue * base_scale);
                }, true);
            }

            /// <summary>
            /// Forwarded by <see cref="MenuCursorContainer.OnMouseDown"/>
            /// when the cursor is in gameplay mode — performs the same
            /// scale-up Expand the in-game cursor uses for click feel.
            /// </summary>
            public void Expand() => gameplayCursor?.Expand();

            /// <summary>
            /// Forwarded by <see cref="MenuCursorContainer.OnMouseUp"/>
            /// when the cursor is in gameplay mode — releases the
            /// Expand state.
            /// </summary>
            public void Contract() => gameplayCursor?.Contract();

            public void SetRightClickHint(bool available)
            {
                if (rightClickIndicator == null || rightClickHintVisible == available)
                    return;

                rightClickHintVisible = available;

                if (available)
                {
                    rightClickIndicator.FadeTo(0.9f, 140, Easing.OutQuint)
                                       .ScaleTo(1f, 180, Easing.OutQuint);
                }
                else
                {
                    rightClickIndicator.FadeOut(120, Easing.OutQuint)
                                       .ScaleTo(0.75f, 140, Easing.OutQuint);
                }
            }

            private void buildCursor()
            {
                // Swap the visual content based on the style enum.
                // Every branch exposes a cursorContainer +
                // AdditiveLayer (the latter is a stub in non-default
                // modes) so the parent class's transforms (pop-in
                // fade, hover rotation, alpha tweaks) keep working
                // unchanged.
                Clear();
                gameplayCursor = null;
                rightClickIndicator = null;
                rightClickHintVisible = false;

                bool gameplayMode = menuCursorStyle.Value == MenuCursorStyle.SkinCursor
                                    || menuCursorStyle.Value == MenuCursorStyle.ToriiCursor;

                if (gameplayMode)
                {
                    // Gameplay-cursor mode. We mirror OsuCursor's
                    // structure as closely as we can from osu.Game:
                    //
                    // - Cursor (this) Origin = Centre — the visual
                    //   middle of the cursor sits at the host-reported
                    //   mouse position. Same as OsuCursor.Origin.
                    //   Without this the cursor renders TopLeft-
                    //   anchored and the click point appears offset
                    //   from the cursor middle (the alignment bug
                    //   the user spotted).
                    //
                    // - Fixed Size (BASE_SIZE = 50, matching
                    //   LegacyCursor) instead of AutoSize — the
                    //   gameplay-cursor sprites live inside a
                    //   bounded box centred on the mouse.
                    //
                    // - cursorContainer fills with RelativeSize so
                    //   our future scale animations (Expand /
                    //   Contract via the Cursor wrapper) compose
                    //   correctly with SkinnableGameplayCursor's
                    //   own GameplayCursorSize scaling without
                    //   layout reflow.
                    AutoSizeAxes = Axes.None;
                    Size = new Vector2(SkinnableGameplayCursor.BASE_SIZE);
                    Origin = Anchor.Centre;

                    // forceTorii flag passed when the user picked
                    // ToriiCursor explicitly — instructs
                    // SkinnableGameplayCursor to skip the skin
                    // lookup and render the stylised pink ring
                    // regardless of whether their skin ships its
                    // own cursor.png.
                    bool forceTorii = menuCursorStyle.Value == MenuCursorStyle.ToriiCursor;

                    Children = new Drawable[]
                    {
                        cursorContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Children = new Drawable[]
                            {
                                gameplayCursor = new SkinnableGameplayCursor(forceTorii),
                                // Stub additive layer for the
                                // parent's mouse-handler API
                                // surface — texture-less, alpha 0,
                                // never visible. The actual press
                                // feedback comes from
                                // SkinnableGameplayCursor.Expand()
                                // (called via the parent's mouse
                                // handlers, see Expand() forwarder
                                // above).
                                AdditiveLayer = new Sprite
                                {
                                    Blending = BlendingParameters.Additive,
                                    Colour = colour.Pink,
                                    Alpha = 0,
                                },
                            },
                        },
                    };

                    // Don't apply any extra scale here — gameplay
                    // sizing is fully owned by SkinnableGameplayCursor.
                    cursorContainer.Scale = Vector2.One;
                }
                else
                {
                    // Original menu cursor (Cursor/menu-cursor +
                    // additive flash). Behaviour unchanged from
                    // upstream lazer — TopLeft-anchored sprite,
                    // base_scale (0.15) × MenuCursorSize, the menu
                    // cursor texture's click point is the top-left
                    // pixel.
                    AutoSizeAxes = Axes.Both;
                    Origin = Anchor.TopLeft;

                    Children = new Drawable[]
                    {
                        cursorContainer = new Container
                        {
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new Sprite
                                {
                                    Texture = textures.Get(@"Cursor/menu-cursor"),
                                },
                                AdditiveLayer = new Sprite
                                {
                                    Blending = BlendingParameters.Additive,
                                    Colour = colour.Pink,
                                    Alpha = 0,
                                    Texture = textures.Get(@"Cursor/menu-cursor-additive"),
                                },
                                rightClickIndicator = new Container
                                {
                                    BypassAutoSizeAxes = Axes.Both,
                                    Position = new Vector2(154, 158),
                                    Origin = Anchor.Centre,
                                    Size = new Vector2(60, 80),
                                    Alpha = 0,
                                    Scale = new Vector2(0.8f),
                                    Child = new Sprite
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Texture = rightClickMouseTexture,
                                    },
                                },
                            }
                        }
                    };

                    cursorContainer.Scale = new Vector2(menuCursorScale.Value * base_scale);
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                rightClickMouseTexture?.Dispose();
                rightClickMouseTexture = null;
                base.Dispose(isDisposing);
            }

            private static Texture createRightClickMouseTexture(IRenderer renderer, OsuColour colour)
            {
                const int width = 192;
                const int height = 256;
                const float half_width = 68f;
                const float half_height = 92f;
                const float corner_radius = 56f;
                const float border_width = 16f;
                const float divider_half = 5f;
                const float split_y = -8f;
                const float aa = 1.5f;

                var image = new Image<Rgba32>(width, height);
                Color4 accent = colour.Pink;

                for (int y = 0; y < height; y++)
                {
                    float uy = (y + 0.5f) - height * 0.5f;

                    for (int x = 0; x < width; x++)
                    {
                        float ux = (x + 0.5f) - width * 0.5f;

                        float shadowDist = sdRoundedRect(ux - 2f, uy - 6f, half_width, half_height, corner_radius);
                        float shadowAlpha = (1f - smoothstep(-2f, 14f, shadowDist)) * (120f / 255f);

                        float r = 0f, g = 0f, b = 0f, a = shadowAlpha;

                        float outerDist = sdRoundedRect(ux, uy, half_width, half_height, corner_radius);
                        float outerMask = 1f - smoothstep(-aa, aa, outerDist);

                        if (outerMask > 0.001f)
                        {
                            float innerMask = 1f - smoothstep(-aa, aa, outerDist + border_width);

                            // Charcoal backing with right-mouse-button accent & crisp inner dividers.
                            float fr = 20f / 255f;
                            float fg = 20f / 255f;
                            float fb = 26f / 255f;
                            float fa = 232f / 255f;

                            float rmbX = smoothstep(0f, aa * 2f, ux - divider_half);
                            float rmbY = 1f - smoothstep(-aa * 2f, 0f, uy - (split_y - divider_half));
                            float rmbAlpha = rmbX * rmbY * 0.9f;
                            if (rmbAlpha > 0f)
                                blendOver(ref fr, ref fg, ref fb, ref fa, accent.R, accent.G, accent.B, rmbAlpha);

                            float hDiv = 1f - smoothstep(divider_half - aa, divider_half + aa, MathF.Abs(uy - split_y));
                            float vDiv = (1f - smoothstep(divider_half - aa, divider_half + aa, MathF.Abs(ux)))
                                         * (1f - smoothstep(-aa, aa, uy - split_y));
                            float divAlpha = MathF.Max(hDiv, vDiv) * (215f / 255f);
                            if (divAlpha > 0f)
                                blendOver(ref fr, ref fg, ref fb, ref fa, 240f / 255f, 240f / 255f, 248f / 255f, divAlpha);

                            float borderWeight = 1f - innerMask;
                            float br = 248f / 255f;
                            float bg = 248f / 255f;
                            float bb = 252f / 255f;
                            float ba = 240f / 255f;

                            float sr = fr * innerMask + br * borderWeight;
                            float sg = fg * innerMask + bg * borderWeight;
                            float sb = fb * innerMask + bb * borderWeight;
                            float sa = (fa * innerMask + ba * borderWeight) * outerMask;

                            blendOver(ref r, ref g, ref b, ref a, sr, sg, sb, sa);
                        }

                        image[x, y] = new Rgba32(
                            (byte)Math.Clamp(MathF.Round(r * 255f), 0, 255),
                            (byte)Math.Clamp(MathF.Round(g * 255f), 0, 255),
                            (byte)Math.Clamp(MathF.Round(b * 255f), 0, 255),
                            (byte)Math.Clamp(MathF.Round(a * 255f), 0, 255));
                    }
                }

                var texture = renderer.CreateTexture(width, height);
                texture.SetData(new TextureUpload(image));
                return texture;
            }

            private static float sdRoundedRect(float px, float py, float halfWidth, float halfHeight, float radius)
            {
                float qx = MathF.Abs(px) - halfWidth + radius;
                float qy = MathF.Abs(py) - halfHeight + radius;
                float outside = MathF.Sqrt(MathF.Max(qx, 0f) * MathF.Max(qx, 0f) + MathF.Max(qy, 0f) * MathF.Max(qy, 0f));
                float inside = MathF.Min(MathF.Max(qx, qy), 0f);
                return outside + inside - radius;
            }

            private static float smoothstep(float edge0, float edge1, float x)
            {
                float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
                return t * t * (3f - 2f * t);
            }

            private static void blendOver(ref float dr, ref float dg, ref float db, ref float da, float sr, float sg, float sb, float sa)
            {
                float outA = sa + da * (1f - sa);
                if (outA <= 1e-6f)
                    return;

                dr = (sr * sa + dr * da * (1f - sa)) / outA;
                dg = (sg * sa + dg * da * (1f - sa)) / outA;
                db = (sb * sa + db * da * (1f - sa)) / outA;
                da = outA;
            }
        }

        private partial class MouseInputDetector : Component
        {
            /// <summary>
            /// Whether the last input applied to the game is sourced from mouse.
            /// </summary>
            public IBindable<bool> LastInputWasMouseSource => lastInputWasMouseSource;

            private readonly Bindable<bool> lastInputWasMouseSource = new Bindable<bool>();

            public MouseInputDetector()
            {
                RelativeSizeAxes = Axes.Both;
            }

            protected override bool Handle(UIEvent e)
            {
                switch (e)
                {
                    case MouseDownEvent:
                    case MouseMoveEvent:
                        lastInputWasMouseSource.Value = true;
                        return false;

                    case KeyDownEvent keyDown when !keyDown.Repeat:
                    case JoystickPressEvent:
                    case MidiDownEvent:
                        lastInputWasMouseSource.Value = false;
                        return false;
                }

                return false;
            }
        }

        private enum DragRotationState
        {
            NotDragging,
            DragStarted,
            Rotating,
        }
    }
}
