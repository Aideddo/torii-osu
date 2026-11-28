// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Online;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play.Ghost
{
    /// <summary>
    /// torii: the loading screen for a ghost race. It uses the time the player is
    /// already spending on the loader to fetch the ghost's replay and work out what
    /// it scored, minute by minute, so gameplay itself never pays for the race.
    /// </summary>
    public partial class GhostRacePlayerLoader : PlayerLoader
    {
        /// <summary>How long to wait for the ghost before starting without one.</summary>
        private static readonly TimeSpan give_up_after = TimeSpan.FromSeconds(15);

        private readonly GhostRaceState state;

        private GhostReplaySimulator? simulator;
        private ScoreDownloadTracker? downloadTracker;

        private readonly Stopwatch stopwatch = new Stopwatch();

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private ScoreModelDownloader scoreDownloader { get; set; } = null!;

        public GhostRacePlayerLoader(GhostRaceState state)
            : base(() => new GhostRacePlayer(state))
        {
            this.state = state;
        }

        /// <summary>
        /// Hold the start until the ghost is ready, so a race never begins with the
        /// opponent frozen at zero and then snapping forward once it catches up.
        /// </summary>
        protected override bool ReadyForGameplay => base.ReadyForGameplay && state.Ready;

        [BackgroundDependencyLoader]
        private void load()
        {
            stopwatch.Start();
            resolveReplay();
        }

        /// <summary>
        /// Gets the ghost's replay in hand, downloading it when the score came off the
        /// global leaderboard rather than out of this machine's own history.
        /// </summary>
        private void resolveReplay()
        {
            var score = tryLoadLocally();

            if (score?.Replay != null)
            {
                log("replay was already on disk");
                startSimulation(score);
                return;
            }

            if (!state.GhostScore.HasOnlineReplay)
            {
                Logger.Log("Ghost race: that score has no replay anywhere, starting a normal play instead.", level: LogLevel.Important);
                state.Ready = true;
                return;
            }

            log($"replay is not local, downloading it (score {state.GhostScore.OnlineID})");

            AddInternal(downloadTracker = new ScoreDownloadTracker(state.GhostScore));

            downloadTracker.State.BindValueChanged(s =>
            {
                switch (s.NewValue)
                {
                    case DownloadState.LocallyAvailable:
                        log("replay downloaded");

                        var downloaded = tryLoadLocally();

                        if (downloaded?.Replay == null)
                        {
                            Logger.Log("Ghost race: the replay downloaded but could not be read, starting a normal play instead.", level: LogLevel.Important);
                            state.Ready = true;
                            return;
                        }

                        startSimulation(downloaded);
                        break;

                    case DownloadState.NotDownloaded:
                        // Only meaningful once a download has actually been attempted
                        // and dropped back; the initial state is handled below.
                        break;
                }
            }, true);

            if (downloadTracker.State.Value != DownloadState.LocallyAvailable)
            {
                // Download() quietly returns false when it will not act, and a silent
                // no-op here used to mean waiting forever on a download that was never
                // going to start.
                if (!scoreDownloader.Download(state.GhostScore))
                {
                    Logger.Log("Ghost race: the replay download would not start, starting a normal play instead.", level: LogLevel.Important);
                    state.Ready = true;
                }
            }
        }

        private Score? tryLoadLocally()
        {
            try
            {
                return scoreManager.GetScore(state.GhostScore);
            }
            catch (Exception e)
            {
                Logger.Log($"Ghost race: could not read the ghost's replay ({e.Message}).", level: LogLevel.Important);
                return null;
            }
        }

        private void startSimulation(Score ghostScore)
        {
            state.GhostReplay = ghostScore.Replay;

            var working = Beatmap.Value;
            var ghostInfo = ghostScore.ScoreInfo;

            // The ghost has to be simulated under the mods it was actually set with.
            // Its replay is a recording of hitting objects where THOSE mods put them,
            // so running it against anything else produces a stream of misses. The
            // player is free to race it under completely different mods: both sides
            // are compared by score at the same point of the map, and map time does
            // not care how fast either of them is playing it.
            var ghostRuleset = rulesets.GetRuleset(ghostInfo.Ruleset.OnlineID)?.CreateInstance();

            if (ghostRuleset == null)
            {
                Logger.Log("Ghost race: the ghost's ruleset is not available, racing without it.", level: LogLevel.Important);
                state.Ready = true;
                return;
            }

            var ghostMods = ghostInfo.Mods.ToArray();

            // Beatmap conversion is heavy, so it happens off the update thread. Only
            // the stepping itself has to be on it.
            Task.Run(() =>
            {
                try
                {
                    return working.GetPlayableBeatmap(ghostInfo.Ruleset, ghostMods);
                }
                catch (Exception e)
                {
                    Logger.Log($"Ghost race: could not prepare the beatmap for the ghost ({e.Message}).", level: LogLevel.Important);
                    return null;
                }
            }).ContinueWith(t => Schedule(() =>
            {
                // The player can back out of the loader while the conversion is still
                // running, and touching a disposed screen from a late callback is a
                // crash this codebase has already been bitten by twice.
                if (IsDisposed)
                    return;

                var playable = t.GetAwaiter().GetResult();

                if (playable == null || playable.HitObjects.Count == 0)
                {
                    state.Ready = true;
                    return;
                }

                log($"simulating {playable.HitObjects.Count} objects against {ghostScore.Replay?.Frames.Count ?? 0} replay frames");

                simulator = new GhostReplaySimulator(ghostScore, playable, ghostRuleset, ghostMods);
                AddInternal(simulator);
            }));
        }

        private static void log(string message) => Logger.Log($"Ghost race: {message}.", level: LogLevel.Debug);

        protected override void Update()
        {
            base.Update();

            if (state.Ready)
                return;

            // Last line of defence. Whatever goes wrong upstream of here, the player
            // gets to play: an unexplained wait with no way out is the worst possible
            // outcome, and a race without a ghost is still a perfectly good play.
            if (stopwatch.Elapsed > give_up_after)
            {
                Logger.Log($"Ghost race: the ghost was not ready after {give_up_after.TotalSeconds:0}s, starting a normal play instead.", level: LogLevel.Important);
                state.Ready = true;
                return;
            }

            if (simulator?.Finished != true)
                return;

            state.Timeline = simulator.Result;

            // Timed on purpose: this is the only thing the race makes the player wait
            // for, so when someone reports a slow start there should be a number in the
            // log saying whether it was us or not.
            Logger.Log($"Ghost race: ghost prepared in {stopwatch.ElapsedMilliseconds}ms.", level: LogLevel.Debug);

            if (state.Timeline == null)
                Logger.Log("Ghost race: the ghost could not be simulated, starting a normal play instead.", level: LogLevel.Important);

            // Ready either way. A ghost that failed to simulate is a race that quietly
            // becomes an ordinary play, which is much better than refusing to start.
            state.Ready = true;
        }
    }
}
