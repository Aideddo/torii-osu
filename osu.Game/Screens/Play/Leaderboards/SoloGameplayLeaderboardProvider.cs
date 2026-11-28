// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Ghost;

namespace osu.Game.Screens.Play.Leaderboards
{
    public partial class SoloGameplayLeaderboardProvider : Component, IGameplayLeaderboardProvider
    {
        public IBindableList<GameplayLeaderboardScore> Scores => scores;
        private readonly BindableList<GameplayLeaderboardScore> scores = new BindableList<GameplayLeaderboardScore>();

        [Resolved]
        private LeaderboardManager? leaderboardManager { get; set; }

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        // torii: only set when this play is a ghost race. A normal play caches no
        // state, so this stays null and everything below behaves as upstream.
        [Resolved(CanBeNull = true)]
        private GhostRaceState? ghostRace { get; set; }

        [Resolved]
        private IGameplayClock? gameplayClock { get; set; }

        private GameplayLeaderboardScore? ghostScore;
        private readonly BindableLong ghostTotalScore = new BindableLong();

        private readonly Cached sorting = new Cached();
        private bool isPartial;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var globalScores = leaderboardManager?.Scores.Value;

            // torii: a race is one against one. Leaving the usual leaderboard up turns
            // it into two anonymous rows climbing through a dozen strangers, and you
            // can't tell which one is you. Racing hides everyone else.
            bool racing = ghostRace?.Timeline != null;

            // With only the two of us there are no gaps to reason about, so positions
            // are simply first and second.
            isPartial = !racing && (globalScores == null || globalScores.IsPartial);

            List<GameplayLeaderboardScore> newScores = new List<GameplayLeaderboardScore>();

            if (globalScores != null && !racing)
            {
                foreach (var topScore in globalScores.AllScores.OrderByTotalScore())
                {
                    newScores.Add(new GameplayLeaderboardScore(topScore, false, GameplayLeaderboardScore.ComboDisplayMode.Highest));
                }
            }

            // torii: the ghost sits on the leaderboard like any other score, which is
            // the whole trick: the existing sorting, positions and display all work on
            // it for free, and the race reads as "you versus them" without a second UI.
            if (racing)
            {
                var ghostUser = ghostRace!.GhostScore.User;

                ghostScore = new GameplayLeaderboardScore(new APIUser
                {
                    // Same id so the avatar still loads, but the name has to say what
                    // this row is. Racing your own score otherwise puts two identical
                    // names and two identical pictures next to each other.
                    Id = ghostUser.OnlineID,
                    Username = $"{ghostUser.Username} (ghost)",
                }, tracked: false, ghostTotalScore)
                {
                    // A ghost is being chased, so ties go to the player: matching its
                    // score should read as having caught up, not as still behind.
                    TotalScoreTiebreaker = long.MinValue,
                };

                ghostScore.Accuracy.Value = 1;
                ghostScore.Combo.Value = 0;
                ghostTotalScore.BindValueChanged(_ => sorting.Invalidate());

                newScores.Add(ghostScore);
            }

            if (gameplayState != null)
            {
                var localScore = new GameplayLeaderboardScore(gameplayState, tracked: true, GameplayLeaderboardScore.ComboDisplayMode.Highest)
                {
                    // Local score should always show lower than any existing scores in cases of ties.
                    TotalScoreTiebreaker = long.MaxValue
                };
                localScore.TotalScore.BindValueChanged(_ => sorting.Invalidate());
                newScores.Add(localScore);
            }

            scores.AddRange(newScores);

            sort();
            Scheduler.AddDelayed(sort, 1000, true);
        }

        protected override void Update()
        {
            base.Update();

            // torii: walk the ghost along its timeline. Reading it every frame rather
            // than scheduling updates keeps it exactly in step with the map, including
            // through rewinds, pauses and rate-changing mods, because the gameplay
            // clock is the single source of truth for "where are we".
            if (ghostScore == null || ghostRace?.Timeline == null || gameplayClock == null)
                return;

            var sample = ghostRace.Timeline.SampleAt(gameplayClock.CurrentTime);

            ghostTotalScore.Value = sample.TotalScore;
            ghostScore.Accuracy.Value = sample.Accuracy;
            ghostScore.Combo.Value = sample.Combo;
        }

        // logic shared with PlaylistsGameplayLeaderboardProvider
        private void sort()
        {
            if (sorting.IsValid)
                return;

            var orderedByScore = scores
                                 .OrderByDescending(i => i.TotalScore.Value)
                                 .ThenBy(i => i.TotalScoreTiebreaker)
                                 .ToList();

            int delta = 0;

            for (int i = 0; i < orderedByScore.Count; i++)
            {
                var score = orderedByScore[i];

                // see `SoloResultsScreen.FetchScores()` for another place that does the same thing with slight deviations
                // if this code is changed, that code should probably be changed as well

                score.DisplayOrder.Value = i + 1;

                // if we know we have all scores there can ever be, we can do the simple and obvious thing.
                if (!isPartial)
                    score.Position.Value = i + 1;
                else
                {
                    // we have a partial leaderboard, with potential gaps.
                    // we have initial score positions which were valid at the point of starting play.
                    // the assumption here is that non-tracked scores here cannot move around, only tracked ones can.
                    if (score.Tracked)
                    {
                        int? previousScorePosition = i > 0 ? orderedByScore[i - 1].InitialPosition : 0;
                        int? nextScorePosition = i < orderedByScore.Count - 1 ? orderedByScore[i + 1].InitialPosition : null;

                        // if the tracked score is perfectly between two scores which have known neighbouring initial positions,
                        // we can assign it the position of the previous score plus one...
                        if (previousScorePosition != null && nextScorePosition != null && previousScorePosition + 1 == nextScorePosition)
                        {
                            score.Position.Value = previousScorePosition + 1;
                            // but we also need to ensure all subsequent scores get shifted down one position, too.
                            delta++;
                        }
                        // conversely, if the tracked score is not between neighbouring two scores and the leaderboard is partial,
                        // we can't really assign a valid position at all. it could be any number between the two neighbours.
                        else
                            score.Position.Value = null;
                    }
                    // for non-tracked scores, we just need to apply any delta that might have come from the tracked scores
                    // which might have been encountered and assigned a position earlier.
                    else
                        score.Position.Value = score.InitialPosition + delta;
                }
            }

            sorting.Validate();
        }
    }
}
