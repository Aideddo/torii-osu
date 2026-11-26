// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Statistics;
using osu.Framework.Testing;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Overlays.ToriiBriefing;
using osu.Game.Configuration;
using osu.Game.Input.Bindings;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Select;
using osu.Game.Rulesets.Osu;
using osu.Game.Tests.Resources;
using osu.Game.Users;
using osu.Game.Graphics.Containers;
using osuTK;
using SixLabors.ImageSharp;

namespace osu.Game.Tests.Visual.Performance
{
    /// <summary>
    /// Walks the full game through every menu and overlay and logs, per page, the CPU cost of a frame (update and draw submission),
    /// the renderer counters (draw calls, flush sources, vertices, frame buffer redraws) and what is present and masking in the tree.
    /// </summary>
    /// <remarks>
    /// Headless (NUnit) runs only update the game, so draw times and draw counters are zero there; the update cost and the census still apply.
    /// For draw numbers run it in the visual test browser, with an optimized build (<c>-c Debug -p:Optimize=true</c>).
    /// Results go to the log ("Menu perf:" lines) and, with <c>TORII_MENU_PERF_OUT</c> set, to that file.
    /// <c>TORII_MENU_PERF_EXIT=1</c> exits the test browser when done; <c>TORII_MENU_PERF_ONLY</c> runs only the pages whose name contains it.
    /// </remarks>
    [Explicit("Performance measurement, not a correctness test.")]
    public partial class TestSceneMenuPerformance : OsuGameTestScene
    {
        private const double settle_time = 2500;
        private const double measure_time = 4000;

        private readonly Queue<(string name, double duration, Action? action)> steps = new Queue<(string, double, Action?)>();
        private double stepEndTime = double.MaxValue;
        private bool finished;
        private Func<bool>? waitUntil;
        private (int gen0, int gen1, int gen2) gcBefore;
        private long allocatedBefore;
        private int cardsBefore;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private readonly StringBuilder report = new StringBuilder();

        // Measurement state. Update samples are taken on the update thread, draw samples on the draw thread.
        private volatile bool measuring;
        private Action? perFrame;
        private readonly List<double> updateTimes = new List<double>();
        private readonly List<double> drawTimes = new List<double>();
        private readonly object drawLock = new object();
        private double[] drawCounterSums = Array.Empty<double>();
        private double[] flushSums = Array.Empty<double>();
        private double[] updateCounterSums = Array.Empty<double>();

        private static readonly string[] draw_counters = { "DrawCalls", "VerticesDraw", "TextureBinds", "ShaderBinds", "UniformUpl", "FBORedraw", "Pixels" };
        private static readonly string[] update_counters = { "Invalidations", "Refreshes", "DrawNodeCtor", "DrawNodeAppl", "CCL", "PositionalIQ", "ScheduleInvk" };
        private static readonly string[] flush_sources = Enum.GetNames<FlushBatchSource>();

        private readonly List<APIBeatmapSet> listingSets = new List<APIBeatmapSet>();

        [Test]
        public void TestMenus()
        {
            // The whole walk runs from one step: the interactive test browser only runs the first step by itself.
            AddStep("run menu performance walk", start);
            AddUntilStep("walk finished", () => finished);
        }

