// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.VR;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Torii: VR controller vibration for osu! gameplay.
    /// </summary>
    /// <remarks>
    /// A short tick on the hand that hit a circle; smaller ticks on the holding hand(s) for slider ticks, repeats and ends; a faint buzz
    /// while holding a slider; a buzz rising with progress while spinning; and a low thud on both hands when a combo breaks.
    /// Nothing while watching a replay. Scaled by <see cref="OsuSetting.VRHapticStrength"/>.
    /// </remarks>
    public partial class OsuVRHaptics : Component
    {
        /// <summary>
        /// Combos shorter than this break without a thud (missing the first notes of a map shouldn't punish).
        /// </summary>
        private const int min_combo_for_break = 10;

        /// <summary>
        /// Continuous effects are re-issued at this interval (ms), each pulse lasting a little longer so they overlap.
        /// </summary>
        private const double continuous_interval = 50;

        [Resolved]
        private IVRControllers controllers { get; set; } = null!;

        [Resolved]
        private DrawableRuleset? drawableRuleset { get; set; }

        private Bindable<float> strength = null!;

        private double lastContinuous;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            strength = config.GetBindable<float>(OsuSetting.VRHapticStrength);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (drawableRuleset != null)
                drawableRuleset.Playfield.NewResult += onNewResult;
        }

        private bool active => strength.Value > 0 && drawableRuleset is { } r && !r.HasReplayLoaded.Value && !r.IsPaused.Value;

        private void onNewResult(DrawableHitObject drawable, JudgementResult result)
        {
            if (!active)
                return;

            if (result.Type.BreaksCombo())
            {
                if (result.ComboAtJudgement >= min_combo_for_break)
                {
                    pulse(IVRControllers.LEFT, 1f, 90, 60);
                    pulse(IVRControllers.RIGHT, 1f, 90, 60);
                }

                return;
            }

            if (!result.IsHit)
                return;

            switch (drawable)
            {
                // Includes slider heads.
                case DrawableHitCircle circle:
                    pulse(handFor(circle.HitAction), 0.55f, 18);
                    break;

                case DrawableSliderRepeat:
                    pulseHolding(0.4f, 14);
                    break;

                case DrawableSliderTick:
                    pulseHolding(0.25f, 10);
                    break;

                case DrawableSliderTail:
                    pulseHolding(0.35f, 12);
                    break;

                case DrawableSpinner:
                    pulseHolding(0.7f, 40);
                    break;

                case DrawableSpinnerTick:
                    pulseHolding(0.2f, 8);
                    break;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!active || Time.Current - lastContinuous < continuous_interval)
                return;

            lastContinuous = Time.Current;

            foreach (var drawable in drawableRuleset!.Playfield.HitObjectContainer.AliveObjects)
            {
                switch (drawable)
                {
                    case DrawableSlider slider when slider.SliderInputManager.Tracking:
                        pulseHolding(0.08f, continuous_interval * 1.5);
                        break;

                    case DrawableSpinner spinner when spinner.RotationTracker.Tracking && spinner.RotationTracker.IsSpinning.Value:
                        pulseHolding(0.1f + 0.45f * spinner.Progress, continuous_interval * 1.5);
                        break;
                }
            }
        }

        private int handFor(OsuAction? action) => action switch
        {
            OsuAction.LeftButton => IVRControllers.LEFT,
            OsuAction.RightButton => IVRControllers.RIGHT,
            _ => controllers.ActiveHand,
        };

        /// <summary>
        /// Pulses every hand whose trigger is held, or the active hand if none is (e.g. playing with keys).
        /// </summary>
        private void pulseHolding(float amplitude, double duration)
        {
            bool left = controllers.IsTriggerDown(IVRControllers.LEFT);
            bool right = controllers.IsTriggerDown(IVRControllers.RIGHT);

            if (!left && !right)
            {
                pulse(controllers.ActiveHand, amplitude, duration);
                return;
            }

            if (left) pulse(IVRControllers.LEFT, amplitude, duration);
            if (right) pulse(IVRControllers.RIGHT, amplitude, duration);
        }

        private void pulse(int hand, float amplitude, double duration, float frequency = 0)
            => controllers.Vibrate(hand, amplitude * strength.Value, duration, frequency);

        protected override void Dispose(bool isDisposing)
        {
            if (drawableRuleset != null)
                drawableRuleset.Playfield.NewResult -= onNewResult;

            base.Dispose(isDisposing);
        }
    }
}
