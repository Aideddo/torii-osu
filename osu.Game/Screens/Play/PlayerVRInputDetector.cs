// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Utils;
using osu.Game.VR;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Torii: marks a play with the VR mod (<see cref="ModVR"/>) once the player uses a VR controller during it: moves one about, or
    /// pulls a trigger. The way <see cref="PlayerTouchInputDetector"/> marks plays made by touch. Does nothing outside VR, or in VR
    /// for a play made with something else (a mouse, a tablet, a keyboard) while the controllers lie still.
    /// </summary>
    public partial class PlayerVRInputDetector : Component
    {
        [Resolved]
        private Player player { get; set; } = null!;

        [Resolved]
        private GameplayState gameplayState { get; set; } = null!;

        [Resolved]
        private DrawableRuleset drawableRuleset { get; set; } = null!;

        /// <summary>
        /// Null outside VR.
        /// </summary>
        [Resolved]
        private IVRControllers? controllers { get; set; }

        /// <summary>
        /// How far the controllers have to be moved during the play, both added together, for it to count as made with them: in
        /// metres. More than putting one down or nudging it, far less than aiming at anything takes.
        /// </summary>
        private const double movement_threshold = 0.3;

        private readonly double[] lastDistance = new double[2];
        private double moved;

        private bool applied;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            for (int hand = 0; hand < 2; hand++)
                lastDistance[hand] = controllers?.GetDistanceMoved(hand) ?? 0;
        }

        protected override void Update()
        {
            base.Update();

            if (applied || controllers == null)
                return;

            // Read every frame, so that what's moved while it doesn't count isn't counted once it does.
            double movedNow = 0;

            for (int hand = 0; hand < 2; hand++)
            {
                double distance = controllers.GetDistanceMoved(hand);
                movedNow += Math.Max(distance - lastDistance[hand], 0);
                lastDistance[hand] = distance;
            }

            if (gameplayState.HasPassed || gameplayState.HasFailed || gameplayState.HasQuit)
                return;

            // Not part of playing: the controllers point at menus and skip buttons then, or are put down.
            if (player.IsBreakTime.Value || drawableRuleset.IsPaused.Value)
                return;

            moved += movedNow;

            if (moved < movement_threshold && !controllers.IsTriggerDown(IVRControllers.LEFT) && !controllers.IsTriggerDown(IVRControllers.RIGHT))
                return;

            if (gameplayState.Score.ScoreInfo.Mods.OfType<ModVR>().Any())
            {
                applied = true;
                return;
            }

            var vrMod = gameplayState.Ruleset.GetVRMod();
            if (vrMod == null)
                return;

            var candidateMods = player.Score.ScoreInfo.Mods.Append(vrMod).ToArray();

            if (!ModUtils.CheckCompatibleSet(candidateMods, out _))
                return;

            // (As for touch: Player takes its mods not to change, so this isn't shown on the mod display during the play.)
            player.Score.ScoreInfo.Mods = candidateMods;
            applied = true;
        }
    }
}