        private void start()
        {
            // TORII_MENU_PERF_LIGHTWEIGHT=1: with the lightweight interface (read where things are built, which for what's measured is from here on).
            if (Environment.GetEnvironmentVariable("TORII_MENU_PERF_LIGHTWEIGHT") == "1")
                osu.Game.Performance.LightweightInterface.SetFromConfig(true);

            // Enough for a dozen pages of results; made up front, as creating one parses a beatmap.
            for (int i = 0; i < 600; i++)
            {
                var set = CreateAPIBeatmapSet(Ruleset.Value);
                set.OnlineID = 100000 + i;
                // Like the average ranked set: several difficulties.
                set.Beatmaps = Enumerable.Repeat(set.Beatmaps.First(), 6).ToArray();
                listingSets.Add(set);
            }

            ((DummyAPIAccess)API).HandleRequest = handleRequest;

            for (int i = 0; i < 20; i++)
                Game.BeatmapManager.Import(TestResources.CreateTestBeatmapSetInfo(4, new[] { new OsuRuleset().RulesetInfo }));

            Game.ScreenStack.ChildrenOfType<MainMenu>().FirstOrDefault()?.MakeCurrent();

            InputManager.MoveMouseTo(Game.ScreenSpaceDrawQuad.Centre + new Vector2(0, Game.ScreenSpaceDrawQuad.Height * 0.45f));

            // Overlays are loaded one by one after the game starts.
            step("wait for overlays", 0, () => waitUntil = () => Game.ChildrenOfType<WikiOverlay>().Any() && Game.ChildrenOfType<ToriiBriefingOverlay>().Any());

            // Torii's briefing shows up shortly after login and would close whatever overlay is open then.
            step("wait for login popups", 4000, null);
            step("dismiss login popups", 1000, dismissPopups);

            page("main menu");

            page("beatmap listing", () => Game.ChildrenOfType<BeatmapListingOverlay>().Single().Show());
            page("beatmap listing, hovering a card", null, () => hoverCard(0));
            // About two screens a second, with the pointer over the cards passing under it, then with the pointer away from them.
            page("beatmap listing, scrolling", null, null, () => scrollOverlay<BeatmapListingOverlay>(2));
            page("beatmap listing, scrolling, pointer away", null, () => InputManager.MoveMouseTo(Game.ScreenSpaceDrawQuad.TopLeft + new Vector2(5)),
                () => scrollOverlay<BeatmapListingOverlay>(2));
            page("beatmap listing, scrolled down idle");
            step("close beatmap listing", settle_time, () => Game.ChildrenOfType<BeatmapListingOverlay>().Single().Hide());

            page("beatmap set", () => Game.ShowBeatmapSet(listingSets[0].OnlineID));
            step("close beatmap set", settle_time, () => Game.ChildrenOfType<BeatmapSetOverlay>().Single().Hide());

            page("user profile", () => Game.ShowUser(new APIUser { Id = 2, Username = "peppy" }));
            step("close user profile", settle_time, () => Game.ChildrenOfType<UserProfileOverlay>().Single().Hide());

            overlayPage<RankingsOverlay>("rankings");
            overlayPage<NewsOverlay>("news");
            overlayPage<ChangelogOverlay>("changelog");
            overlayPage<WikiOverlay>("wiki");
            overlayPage<DashboardOverlay>("dashboard");
            overlayPage<ChatOverlay>("chat");
            overlayPage<NotificationOverlay>("notifications");

            page("settings", () => Game.Settings.Show());
            page("settings, scrolling", null, null, () => scrollSettings(8));
            step("close settings", settle_time, () => Game.Settings.Hide());

            page("song select", () => Game.PerformFromScreen(s => s.Push(new SoloSongSelect()), new[] { typeof(MainMenu) }));
            page("song select, mod select", () => trigger(GlobalAction.ToggleModSelection));
            step("close mod select", settle_time, () => trigger(GlobalAction.Back));

            page("results", () =>
            {
                var score = TestResources.CreateTestScoreInfo(Game.Beatmap.Value.BeatmapInfo);
                score.Mods = Array.Empty<Mod>();
                Game.ScreenStack.CurrentScreen.Push(new SoloResultsScreen(score));
            });

            page("multiplayer lounge", () => Game.PerformFromScreen(s => s.Push(new Screens.OnlinePlay.Multiplayer.Multiplayer()), new[] { typeof(MainMenu) }));
            page("playlists lounge", () => Game.PerformFromScreen(s => s.Push(new Screens.OnlinePlay.Playlists.Playlists()), new[] { typeof(MainMenu) }));
            page("main menu again", () => Game.PerformFromScreen(_ => { }, new[] { typeof(MainMenu) }));

            step("done", 0, () =>
            {
                ((DummyAPIAccess)API).HandleRequest = null;

                string? output = Environment.GetEnvironmentVariable("TORII_MENU_PERF_OUT");
                if (!string.IsNullOrEmpty(output))
                    File.WriteAllText(output, report.ToString());

                finished = true;

                if (Environment.GetEnvironmentVariable("TORII_MENU_PERF_EXIT") == "1" && host.Window != null)
                    host.Exit();
            });

            stepEndTime = Time.Current;
        }

