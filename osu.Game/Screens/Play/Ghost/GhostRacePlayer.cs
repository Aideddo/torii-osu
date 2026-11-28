// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;

namespace osu.Game.Screens.Play.Ghost
{
    /// <summary>
    /// torii: a completely normal play that happens to have an opponent.
    /// </summary>
    /// <remarks>
    /// Deriving from <see cref="SoloPlayer"/> is deliberate: a race submits, earns pp
    /// and sets records exactly like any other play. Nothing about the ghost helps
    /// you hit notes. Its cursor is only drawn during breaks, where there is nothing
    /// to hit, and its score on the leaderboard is no more of an advantage than the
    /// global scores lazer already shows you there.
    /// </remarks>
    public partial class GhostRacePlayer : SoloPlayer
    {
        [Cached]
        private readonly GhostRaceState ghostRace;

        public GhostRacePlayer(GhostRaceState ghostRace)
        {
            this.ghostRace = ghostRace;
        }
    }
}
