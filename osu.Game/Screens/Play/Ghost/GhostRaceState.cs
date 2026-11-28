// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Replays;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play.Ghost
{
    /// <summary>
    /// torii: "this play is a race against that score", passed down through
    /// dependency injection so the parts that care can find it and the parts that
    /// don't never know it exists.
    /// </summary>
    /// <remarks>
    /// It is cached by <see cref="GhostRacePlayer"/> and resolved as optional
    /// everywhere else, which is what keeps a normal play completely untouched:
    /// no ghost cached, nothing resolves, nothing changes.
    /// </remarks>
    public class GhostRaceState
    {
        /// <summary>The score being raced.</summary>
        public ScoreInfo GhostScore { get; }

        /// <summary>
        /// The ghost's input, used to draw its cursor. Kept separate from the
        /// timeline because the cursor needs frames while the leaderboard needs
        /// scores, and one can be present without the other being useful.
        /// </summary>
        /// <remarks>
        /// Filled in by the loader rather than passed in, because racing a score off
        /// the global leaderboard means downloading its replay first. Anything reading
        /// this runs after the loader is done, so it is never null by then.
        /// </remarks>
        public Replay? GhostReplay { get; set; }

        /// <summary>
        /// Where the ghost stood over time. Null until the simulation finishes, and
        /// null forever if it failed, in which case the race silently degrades to a
        /// normal play rather than breaking gameplay.
        /// </summary>
        public GhostTimeline? Timeline { get; set; }

        /// <summary>
        /// Whether the ghost is done being prepared. The loader holds gameplay back
        /// on this so the race never starts with the opponent still stuck at zero.
        /// </summary>
        public bool Ready { get; set; }

        public GhostRaceState(ScoreInfo ghostScore)
        {
            GhostScore = ghostScore;
        }
    }
}