        private bool handleRequest(APIRequest request)
        {
            switch (request)
            {
                case SearchBeatmapSetsRequest search:
                    search.TriggerSuccess(new SearchBeatmapSetsResponse
                    {
                        BeatmapSets = nextPage(),
                        // Lets infinite scrolling fetch further pages.
                        Cursor = new Cursor(),
                    });
                    return true;

                case GetBeatmapSetRequest getSet:
                    getSet.TriggerSuccess(listingSets[0]);
                    return true;

                case GetUserRequest getUser:
                    getUser.TriggerSuccess(new APIUser
                    {
                        Id = 2,
                        Username = "peppy",
                        CountryCode = CountryCode.AU,
                        JoinDate = DateTimeOffset.Now.AddYears(-10),
                        Statistics = new UserStatistics { IsRanked = true, GlobalRank = 1, CountryRank = 1, PP = 10000, PlayCount = 1000 },
                    });
                    return true;
            }

            return false;
        }

        private int nextPageStart;

        private List<APIBeatmapSet> nextPage()
        {
            // Each page of results needs its own IDs, or the listing drops them as duplicates.
            var page = listingSets.Skip(nextPageStart % listingSets.Count).Take(50).ToList();
            nextPageStart += 50;
            return page;
        }

        private void overlayPage<T>(string name) where T : OverlayContainer
        {
            page(name, () => Game.ChildrenOfType<T>().Single().Show());
            step($"close {name}", settle_time, () => Game.ChildrenOfType<T>().Single().Hide());
        }

        private void hoverCard(int index)
        {
            var card = Game.ChildrenOfType<BeatmapCard>().Where(c => c.IsPresent).Skip(index).FirstOrDefault();
            if (card != null)
                InputManager.MoveMouseTo(card);
        }

        private ScrollContainer<Drawable>? scrolled;

        // The scroll container is looked up once: walking the tree every frame would cost more than what's measured.
        private void scrollOverlay<T>(float speed) where T : Drawable
        {
            scrolled ??= Game.ChildrenOfType<T>().Single().ChildrenOfType<OverlayScrollContainer>().First();
            scrolled.ScrollBy(speed * (float)Clock.ElapsedFrameTime);
        }

        private void scrollSettings(float speed)
        {
            scrolled ??= Game.Settings.ChildrenOfType<OsuScrollContainer>().OrderByDescending(s => s.ScrollableExtent).First();
            scrolled.ScrollBy(speed * (float)Clock.ElapsedFrameTime);
        }

        /// <summary>
        /// Torii's welcome wizard, briefing and notices show up on their own (on first run, after login) and block other overlays.
        /// </summary>
        private void dismissPopups()
        {
            Game.LocalConfig.SetValue(OsuSetting.ShowToriiWelcome, false);

            foreach (var overlay in Game.ChildrenOfType<VisibilityContainer>())
            {
                if (overlay is ToriiWelcomeOverlay or ToriiBriefingOverlay or ToriiFeatureHintOverlay or ToriiRestrictionOverlay or ToriiResultsNoticeOverlay
                    or DifficultyRecalcOverlay or FirstRunSetupOverlay)
                    overlay.Hide();
            }
        }

        private void trigger(GlobalAction action)
        {
            var actions = Game.ChildrenOfType<GlobalActionContainer>().First();
            actions.TriggerPressed(action);
            actions.TriggerReleased(action);
        }

        private void step(string name, double duration, Action? action) => steps.Enqueue((name, duration, action));

