// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.UI;
using osu.Game.VR;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Torii: maps VR controller triggers to osu! gameplay keys.
    /// </summary>
    /// <remarks>
    /// While gameplay input is live, the left trigger is <see cref="OsuAction.LeftButton"/> and the right trigger is
    /// <see cref="OsuAction.RightButton"/>, each pressed and released independently (so alternating triggers on streams never
    /// swallow a press). The cursor follows the controller that pressed last; its position is applied by the platform before
    /// this runs, so presses land where the user points.
    /// While paused, watching a replay, or with no hit objects on the playfield (intro, breaks, outro), triggers act as mouse clicks so
    /// overlays and skip buttons can be used.
    /// </remarks>
    public partial class OsuVRInputMapper : Component
    {
        private readonly OsuInputManager osuInputManager;

        [Resolved]
        private IVRControllers controllers { get; set; } = null!;

        [Resolved]
        private DrawableRuleset? drawableRuleset { get; set; }

        private readonly bool[] pressed = new bool[2];

        public OsuVRInputMapper(OsuInputManager inputManager)
        {
            osuInputManager = inputManager;
        }

        protected override void Update()
        {
            base.Update();

            // Triggers are gameplay keys only while hit objects are actually on the playfield. During the intro, breaks and the outro
            // they click as usual, so skip buttons and overlays work.
            bool live = osuInputManager.AllowGameplayInputs
                        && drawableRuleset?.IsPaused.Value != true
                        && drawableRuleset?.HasReplayLoaded.Value != true
                        && drawableRuleset?.Playfield.HitObjectContainer.AliveEntries.Count > 0;

            // A per-frame request: it lapses on its own once this stops updating (e.g. when results are pushed over the player).
            if (live)
                controllers.SuppressPointerButtons = true;

            update(IVRControllers.LEFT, OsuAction.LeftButton, live);
            update(IVRControllers.RIGHT, OsuAction.RightButton, live);
        }

        private void update(int hand, OsuAction action, bool live)
        {
            bool down = live && controllers.IsTriggerDown(hand);

            if (down == pressed[hand])
                return;

            if (down)
                osuInputManager.KeyBindingContainer.TriggerPressed(action);
            else
                osuInputManager.KeyBindingContainer.TriggerReleased(action);

            pressed[hand] = down;
        }
    }
}
