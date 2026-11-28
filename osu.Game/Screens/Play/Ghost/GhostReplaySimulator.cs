// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Screens.Play.Ghost
{
    /// <summary>
    /// torii: replays a stored score as fast as the machine allows, without drawing
    /// or making a sound, to find out what it scored at every point of the map.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A replay is only input. Turning it back into a score means running the real
    /// gameplay logic over it, because judgement lives in the drawable hit objects
    /// and nowhere else. So this builds an actual ruleset, hands it the replay, and
    /// spins its clock forward.
    /// </para>
    /// <para>
    /// The speed comes from <see cref="FrameStabilityContainer"/>: with a replay
    /// attached it will chew through as many frames as it can fit in ~10ms per
    /// update, so pointing its clock at the end of the map turns each of our update
    /// frames into a big chunk of simulated map. That also means the work is spread
    /// across frames instead of blocking one, which is why this can run under the
    /// loading screen without freezing it.
    /// </para>
    /// <para>
    /// It deliberately does NOT run during gameplay. Torii has spent a lot of effort
    /// on frame stability, and a second ruleset judging hit objects alongside the
    /// real one is exactly the kind of thing that brings hitching back.
    /// </para>
    /// </remarks>
    public partial class GhostReplaySimulator : CompositeDrawable
    {
        /// <summary>How often to take a reading, in map milliseconds.</summary>
        /// <remarks>
        /// Half a second is fine because the leaderboard interpolates between
        /// samples. Denser sampling costs memory for accuracy nobody can see.
        /// </remarks>
        private const double sample_interval = 500;

        /// <summary>
        /// How many frames of no progress at all before giving up.
        /// </summary>
        /// <remarks>
        /// Watching for a stalled clock rather than counting total frames matters: a
        /// simulation that cannot advance is stuck forever, and a plain frame budget
        /// large enough for a long map is also large enough to leave the player
        /// staring at a frozen loading screen for minutes before anything gives.
        /// </remarks>
        private const int stalled_frames_before_giving_up = 120;

        /// <summary>
        /// How long each of our frames may spend pushing the simulation along.
        /// </summary>
        /// <remarks>
        /// The frame-stable container only ever advances ~10ms of work per call, which
        /// on a real map full of sliders is nowhere near enough: a four minute map
        /// needs several seconds of CPU, so at one call per frame the player waits
        /// most of a minute. Calling it repeatedly buys that time back at the cost of
        /// a choppier loading screen, which is a very good trade for something that
        /// is over in a couple of seconds and has nothing moving on it anyway.
        /// </remarks>
        private static readonly double catch_up_budget_ms = 40;

        public bool Finished { get; private set; }

        /// <summary>Null until <see cref="Finished"/>, and stays null if the simulation failed.</summary>
        public GhostTimeline? Result { get; private set; }

        private readonly Score ghostScore;
        private readonly IBeatmap playableBeatmap;
        private readonly Ruleset ruleset;
        private readonly Mod[] mods;
        private readonly double endTime;

        private readonly ManualClock manualClock = new ManualClock();
        private readonly System.Diagnostics.Stopwatch budgetStopwatch = new System.Diagnostics.Stopwatch();
        private readonly List<GhostSample> samples = new List<GhostSample>();

        private DrawableRuleset? drawableRuleset;
        private ScoreProcessor? scoreProcessor;

        private double nextSampleTime;
        private double lastObservedTime = double.NegativeInfinity;
        private int stalledFrames;

        /// <summary>Set once the replay is actually attached, so stepping never starts before that.</summary>
        private bool running;

        public GhostReplaySimulator(Score ghostScore, IBeatmap playableBeatmap, Ruleset ruleset, Mod[] mods)
        {
            this.ghostScore = ghostScore;
            this.playableBeatmap = playableBeatmap;
            this.ruleset = ruleset;
            this.mods = mods;

            endTime = playableBeatmap.HitObjects.Count == 0
                ? 0
                : playableBeatmap.HitObjects.Max(h => h.GetEndTime());

            // Invisible, but deliberately still PRESENT and full size. This looks
            // redundant and is not: a composite that is not present, or that is masked
            // away for having no size, does not update its children at all
            // (CompositeDrawable.UpdateSubTree bails on both). The ruleset inside would
            // then sit frozen and the simulation would hang forever instead of running.
            // Alpha 0 is what actually keeps it off the screen.
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load(IBindable<WorkingBeatmap> beatmap)
        {
            try
            {
                drawableRuleset = ruleset.CreateDrawableRulesetWith(playableBeatmap, mods);
            }
            catch (Exception e)
            {
                // A ruleset that cannot build the map is a dead end, not a crash:
                // the race just does not happen.
                Logger.Log($"Ghost race: could not build a ruleset for the ghost ({e.Message}).", level: LogLevel.Important);
                fail();
                return;
            }

            scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = mods;
            scoreProcessor.ApplyBeatmap(playableBeatmap);

            drawableRuleset.NewResult += r => scoreProcessor.ApplyResult(r);
            drawableRuleset.RevertResult += r => scoreProcessor.RevertResult(r);

            InternalChild = new RulesetSkinProvidingContainer(ruleset, playableBeatmap, beatmap.Value.Skin)
            {
                RelativeSizeAxes = Axes.Both,
                // Muting is not optional. The ruleset below is playing the map at
                // many times speed and would otherwise spray every hitsound in it
                // over the loading screen.
                Child = new AudioContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Volume = { Value = 0 },
                    Child = drawableRuleset,
                },
            };

            // Everything under here runs on our clock, which Update() steps by hand.
            // ProcessCustomClock stays on so the framed clock actually picks up the
            // manual time each frame; without it the ruleset would sit at time zero
            // forever and the simulation would never move.
            Clock = new FramedClock(manualClock);
            ProcessCustomClock = true;

            nextSampleTime = playableBeatmap.HitObjects.Count == 0 ? 0 : playableBeatmap.HitObjects[0].StartTime;
            manualClock.CurrentTime = nextSampleTime - 5000;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (drawableRuleset == null)
                return;

            // Attaching the replay has to wait until the ruleset itself has loaded:
            // the frame-stable container it hands the input handler to is built in the
            // ruleset's own loader, so doing this any earlier hits a null. This is the
            // same point at which ReplayPlayer does it.
            drawableRuleset.SetReplayScore(ghostScore);

            if (!drawableRuleset.HasReplayLoaded.Value)
            {
                Logger.Log("Ghost race: the replay could not be attached to the ghost's ruleset.", level: LogLevel.Important);
                fail();
                return;
            }

            running = true;
        }

        protected override void Update()
        {
            base.Update();

            if (!running || Finished || drawableRuleset == null || scoreProcessor == null)
                return;

            // Aim the clock past the end of the map. The frame-stable container will
            // not jump there, it will walk the replay towards it a frame at a time,
            // which is exactly the simulation we want.
            manualClock.CurrentTime = endTime + 10000;

            // Drive it as hard as the budget allows rather than settling for the single
            // pass the normal update would give us.
            budgetStopwatch.Restart();

            while (budgetStopwatch.Elapsed.TotalMilliseconds < catch_up_budget_ms
                   && drawableRuleset.FrameStableClock.CurrentTime < endTime)
            {
                drawableRuleset.UpdateSubTree();
            }

            double currentTime = drawableRuleset.FrameStableClock.CurrentTime;

            recordSamplesUpTo(currentTime);

            // The frame-stable clock stops advancing once the replay runs out, which
            // is the honest signal that there is nothing left to simulate.
            if (currentTime >= endTime)
            {
                finish();
                return;
            }

            if (currentTime > lastObservedTime)
            {
                lastObservedTime = currentTime;
                stalledFrames = 0;
                return;
            }

            if (++stalledFrames > stalled_frames_before_giving_up)
            {
                Logger.Log($"Ghost race: the ghost replay stopped advancing at {currentTime:0}ms, dropping it.", level: LogLevel.Important);
                fail();
            }
        }

        private void recordSamplesUpTo(double currentTime)
        {
            if (scoreProcessor == null)
                return;

            // The clock can leap several sample intervals in one of our frames, so
            // this fills in every reading that was passed rather than just the last.
            while (nextSampleTime <= currentTime)
            {
                samples.Add(new GhostSample(
                    nextSampleTime,
                    scoreProcessor.TotalScore.Value,
                    scoreProcessor.Accuracy.Value,
                    scoreProcessor.HighestCombo.Value));

                nextSampleTime += sample_interval;
            }
        }

        private void finish()
        {
            if (scoreProcessor != null)
            {
                // Close the timeline off at the real end so the ghost's last moment
                // is its final score rather than whatever the last interval landed on.
                samples.Add(new GhostSample(
                    endTime,
                    scoreProcessor.TotalScore.Value,
                    scoreProcessor.Accuracy.Value,
                    scoreProcessor.HighestCombo.Value));

                Result = new GhostTimeline(ghostScore.ScoreInfo, samples);
            }

            Finished = true;
            Expire();
        }

        private void fail()
        {
            Result = null;
            Finished = true;
            Expire();
        }
    }
}