        /// <summary>
        /// Opens a page (<paramref name="open"/>), lets it settle, then measures it.
        /// </summary>
        private void page(string name, Action? open = null, Action? beforeMeasuring = null, Action? eachFrame = null)
        {
            string? only = Environment.GetEnvironmentVariable("TORII_MENU_PERF_ONLY");
            bool skipMeasure = !string.IsNullOrEmpty(only) && !name.Contains(only, StringComparison.OrdinalIgnoreCase);

            step($"open {name}", skipMeasure ? 500 : settle_time, () =>
            {
                dismissPopups();
                open?.Invoke();
            });

            if (skipMeasure)
                return;

            step($"begin {name}", measure_time, () =>
            {
                beforeMeasuring?.Invoke();

                lock (drawLock)
                {
                    drawTimes.Clear();
                    drawCounterSums = new double[draw_counters.Length];
                    flushSums = new double[flush_sources.Length];
                }

                updateTimes.Clear();
                gcBefore = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
                allocatedBefore = GC.GetTotalAllocatedBytes();
                cardsBefore = Game.ChildrenOfType<BeatmapCard>().Count();
                allocationSampler ??= new AllocationSampler();
                allocationSampler.Reset();
                updateCounterSums = new double[update_counters.Length];
                perFrame = eachFrame;
                scrolled = null;
                measuring = true;

                // TORII_MENU_PERF_SHOTS=<directory>: a screenshot halfway through each measured page.
                string? shots = Environment.GetEnvironmentVariable("TORII_MENU_PERF_SHOTS");
                if (!string.IsNullOrEmpty(shots) && host.Window != null)
                    Scheduler.AddDelayed(() => takeScreenshot(Path.Combine(shots, $"{name.Replace(' ', '-').Replace(",", "")}.png")), measure_time / 2);
            });
            step($"end {name}", 0, () =>
            {
                measuring = false;
                perFrame = null;
                logResults(name);
            });
        }

        protected override void Update()
        {
            base.Update();

            if (stepEndTime == double.MaxValue)
                return;

            perFrame?.Invoke();

            if (waitUntil != null)
            {
                if (!waitUntil())
                    return;

                waitUntil = null;
            }

            while (waitUntil == null && Time.Current >= stepEndTime && steps.TryDequeue(out var s))
            {
                Logger.Log($"Menu perf: step {s.name}");

                try
                {
                    s.action?.Invoke();
                }
                catch (Exception e)
                {
                    // The game swallows a few exceptions, which would otherwise hide a page that never opened.
                    report.AppendLine($"!! step {s.name} failed: {e.Message}");
                    Logger.Log($"Menu perf: step {s.name} failed: {e}");
                }

                stepEndTime = Time.Current + s.duration;
            }
        }

        public override bool UpdateSubTree()
        {
            long start = Stopwatch.GetTimestamp();
            bool result = base.UpdateSubTree();

            if (measuring)
            {
                updateTimes.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);

                for (int i = 0; i < update_counters.Length; i++)
                    updateCounterSums[i] += GlobalStatistics.Get<long>("Update", update_counters[i]).Value;
            }

            return result;
        }

        private void logResults(string name)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"== {name}");
            sb.AppendLine($"   screen: {Game.ScreenStack.CurrentScreen?.GetType().ReadableName()}, visible overlays: {string.Join(", ", Game.ChildrenOfType<VisibilityContainer>().Where(v => v.State.Value == Visibility.Visible && v.IsPresent).Select(v => v.GetType().ReadableName()))}, activation {Game.OverlayActivationMode.Value}");
            sb.AppendLine($"   update ms: {stats(updateTimes)}");
            sb.AppendLine($"   GCs: gen0 {GC.CollectionCount(0) - gcBefore.gen0}, gen1 {GC.CollectionCount(1) - gcBefore.gen1}, gen2 {GC.CollectionCount(2) - gcBefore.gen2}; allocated {(GC.GetTotalAllocatedBytes() - allocatedBefore) / 1024.0 / Math.Max(1, updateTimes.Count):0.#} KB per frame, {(GC.GetTotalAllocatedBytes() - allocatedBefore) / 1048576.0 / (measure_time / 1000):0.#} MB/s");
            if (allocationSampler != null)
                sb.AppendLine($"   allocations by type (sampled, MB): {allocationSampler.Report(45)}");
            int newCards = Game.ChildrenOfType<BeatmapCard>().Count() - cardsBefore;
            if (newCards > 0)
                sb.AppendLine($"   cards added: {newCards}, {(GC.GetTotalAllocatedBytes() - allocatedBefore) / 1024.0 / newCards:0} KB allocated per card (everything included)");

            int drawFrames;

            lock (drawLock)
            {
                drawFrames = drawTimes.Count;
                sb.AppendLine($"   draw ms:   {stats(drawTimes)}");

                if (drawFrames > 0)
                {
                    sb.Append("   draw counters:");
                    for (int i = 0; i < draw_counters.Length; i++)
                        sb.Append($" {draw_counters[i]} {drawCounterSums[i] / drawFrames:0.#}");
                    sb.AppendLine();

                    sb.Append("   flushes:");
                    for (int i = 0; i < flush_sources.Length; i++)
                    {
                        double perFrame = flushSums[i] / drawFrames;
                        if (perFrame >= 0.05)
                            sb.Append($" {flush_sources[i]} {perFrame:0.#}");
                    }

                    sb.AppendLine();
                }
            }

            if (updateTimes.Count > 0)
            {
                sb.Append("   update counters:");
                for (int i = 0; i < update_counters.Length; i++)
                    sb.Append($" {update_counters[i]} {updateCounterSums[i] / updateTimes.Count:0.#}");
                sb.AppendLine();
            }

            census(sb);

            report.Append(sb);
            foreach (string line in sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
                Logger.Log($"Menu perf: {line.TrimEnd()}");
        }

        private async void takeScreenshot(string path)
        {
            using (var image = await host.TakeScreenshotAsync().ConfigureAwait(false))
                await image.SaveAsPngAsync(path).ConfigureAwait(false);
        }

        private AllocationSampler? allocationSampler;

        /// <summary>
        /// Samples what is being allocated, by type, from the runtime's allocation tick events (one per ~100 KB allocated).
        /// </summary>
        private class AllocationSampler : System.Diagnostics.Tracing.EventListener
        {
            private readonly Dictionary<string, long> bytesByType = new Dictionary<string, long>();

            protected override void OnEventSourceCreated(System.Diagnostics.Tracing.EventSource source)
            {
                if (source.Name == "Microsoft-Windows-DotNETRuntime")
                    EnableEvents(source, System.Diagnostics.Tracing.EventLevel.Verbose, (System.Diagnostics.Tracing.EventKeywords)0x1);
            }

            protected override void OnEventWritten(System.Diagnostics.Tracing.EventWrittenEventArgs e)
            {
                if (e.EventName == null || !e.EventName.StartsWith("GCAllocationTick", StringComparison.Ordinal) || e.PayloadNames == null || e.Payload == null)
                    return;

                int typeIndex = e.PayloadNames.IndexOf("TypeName");
                int amountIndex = e.PayloadNames.IndexOf("AllocationAmount64");
                if (typeIndex < 0 || amountIndex < 0)
                    return;

                string type = e.Payload[typeIndex]?.ToString() ?? "?";
                long amount = Convert.ToInt64(e.Payload[amountIndex]);

                lock (bytesByType)
                    bytesByType[type] = bytesByType.GetValueOrDefault(type) + amount;
            }

            public void Reset()
            {
                lock (bytesByType)
                    bytesByType.Clear();
            }

            public string Report(int count)
            {
                lock (bytesByType)
                    return string.Join("; ", bytesByType.OrderByDescending(kv => kv.Value).Take(count).Select(kv => $"{kv.Value / 1048576.0:0.0} {kv.Key}"));
            }
        }

        private static string stats(List<double> times)
        {
            if (times.Count == 0)
                return "-";

            var sorted = times.OrderBy(t => t).ToArray();
            return $"avg {sorted.Average():0.00} p50 {sorted[sorted.Length / 2]:0.00} p95 {sorted[(int)(sorted.Length * 0.95)]:0.00} max {sorted[^1]:0.00} ({sorted.Length} frames)";
        }

        /// <summary>
        /// What is alive and present, what masks (each masking container costs batch flushes), and what is buffered.
        /// </summary>
        private void census(StringBuilder sb)
        {
            int alive = 0, present = 0, texts = 0, maskingCount = 0, buffered = 0;
            var masking = new Dictionary<string, int>();
            var presentByType = new Dictionary<string, int>();

            // Drawn: alive, present and not culled by masking, all the way up.
            static bool presentInTree(Drawable d)
            {
                for (Drawable? p = d; p != null; p = p.Parent)
                {
                    if (!p.IsPresent || !p.IsAlive || isMaskedAway(p))
                        return false;
                }

                return true;
            }

            foreach (var d in Game.ChildrenOfType<Drawable>())
            {
                if (!d.IsAlive)
                    continue;

                alive++;

                if (!presentInTree(d))
                    continue;

                present++;
                if (d is SpriteText)
                    texts++;
                if (d is BufferedContainer)
                    buffered++;

                string type = d.GetType().ReadableName();
                presentByType[type] = presentByType.GetValueOrDefault(type) + 1;

                if (d is CompositeDrawable { Masking: true } c)
                {
                    maskingCount++;
                    string where = type;
                    int named = 0;

                    for (var p = c.Parent; p != null && named < 2; p = p.Parent)
                    {
                        string n = p.GetType().ReadableName();
                        if (n.StartsWith("Container", StringComparison.Ordinal) || n.StartsWith("FillFlowContainer", StringComparison.Ordinal) || n == "CircularContainer")
                            continue;

                        where += " < " + n;
                        named++;
                    }

                    masking[where] = masking.GetValueOrDefault(where) + 1;
                }
            }

            sb.AppendLine($"   census: {alive} alive, {present} drawn, {texts} sprite texts, {buffered} buffered containers, {maskingCount} masking");
            sb.AppendLine($"   masking: {string.Join("; ", masking.OrderByDescending(kv => kv.Value).Take(12).Select(kv => $"{kv.Value}x {kv.Key}"))}");
            sb.AppendLine($"   present: {string.Join(", ", presentByType.OrderByDescending(kv => kv.Value).Take(12).Select(kv => $"{kv.Value}x {kv.Key}"))}");
        }

        private static readonly Func<Drawable, bool> is_masked_away = (Func<Drawable, bool>)Delegate.CreateDelegate(typeof(Func<Drawable, bool>),
            typeof(Drawable).GetProperty("IsMaskedAway", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetMethod!);

        private static bool isMaskedAway(Drawable d) => is_masked_away(d);

        protected override DrawNode CreateDrawNode() => new TimingDrawNode(this);

        private class TimingDrawNode : CompositeDrawableDrawNode
        {
            private readonly TestSceneMenuPerformance scene;

            public TimingDrawNode(TestSceneMenuPerformance scene)
                : base(scene)
            {
                this.scene = scene;
            }

            protected override void DrawOpaqueInterior(IRenderer renderer)
            {
                long start = Stopwatch.GetTimestamp();
                base.DrawOpaqueInterior(renderer);
                opaqueTime = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }

            private double opaqueTime;

            protected override void Draw(IRenderer renderer)
            {
                long start = Stopwatch.GetTimestamp();
                base.Draw(renderer);
                double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds + opaqueTime;
                opaqueTime = 0;

                if (!scene.measuring)
                    return;

                lock (scene.drawLock)
                {
                    scene.drawTimes.Add(elapsed);

                    // Counters are published at the start of each draw frame, so these are the previous frame's.
                    for (int i = 0; i < draw_counters.Length; i++)
                        scene.drawCounterSums[i] += GlobalStatistics.Get<long>("Draw", draw_counters[i]).Value;
                    for (int i = 0; i < flush_sources.Length; i++)
                        scene.flushSums[i] += GlobalStatistics.Get<int>(nameof(FlushBatchSource), flush_sources[i]).Value;
                }
            }
        }
    }
}
