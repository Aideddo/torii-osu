// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Utils
{
    /// <summary>
    /// Options controlling how <see cref="OsuSpacingAmplifier"/> transforms hitobject positions and slider orientations.
    /// </summary>
    public readonly record struct AmplifyOptions(
        double SpacingMultiplier,
        bool SpaceStreams = true,
        bool RotateSliders = true,
        bool AccentAngles = false,
        bool KiaiOnly = false);

    /// <summary>
    /// Target placement computed for a single <see cref="OsuHitObject"/>: new head position in osu! playfield
    /// coordinates and rigid rotation (radians, around the head) to apply to a slider's control points.
    /// </summary>
    public readonly record struct AmplifyPlacement(Vector2 Position, float Rotation);

    /// <summary>
    /// Deterministically amplifies (or compacts) inter-object spacing of an osu!standard beatmap while keeping
    /// pattern geometry, stream smoothness, slider velocities, and circle-size-aware playfield bounds intact.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The map is partitioned into independent phrases at spinners, long pauses, and non-kiai boundaries (when
    /// <see cref="AmplifyOptions.KiaiOnly"/> is active). Each phrase is formulated as a strictly convex quadratic
    /// program over head positions and solved with over-relaxed ADMM using a banded Cholesky linear solver (bandwidth 5,
    /// <c>O(n)</c> per iteration):
    /// </para>
    /// <list type="bullet">
    /// <item>Every hitcircle and every sampled vertex of every slider path remains inside its CS-aware playfield box.</item>
    /// <item>Each inter-object move is projected onto a convex trapezoid in its original direction frame so moves keep
    /// their orientation within a bounded cone, scale smoothly via <c>C * tanh(k * d / C)</c>, and never invert direction.</item>
    /// <item>Consecutive moves in a phrase share growth and turning-angle smoothness ties so patterns keep their shape
    /// and streams avoid sudden spacing cuts.</item>
    /// <item>Streams pinned against a playfield edge are rotated smoothly as one cohesive curve; wall-blocked sliders
    /// may rotate rigidly around their head (preserving exact slider velocity and duration); and an always-on
    /// anti-overlap pass prevents wall rebounds from stacking unrelated objects onto recent notes or slider bodies.</item>
    /// </list>
    /// </remarks>
    public static class OsuSpacingAmplifier
    {
        /// <summary>Saturation cap <c>C</c> of <c>g(d) = max(d, C * tanh(k * d / C))</c> in osu! pixels.</summary>
        internal const double CAP_PX = 560.0;

        /// <summary>Moves shorter than this (stacks) are kept exact so stacking rules stay intact.</summary>
        private const float stack_px = OsuBeatmapProcessor.STACK_DISTANCE;

        /// <summary>End of the smooth ramp out of the stack threshold, avoiding a cliff right at 3 px.</summary>
        private const double stack_ramp_end_px = 8.0;

        /// <summary>Maximum time gap (in beats) between end of previous object and start of next for a stream step.</summary>
        private const double stream_gap_in_beats = 1.15 / 4.0;

        /// <summary>Time gap (in beats) that ends a phrase's consecutive-move smoothness ties.</summary>
        private const double pause_beats = 1.0;

        /// <summary>Time gap (in beats) that decouples the beatmap into an independent subproblem.</summary>
        private const double restart_beats = 4.0;

        /// <summary>Tolerance for beat-relative timing thresholds, in ms.</summary>
        private const double time_slack_ms = 1.0;

        /// <summary>Maximum allowed direction deviation (degrees) for jumps, streams, compacting, and turned streams.</summary>
        private const double jump_cone_degrees = 25.0;
        private const double stream_cone_degrees = 30.0;
        private const double compact_cone_degrees = 12.0;
        private const double turned_cone_degrees = 15.0;

        /// <summary>Streams stop growing past this multiplier to avoid excessive curling.</summary>
        private const double stream_factor_cap = 2.5;

        /// <summary>Parameters for turning edge-stuck streams as one smooth piece.</summary>
        private const double burst_max_turn_degrees = 60.0;
        private const double burst_reach_target = 0.95;
        private const double burst_min_gain = 0.03;
        private const double burst_max_neighbour_loss = 0.02;
        private const int min_burst_moves = 2;
        private const int burst_rounds = 4;
        private const int burst_spacing_objects = 16;
        private const double reference_factor = 2.5;
        private const double full_turn_factor = 2.0;
        private const double curve_share = 0.5;
        private const double soft_aim_weight = 0.2;
        private const double soft_least_ratio = 0.8;

        /// <summary>Smoothness and angle-accent tie weights.</summary>
        private const double stream_growth_tie = 100.0;
        private const double jump_growth_tie = 2.0;
        private const double stream_turn_tie = 2.0;
        private const double jump_turn_tie = 1.0;
        private const double accent_turn_tie = 4.0;
        private const double accent_radians = 30.0 * Math.PI / 180.0;
        private const float accent_move_px = 30.0f;
        private const double grow_weight = 1.0;
        private const double anchor_weight = 1e-7;

        /// <summary>Slider rotation tuning parameters (rigid rotation around head; SV is never altered).</summary>
        private const float min_rotatable_slider_span_px = 14.0f;
        private const int slider_max_turn_degrees = 45;
        private const int slider_turn_step_degrees = 5;
        private const float slider_flow_max_degrees = 25.0f;

        /// <summary>Anti-overlap safety pass parameters.</summary>
        private const int overlap_max_rounds = 4;
        private const int overlap_lookback_objects = 12;
        private const double overlap_lookback_beats = 2.0;
        private const float overlap_far_min_px = 55.0f;
        private static readonly int[] accent_widen = { 0, 2, 6, 14 };

        /// <summary>ADMM solver parameters (over-relaxed Boyd et al. §3.4.3).</summary>
        private const double rho = 1e-3;
        private const double relax = 1.6;
        private const int max_iterations = 800;
        private const int min_iterations = 60;
        private const double tolerance_px = 0.008;
        private const int plan_max_iterations = 350;
        private const int plan_min_iterations = 40;
        private const double plan_tolerance_px = 0.025;
        private const float fit_slack = 0.01f;

        /// <summary>Maximum number of representative polyline vertices retained per slider for bounding-box checks.</summary>
        private const int max_slider_samples = 36;

        /// <summary>
        /// Computes the target amplified/compacted move length for an original distance <paramref name="d"/>.
        /// </summary>
        internal static double TargetLength(double d, double k)
        {
            if (d < stack_px)
                return d;

            double ramp = smoothStep(stack_px, stack_ramp_end_px, d);

            if (k >= 1.0)
            {
                double raw = Math.Max(d, CAP_PX * Math.Tanh(k * d / CAP_PX));
                return d + ramp * (raw - d);
            }
            else
            {
                double floor = Math.Max(stack_px + 0.5, d * 0.25);
                double raw = Math.Max(floor, d * k);
                return d + ramp * (raw - d);
            }
        }

        /// <summary>
        /// Computes deterministic placements for all <paramref name="hitObjects"/> in a beatmap.
        /// </summary>
        public static AmplifyPlacement[] Amplify(IReadOnlyList<OsuHitObject> hitObjects, ControlPointInfo controlPoints, AmplifyOptions options)
        {
            ArgumentNullException.ThrowIfNull(hitObjects);
            ArgumentNullException.ThrowIfNull(controlPoints);

            int n = hitObjects.Count;
            var placements = new AmplifyPlacement[n];

            for (int i = 0; i < n; i++)
                placements[i] = new AmplifyPlacement(hitObjects[i].Position, 0);

            if (n < 2 || (Math.Abs(options.SpacingMultiplier - 1.0) < 1e-6 && !options.AccentAngles))
                return placements;

            Item[] items = buildItems(hitObjects, controlPoints);
            List<(int Start, int Count)> segments = partitionPhrases(items, options.KiaiOnly);

            if (segments.Count >= 4)
            {
                Parallel.For(0, segments.Count, idx =>
                {
                    var (start, count) = segments[idx];
                    if (count >= 2)
                        solvePhrase(items, start, count, options, placements);
                });
            }
            else
            {
                foreach (var (start, count) in segments)
                {
                    if (count >= 2)
                        solvePhrase(items, start, count, options, placements);
                }
            }

            return placements;
        }

        /// <summary>
        /// Rotates a 2D vector by <paramref name="radians"/> using the exact trigonometric convention shared with
        /// <see cref="OsuHitObjectGenerationUtils.RotateSlider"/>.
        /// </summary>
        internal static Vector2 Rotate(Vector2 v, float radians)
        {
            if (radians == 0)
                return v;

            float c = MathF.Cos(radians);
            float s = MathF.Sin(radians);
            return new Vector2(c * v.X - s * v.Y, s * v.X + c * v.Y);
        }

        private static Item[] buildItems(IReadOnlyList<OsuHitObject> hitObjects, ControlPointInfo controlPoints)
        {
            int n = hitObjects.Count;
            var items = new Item[n];

            for (int i = 0; i < n; i++)
            {
                var obj = hitObjects[i];
                double beatLength = controlPoints.TimingPointAt(obj.StartTime).BeatLength;
                if (beatLength <= 1e-3 || double.IsNaN(beatLength) || double.IsInfinity(beatLength))
                    beatLength = 500.0;

                bool inKiai = controlPoints.EffectPointAt(obj.StartTime).KiaiMode;
                float radius = (float)Math.Max(0, obj.Radius);
                float margin = Math.Clamp(radius * 0.42f, 4f, 16f);

                bool isSpinner = obj is Spinner;
                bool isSlider = obj is Slider;
                Vector2 start = obj.Position;
                Vector2 end = obj.EndPosition;
                Vector2 body = end - start;
                Vector2[] pathPoints = Array.Empty<Vector2>();
                Vector2 midOffset = Vector2.Zero;
                float pathSpan = 0;

                if (obj is Slider slider)
                {
                    pathPoints = extractSliderSamples(slider);
                    midOffset = slider.Path.PositionAt(0.5);

                    foreach (var p in pathPoints)
                        pathSpan = MathF.Max(pathSpan, p.Length);
                }

                // Compute original world bounding box so objects originally near or outside the playfield edge are
                // never pushed further outside, and originally legal edge objects don't jump discontinuously.
                float origMinX = start.X;
                float origMaxX = start.X;
                float origMinY = start.Y;
                float origMaxY = start.Y;

                foreach (var p in pathPoints)
                {
                    origMinX = MathF.Min(origMinX, start.X + p.X);
                    origMaxX = MathF.Max(origMaxX, start.X + p.X);
                    origMinY = MathF.Min(origMinY, start.Y + p.Y);
                    origMaxY = MathF.Max(origMaxY, start.Y + p.Y);
                }

                var allowedMin = new Vector2(MathF.Min(margin, origMinX), MathF.Min(margin, origMinY));
                var allowedMax = new Vector2(MathF.Max(OsuPlayfield.BASE_SIZE.X - margin, origMaxX), MathF.Max(OsuPlayfield.BASE_SIZE.Y - margin, origMaxY));

                items[i] = new Item(
                    isSpinner,
                    isSlider,
                    obj.StartTime,
                    obj.GetEndTime(),
                    beatLength,
                    inKiai,
                    radius,
                    start,
                    body,
                    midOffset,
                    pathSpan,
                    pathPoints,
                    allowedMin,
                    allowedMax);
            }

            return items;
        }

        private static Vector2[] extractSliderSamples(Slider slider)
        {
            var calculated = slider.Path.CalculatedPath;

            if (calculated.Count == 0)
                return new[] { Vector2.Zero, slider.Path.PositionAt(1) };

            if (calculated.Count <= max_slider_samples)
            {
                var copy = new Vector2[calculated.Count];
                for (int i = 0; i < calculated.Count; i++)
                    copy[i] = calculated[i];
                return copy;
            }

            // Retain endpoints, exact bounding-box extrema, and uniform samples across the calculated polyline.
            int minXi = 0, maxXi = 0, minYi = 0, maxYi = 0;
            for (int i = 1; i < calculated.Count; i++)
            {
                if (calculated[i].X < calculated[minXi].X) minXi = i;
                if (calculated[i].X > calculated[maxXi].X) maxXi = i;
                if (calculated[i].Y < calculated[minYi].Y) minYi = i;
                if (calculated[i].Y > calculated[maxYi].Y) maxYi = i;
            }

            var indices = new SortedSet<int> { 0, calculated.Count - 1, minXi, maxXi, minYi, maxYi };
            int uniformCount = max_slider_samples - indices.Count;

            for (int s = 1; s <= uniformCount; s++)
                indices.Add((int)((long)s * (calculated.Count - 1) / (uniformCount + 1)));

            var result = new Vector2[indices.Count];
            int k = 0;
            foreach (int idx in indices)
                result[k++] = calculated[idx];

            return result;
        }

        private static List<(int Start, int Count)> partitionPhrases(Item[] items, bool kiaiOnly)
        {
            var segments = new List<(int Start, int Count)>();
            int n = items.Length;
            int i = 0;

            while (i < n)
            {
                if (items[i].IsSpinner || (kiaiOnly && !items[i].InKiai))
                {
                    i++;
                    continue;
                }

                int start = i;
                i++;

                while (i < n)
                {
                    if (items[i].IsSpinner || (kiaiOnly && !items[i].InKiai))
                        break;

                    double gap = items[i].StartTime - items[i - 1].EndTime;
                    if (gap >= restart_beats * items[i].BeatLength - time_slack_ms)
                        break;

                    i++;
                }

                segments.Add((start, i - start));
            }

            return segments;
        }

        private static void solvePhrase(Item[] allItems, int offset, int count, AmplifyOptions options, AmplifyPlacement[] output)
        {
            var slice = new Item[count];
            Array.Copy(allItems, offset, slice, 0, count);

            bool pinFirst = options.KiaiOnly && offset > 0 && !allItems[offset - 1].InKiai;
            float[] rotations = new float[count];
            BurstTurn[] turns = Array.Empty<BurstTurn>();

            // Step 1: Primary convex ADMM solve for this phrase.
            var problem = new PhraseProblem(slice, pinFirst, options, turns, rotations);
            Vector2[] heads = problem.Solve();

            // Step 2: Only plan smooth stream turns when this phrase actually contains a wall-compressed stream.
            if (options.SpacingMultiplier > 1.0 && options.SpaceStreams && hasBlockedStream(problem, heads))
            {
                turns = planStreamTurns(slice, pinFirst, options);
                if (turns.Length > 0)
                {
                    var turnedProblem = new PhraseProblem(slice, pinFirst, options, turns, rotations);
                    turnedProblem.StartFrom(problem);
                    problem = turnedProblem;
                    heads = problem.Solve();
                }
            }

            // Step 3: Controlled slider rotation pass (rigid around head, zero SV change).
            if (options.RotateSliders && hasRotatableSliders(slice))
            {
                if (proposeSliderRotations(slice, problem, heads, rotations))
                {
                    var rotatedProblem = new PhraseProblem(slice, pinFirst, options, turns, rotations);
                    rotatedProblem.StartFrom(problem);
                    Vector2[] rotatedHeads = rotatedProblem.Solve();

                    if (isValidSolution(slice, rotatedHeads, rotations))
                    {
                        problem = rotatedProblem;
                        heads = rotatedHeads;
                    }
                    else
                    {
                        Array.Clear(rotations);
                    }
                }
            }

            // Step 4: Always-on anti-overlap safety pass (handles both AccentAngles and normal wall-rebound overlaps).
            (_, heads) = resolveAccidentalOverlaps(slice, pinFirst, options, turns, rotations, problem, heads);

            // Step 5: Final strict validity guarantee and writeback.
            for (int i = 0; i < count; i++)
            {
                Vector2 pos = heads[i];
                float rot = rotations[i];

                if (!isFinite(pos) || !isWithinAllowedBounds(slice[i], pos, rot))
                {
                    pos = slice[i].Start;
                    rot = 0;
                }

                output[offset + i] = new AmplifyPlacement(pos, rot);
            }
        }

        private static bool hasRotatableSliders(Item[] slice)
        {
            for (int i = 0; i < slice.Length; i++)
            {
                if (slice[i].IsSlider && slice[i].PathSpan >= min_rotatable_slider_span_px)
                    return true;
            }

            return false;
        }

        private static bool hasBlockedStream(PhraseProblem problem, Vector2[] heads)
        {
            List<(int Start, int End)> runs = problem.FindStreamRuns();
            for (int r = 0; r < runs.Count; r++)
            {
                var (a, b) = runs[r];
                if (problem.MeasureGrowth(heads, a, b) < 0.94 * problem.WantedGrowth(a, b))
                    return true;
            }

            return false;
        }

        private static BurstTurn[] planStreamTurns(Item[] slice, bool pinFirst, AmplifyOptions options)
        {
            // Quick check: skip planning completely if this phrase contains no multi-step stream.
            var probe = new PhraseProblem(slice, pinFirst, options with { SpacingMultiplier = reference_factor, AccentAngles = false }, Array.Empty<BurstTurn>(), new float[slice.Length]);
            List<(int Start, int End)> runs = probe.FindStreamRuns();

            if (runs.Count == 0)
                return Array.Empty<BurstTurn>();

            var accepted = new List<BurstTurn>();
            var refused = new HashSet<int>();
            PhraseProblem problem = probe;
            Vector2[] heads = problem.Solve(fast: true);
            float[] zeroRot = new float[slice.Length];

            for (int round = 0; round < burst_rounds; round++)
            {
                var taken = new HashSet<int>(refused);
                foreach (BurstTurn t in accepted)
                    taken.Add(t.Start);

                List<BurstTurn> proposed = problem.ProposeStreamTurns(heads, runs, taken);
                if (proposed.Count == 0)
                    break;

                double[] before = new double[runs.Count];
                for (int r = 0; r < runs.Count; r++)
                    before[r] = problem.MeasureGrowth(heads, runs[r].Start, runs[r].End);

                var trialTurns = new List<BurstTurn>(accepted.Count + proposed.Count);
                trialTurns.AddRange(accepted);
                trialTurns.AddRange(proposed);

                var trial = new PhraseProblem(slice, pinFirst, options with { SpacingMultiplier = reference_factor, AccentAngles = false }, trialTurns, zeroRot);
                trial.StartFrom(problem);
                Vector2[] trialHeads = trial.Solve(fast: true);

                double[] after = new double[runs.Count];
                for (int r = 0; r < runs.Count; r++)
                    after[r] = trial.MeasureGrowth(trialHeads, runs[r].Start, runs[r].End);

                bool allAccepted = true;

                foreach (BurstTurn t in proposed)
                {
                    bool good = true;

                    for (int r = 0; r < runs.Count && good; r++)
                    {
                        var (a, b) = runs[r];
                        if (b < t.Start - burst_spacing_objects || a > t.End + burst_spacing_objects)
                            continue;

                        double wanted = problem.WantedGrowth(a, b);
                        good = a == t.Start
                            ? after[r] >= before[r] + burst_min_gain
                            : Math.Min(after[r], wanted) >= Math.Min(before[r], wanted) - burst_max_neighbour_loss;
                    }

                    if (good)
                        accepted.Add(t);
                    else
                    {
                        refused.Add(t.Start);
                        allAccepted = false;
                    }
                }

                if (allAccepted)
                {
                    problem = trial;
                    heads = trialHeads;
                }
                else
                {
                    var kept = new PhraseProblem(slice, pinFirst, options with { SpacingMultiplier = reference_factor, AccentAngles = false }, accepted, zeroRot);
                    kept.StartFrom(problem);
                    problem = kept;
                    heads = kept.Solve(fast: true);
                }
            }

            if (accepted.Count == 0)
                return Array.Empty<BurstTurn>();

            double share = Math.Clamp((options.SpacingMultiplier - 1.0) / (full_turn_factor - 1.0), 0.0, 1.0);
            double curve = curve_share * Math.Clamp((options.SpacingMultiplier - 1.0) / (reference_factor - 1.0), 0.0, 1.0);

            var scaled = new BurstTurn[accepted.Count];
            for (int i = 0; i < accepted.Count; i++)
                scaled[i] = accepted[i] with { Angle = share * accepted[i].Angle, Share = curve };

            return scaled;
        }

        private static bool proposeSliderRotations(Item[] slice, PhraseProblem problem, Vector2[] heads, float[] rotations)
        {
            bool anyChanged = false;
            int n = slice.Length;

            for (int i = 0; i < n; i++)
            {
                if (!slice[i].IsSlider || slice[i].PathSpan < min_rotatable_slider_span_px || problem.IsPinned(i))
                    continue;

                var (lo0, hi0, valid0) = computeHeadBounds(slice[i], 0);
                if (!valid0)
                    continue;

                bool nearWall = heads[i].X <= lo0.X + 1.5f || heads[i].X >= hi0.X - 1.5f
                                || heads[i].Y <= lo0.Y + 1.5f || heads[i].Y >= hi0.Y - 1.5f;

                bool inShort = i > 0 && problem.IsMoveShort(heads, i);
                bool outShort = i + 1 < n && problem.IsMoveShort(heads, i + 1);
                bool wallBlocked = nearWall && (inShort || outShort);

                double avgDeflection = computeAdjacentDeflection(problem, heads, i);

                if (!wallBlocked && Math.Abs(avgDeflection) < 6.0 * Math.PI / 180.0)
                    continue;

                float baseScore = evaluateSliderOrientation(slice, problem, heads, i, 0, avgDeflection, wallBlocked);
                float bestTheta = 0;
                float bestScore = baseScore;

                if (wallBlocked)
                {
                    for (int deg = -slider_max_turn_degrees; deg <= slider_max_turn_degrees; deg += slider_turn_step_degrees)
                    {
                        if (deg == 0)
                            continue;

                        float theta = deg * MathF.PI / 180f;
                        float score = evaluateSliderOrientation(slice, problem, heads, i, theta, avgDeflection, wallBlocked: true);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestTheta = theta;
                        }
                    }
                }
                else
                {
                    float candidate = Math.Clamp((float)(avgDeflection * 0.65), -slider_flow_max_degrees * MathF.PI / 180f, slider_flow_max_degrees * MathF.PI / 180f);
                    float score = evaluateSliderOrientation(slice, problem, heads, i, candidate, avgDeflection, wallBlocked: false);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTheta = candidate;
                    }
                }

                if (bestTheta != 0 && bestScore > baseScore + 0.04f)
                {
                    rotations[i] = bestTheta;
                    anyChanged = true;
                }
            }

            return anyChanged;
        }

        private static double computeAdjacentDeflection(PhraseProblem problem, Vector2[] heads, int i)
        {
            double sum = 0;
            int count = 0;

            if (i > 0 && problem.TryGetMoveDeflection(heads, i, out double dIn))
            {
                sum += dIn;
                count++;
            }

            if (i + 1 < heads.Length && problem.TryGetMoveDeflection(heads, i + 1, out double dOut))
            {
                sum += dOut;
                count++;
            }

            return count > 0 ? sum / count : 0;
        }

        private static float evaluateSliderOrientation(Item[] slice, PhraseProblem problem, Vector2[] heads, int i, float theta, double avgDeflection, bool wallBlocked)
        {
            var (lo, hi, valid) = computeHeadBounds(slice[i], theta);
            if (!valid)
                return float.NegativeInfinity;

            // Clamp head into the rotated box to estimate feasible position.
            var clampedHead = new Vector2(Math.Clamp(heads[i].X, lo.X, hi.X), Math.Clamp(heads[i].Y, lo.Y, hi.Y));
            float shiftPenalty = Vector2.Distance(clampedHead, heads[i]) / 120f;

            // Measure available playfield slack around the head (prevents hugging walls).
            float slackX = MathF.Min(clampedHead.X - lo.X, hi.X - clampedHead.X);
            float slackY = MathF.Min(clampedHead.Y - lo.Y, hi.Y - clampedHead.Y);
            float slackScore = (MathF.Min(slackX, 32f) + MathF.Min(slackY, 32f)) / 64f;

            // Measure how well the rotated slider tail aligns the outgoing move toward its wanted vector.
            float outScore = 0;
            if (i + 1 < slice.Length && problem.IsMoveActive(i + 1))
            {
                Vector2 rotatedEnd = clampedHead + Rotate(slice[i].Body, theta);
                Vector2 outVec = heads[i + 1] - rotatedEnd;
                double wantedLen = problem.WantedLength(i + 1);
                if (wantedLen > 1.0)
                {
                    Vector2 wantedVec = problem.OriginalUnit(i + 1) * (float)wantedLen;
                    float err = Vector2.Distance(outVec, wantedVec) / (float)wantedLen;
                    outScore = -0.55f * err;
                }
            }

            // Reward aligning with local flow deflection when not wall-blocked.
            float flowScore = 0;
            if (!wallBlocked && Math.Abs(avgDeflection) > 1e-3)
            {
                float diff = MathF.Abs(theta - (float)avgDeflection);
                flowScore = -0.35f * diff;
            }

            float maxRad = slider_max_turn_degrees * MathF.PI / 180f;
            float anglePenalty = 0.08f * (theta / maxRad) * (theta / maxRad);

            return (wallBlocked ? 0.85f * slackScore : 0.25f * slackScore) + outScore + flowScore - shiftPenalty - anglePenalty;
        }

        private static (PhraseProblem, Vector2[]) resolveAccidentalOverlaps(
            Item[] slice,
            bool pinFirst,
            AmplifyOptions options,
            BurstTurn[] turns,
            float[] rotations,
            PhraseProblem problem,
            Vector2[] heads)
        {
            int n = slice.Length;
            float avgRadius = slice[0].Radius;
            float checkPx = Math.Clamp(avgRadius * 1.05f, 24f, 34f);
            float farPx = MathF.Max(checkPx * 1.75f, overlap_far_min_px);

            var initialOverlaps = findOverlaps(slice, heads, rotations, checkPx, farPx);
            if (initialOverlaps.Count == 0)
                return (problem, heads);

            // When AccentAngles is on, compute the plain (non-accented) baseline so we can fall back cleanly if needed.
            PhraseProblem? plainProblem = null;
            Vector2[]? plainHeads = null;
            bool[]? excused = null;

            if (options.AccentAngles)
            {
                plainProblem = new PhraseProblem(slice, pinFirst, options with { AccentAngles = false }, turns, rotations);
                plainHeads = Math.Abs(options.SpacingMultiplier - 1.0) < 1e-6
                    ? slice.Select(s => s.Start).ToArray()
                    : plainProblem.Solve();

                excused = new bool[n];
                foreach (var ov in findOverlaps(slice, plainHeads, rotations, MathF.Max(18f, checkPx - 3f), farPx))
                    excused[ov.Target] = true;
            }

            var bestProblem = problem;
            var bestHeads = heads;
            int bestCount = countUnexcused(initialOverlaps, excused);

            if (bestCount == 0)
                return (problem, heads);

            var droppedAccents = new bool[n];

            for (int round = 0; round < overlap_max_rounds; round++)
            {
                var currentOverlaps = findOverlaps(slice, heads, rotations, checkPx, farPx);
                int unexcusedCount = countUnexcused(currentOverlaps, excused);
                if (unexcusedCount == 0)
                    return (problem, heads);

                bool modified = false;

                if (options.AccentAngles)
                {
                    int widen = accent_widen[Math.Min(round, accent_widen.Length - 1)];

                    foreach (var ov in currentOverlaps)
                    {
                        if (excused != null && excused[ov.Target])
                            continue;

                        for (int m = Math.Max(0, ov.Recent - widen); m <= ov.Target; m++)
                        {
                            if (!droppedAccents[m])
                            {
                                droppedAccents[m] = true;
                                modified = true;
                            }
                        }
                    }

                    if (modified)
                        problem.DropAccents(droppedAccents);
                }

                // Always-on geometric repulsion for overlaps not caused by AccentAngles (e.g., wall rebounds).
                if (!modified)
                {
                    double repWeight = 0.035 * (round + 1);

                    foreach (var ov in currentOverlaps)
                    {
                        if (excused != null && excused[ov.Target])
                            continue;

                        if (problem.IsPinned(ov.Target))
                            continue;

                        Vector2 origDiff = slice[ov.Target].Start - ov.OriginalRecentPoint;
                        Vector2 pushDir = origDiff.Length > 1e-2f
                            ? origDiff.Normalized()
                            : (heads[ov.Target] - ov.CurrentRecentPoint).Length > 1e-2f
                                ? (heads[ov.Target] - ov.CurrentRecentPoint).Normalized()
                                : new Vector2(1, 0);

                        float pushDist = (checkPx + 8f) - ov.CurrentDistance;
                        Vector2 desired = heads[ov.Target] + pushDir * MathF.Max(pushDist, 6f);
                        problem.AddRepulsion(ov.Target, desired, repWeight);
                        modified = true;
                    }
                }

                if (!modified)
                    break;

                heads = problem.Solve();
                int newCount = countUnexcused(findOverlaps(slice, heads, rotations, checkPx, farPx), excused);

                if (newCount < bestCount)
                {
                    bestCount = newCount;
                    bestProblem = problem;
                    bestHeads = (Vector2[])heads.Clone();
                }

                if (newCount == 0)
                    return (bestProblem, bestHeads);
            }

            if (options.AccentAngles && plainProblem != null && plainHeads != null)
            {
                int plainCount = countUnexcused(findOverlaps(slice, plainHeads, rotations, checkPx, farPx), excused);
                if (plainCount < bestCount)
                    return (plainProblem, plainHeads);
            }

            return (bestProblem, bestHeads);
        }

        private static int countUnexcused(List<OverlapHit> overlaps, bool[]? excused)
        {
            if (excused == null)
                return overlaps.Count;

            int count = 0;
            foreach (var ov in overlaps)
            {
                if (!excused[ov.Target])
                    count++;
            }

            return count;
        }

        private static List<OverlapHit> findOverlaps(Item[] slice, Vector2[] heads, float[] rotations, float checkPx, float farPx)
        {
            var hits = new List<OverlapHit>();
            int n = slice.Length;

            for (int i = 2; i < n; i++)
            {
                if (slice[i].IsSpinner)
                    continue;

                double windowMs = overlap_lookback_beats * slice[i].BeatLength;

                for (int j = i - 2; j >= 0 && j >= i - overlap_lookback_objects; j--)
                {
                    if (slice[j].IsSpinner || slice[i].StartTime - slice[j].StartTime > windowMs)
                        break;

                    Vector2 curHeadJ = heads[j];
                    Vector2 curEndJ = curHeadJ + Rotate(slice[j].Body, rotations[j]);
                    Vector2 origHeadJ = slice[j].Start;
                    Vector2 origEndJ = origHeadJ + slice[j].Body;

                    float curDist = Vector2.Distance(heads[i], curHeadJ);
                    Vector2 curNearest = curHeadJ;
                    Vector2 origNearest = origHeadJ;

                    float endDist = Vector2.Distance(heads[i], curEndJ);
                    if (endDist < curDist)
                    {
                        curDist = endDist;
                        curNearest = curEndJ;
                        origNearest = origEndJ;
                    }

                    float origDist = MathF.Min(Vector2.Distance(slice[i].Start, origHeadJ), Vector2.Distance(slice[i].Start, origEndJ));

                    if (slice[j].IsSlider)
                    {
                        Vector2 curMidJ = curHeadJ + Rotate(slice[j].MidOffset, rotations[j]);
                        Vector2 origMidJ = origHeadJ + slice[j].MidOffset;
                        float midDist = Vector2.Distance(heads[i], curMidJ);
                        if (midDist < curDist)
                        {
                            curDist = midDist;
                            curNearest = curMidJ;
                            origNearest = origMidJ;
                        }

                        origDist = MathF.Min(origDist, Vector2.Distance(slice[i].Start, origMidJ));
                    }

                    if (curDist < checkPx && origDist >= farPx)
                    {
                        hits.Add(new OverlapHit(i, j, curDist, curNearest, origNearest));
                        break;
                    }
                }
            }

            return hits;
        }

        private static (Vector2 Lo, Vector2 Hi, bool Valid) computeHeadBounds(Item item, float rotation)
        {
            float minX = 0, maxX = 0, minY = 0, maxY = 0;

            if (item.PathPoints.Length > 0)
            {
                float cos = MathF.Cos(rotation);
                float sin = MathF.Sin(rotation);

                foreach (var p in item.PathPoints)
                {
                    float rx = cos * p.X - sin * p.Y;
                    float ry = sin * p.X + cos * p.Y;
                    minX = MathF.Min(minX, rx);
                    maxX = MathF.Max(maxX, rx);
                    minY = MathF.Min(minY, ry);
                    maxY = MathF.Max(maxY, ry);
                }
            }

            float loX = item.AllowedMin.X - minX;
            float hiX = item.AllowedMax.X - maxX;
            float loY = item.AllowedMin.Y - minY;
            float hiY = item.AllowedMax.Y - maxY;

            if (hiX - loX > 2 * fit_slack)
            {
                loX += fit_slack;
                hiX -= fit_slack;
            }

            if (hiY - loY > 2 * fit_slack)
            {
                loY += fit_slack;
                hiY -= fit_slack;
            }

            return (new Vector2(loX, loY), new Vector2(hiX, hiY), loX <= hiX && loY <= hiY);
        }

        private static bool isValidSolution(Item[] slice, Vector2[] heads, float[] rotations)
        {
            for (int i = 0; i < slice.Length; i++)
            {
                if (!isFinite(heads[i]) || !isWithinAllowedBounds(slice[i], heads[i], rotations[i]))
                    return false;
            }

            return true;
        }

        private static bool isWithinAllowedBounds(Item item, Vector2 head, float rotation)
        {
            const float eps = 0.25f;

            if (head.X < item.AllowedMin.X - eps || head.X > item.AllowedMax.X + eps
                || head.Y < item.AllowedMin.Y - eps || head.Y > item.AllowedMax.Y + eps)
                return false;

            if (item.PathPoints.Length == 0)
                return true;

            float cos = MathF.Cos(rotation);
            float sin = MathF.Sin(rotation);

            foreach (var p in item.PathPoints)
            {
                float qx = head.X + cos * p.X - sin * p.Y;
                float qy = head.Y + sin * p.X + cos * p.Y;

                if (qx < item.AllowedMin.X - eps || qx > item.AllowedMax.X + eps
                    || qy < item.AllowedMin.Y - eps || qy > item.AllowedMax.Y + eps)
                    return false;
            }

            return true;
        }

        private static bool isFinite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);

        private static double smoothStep(double edge0, double edge1, double x)
        {
            double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }

        private static double signedAngle(Vector2 a, Vector2 b)
            => Math.Atan2(a.X * b.Y - a.Y * b.X, a.X * b.X + a.Y * b.Y);

        /// <summary>
        /// Exact projection of <c>(a0, b0)</c> onto the convex trapezoid <c>minA &lt;= a &lt;= maxA</c>,
        /// <c>|b| &lt;= t * a</c> under the anisotropic quadratic metric <c>alpha * da^2 + beta * db^2</c>.
        /// </summary>
        private static double projectTrapezoid(double a0, double b0, double minA, double maxA, double t, double alpha, double beta, out double a)
        {
            if (a0 >= minA && a0 <= maxA && Math.Abs(b0) <= t * a0)
            {
                a = a0;
                return b0;
            }

            double bestCost = double.PositiveInfinity;
            double bestA = minA;
            double bestB = 0;

            void tryPoint(double x, double y)
            {
                double dx = x - a0;
                double dy = y - b0;
                double cost = alpha * dx * dx + beta * dy * dy;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestA = x;
                    bestB = y;
                }
            }

            tryPoint(minA, Math.Clamp(b0, -t * minA, t * minA));
            tryPoint(maxA, Math.Clamp(b0, -t * maxA, t * maxA));

            double denom = alpha + beta * t * t;
            if (denom > 0)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    double x = Math.Clamp((alpha * a0 + beta * sign * t * b0) / denom, minA, maxA);
                    tryPoint(x, sign * t * x);
                }
            }

            a = bestA;
            return bestB;
        }

        private readonly record struct Item(
            bool IsSpinner,
            bool IsSlider,
            double StartTime,
            double EndTime,
            double BeatLength,
            bool InKiai,
            float Radius,
            Vector2 Start,
            Vector2 Body,
            Vector2 MidOffset,
            float PathSpan,
            Vector2[] PathPoints,
            Vector2 AllowedMin,
            Vector2 AllowedMax);

        private sealed record BurstTurn(int Start, int End, double Angle, Vector2[] Directions, double Share = 1.0);

        private readonly record struct OverlapHit(int Target, int Recent, float CurrentDistance, Vector2 CurrentRecentPoint, Vector2 OriginalRecentPoint);

        private sealed class PhraseProblem
        {
            private enum MoveKind { Free, Fixed, Ray, Soft }

            private readonly Item[] items;
            private readonly int n;
            private readonly Vector2[] lo, hi;
            private readonly MoveKind[] kind;
            private readonly Vector2[] unit, originalUnit, body;
            private readonly double[] length, minLen, maxLen, targetLen;
            private readonly double[] cone;
            private readonly double[] growthTie, turnTie, turnTarget;
            private readonly double[] anchorWeight, repulsionWeight;
            private readonly Vector2[] repulsionTarget;
            private readonly bool[] isStream;
            private readonly bool[] pinned;

            private double[]? q, zq, yq, zd, yd, constant;
            private BandedMatrix? system;

            public PhraseProblem(Item[] items, bool pinFirst, AmplifyOptions options, IReadOnlyList<BurstTurn> turns, float[] rotations)
            {
                this.items = items;
                n = items.Length;
                lo = new Vector2[n];
                hi = new Vector2[n];
                kind = new MoveKind[n];
                unit = new Vector2[n];
                body = new Vector2[n];
                length = new double[n];
                minLen = new double[n];
                maxLen = new double[n];
                targetLen = new double[n];
                cone = new double[n];
                growthTie = new double[n];
                turnTie = new double[n];
                turnTarget = new double[n];
                anchorWeight = new double[n];
                repulsionWeight = new double[n];
                repulsionTarget = new Vector2[n];
                isStream = new bool[n];
                pinned = new bool[n];

                for (int i = 0; i < n; i++)
                {
                    bool isPin = items[i].IsSpinner || (pinFirst && i == 0);
                    var (boxLo, boxHi, valid) = computeHeadBounds(items[i], rotations[i]);

                    if (isPin || !valid)
                    {
                        lo[i] = hi[i] = items[i].Start;
                        pinned[i] = true;
                    }
                    else
                    {
                        lo[i] = boxLo;
                        hi[i] = boxHi;
                    }

                    anchorWeight[i] = anchor_weight;
                }

                double k = options.SpacingMultiplier;
                bool expanding = k >= 1.0;
                var pause = new bool[n];

                for (int i = 1; i < n; i++)
                {
                    var prev = items[i - 1];
                    var curr = items[i];
                    body[i] = Rotate(prev.Body, rotations[i - 1]);

                    double gap = curr.StartTime - prev.EndTime;
                    if (prev.IsSpinner || curr.IsSpinner || gap >= restart_beats * curr.BeatLength - time_slack_ms)
                        continue;

                    Vector2 v = curr.Start - (prev.Start + prev.Body);
                    if (pinned[i] && pinned[i - 1])
                    {
                        kind[i] = MoveKind.Fixed;
                        unit[i] = v;
                        continue;
                    }

                    isStream[i] = gap <= stream_gap_in_beats * curr.BeatLength + time_slack_ms
                                  && !(prev.IsSlider && curr.IsSlider);
                    pause[i] = gap >= pause_beats * curr.BeatLength - time_slack_ms;
                    length[i] = v.Length;

                    if (length[i] < stack_px)
                    {
                        kind[i] = MoveKind.Fixed;
                        unit[i] = v;
                        continue;
                    }

                    kind[i] = MoveKind.Ray;
                    unit[i] = v / (float)length[i];

                    double effectiveK = !isStream[i]
                        ? k
                        : options.SpaceStreams
                            ? (expanding ? Math.Min(k, stream_factor_cap) : k)
                            : 1.0;

                    double tgt = TargetLength(length[i], effectiveK);
                    targetLen[i] = tgt;

                    if (tgt >= length[i])
                    {
                        minLen[i] = length[i];
                        maxLen[i] = tgt;
                    }
                    else
                    {
                        minLen[i] = tgt;
                        maxLen[i] = length[i];
                    }
                }

                var accented = new bool[n];
                for (int i = 2; i < n; i++)
                {
                    if (kind[i] != MoveKind.Ray || kind[i - 1] != MoveKind.Ray || pause[i])
                        continue;

                    bool bothStreams = isStream[i] && isStream[i - 1];
                    turnTie[i] = bothStreams ? stream_turn_tie : jump_turn_tie;

                    if (!pause[i - 1])
                        growthTie[i] = bothStreams ? stream_growth_tie : (!isStream[i] && !isStream[i - 1] ? jump_growth_tie : 0);

                    if (options.AccentAngles && isJump(i) && isJump(i - 1))
                    {
                        double turn = signedAngle(unit[i - 1], unit[i]);
                        double wanted = Math.Sign(turn) * accent_radians * Math.Abs(Math.Sin(turn));
                        double growth = 0.5 * (targetLen[i] / length[i] + targetLen[i - 1] / length[i - 1]);
                        turnTarget[i] = Math.Sin(wanted) * growth;
                        turnTie[i] = accent_turn_tie;
                        accented[i] = accented[i - 1] = true;
                    }
                }

                for (int i = 1; i < n; i++)
                {
                    if (kind[i] != MoveKind.Ray)
                        continue;

                    bool changesLength = Math.Abs(targetLen[i] - length[i]) > 1e-4;
                    if (!changesLength && !accented[i])
                        continue;

                    double deg = !expanding
                        ? compact_cone_degrees
                        : isStream[i] ? stream_cone_degrees : jump_cone_degrees;

                    cone[i] = Math.Tan(deg * Math.PI / 180.0);
                }

                originalUnit = (Vector2[])unit.Clone();

                foreach (var t in turns)
                    applyBurstTurn(t);

                bool isJump(int idx) => kind[idx] == MoveKind.Ray && !isStream[idx] && length[idx] >= accent_move_px;
            }

            public bool IsPinned(int i) => pinned[i];

            public bool IsMoveActive(int i) => kind[i] == MoveKind.Ray || kind[i] == MoveKind.Soft;

            public double WantedLength(int i) => targetLen[i];

            public Vector2 OriginalUnit(int i) => originalUnit[i];

            public bool IsMoveShort(Vector2[] heads, int i)
            {
                if (kind[i] != MoveKind.Ray || targetLen[i] <= length[i] + 1e-3)
                    return false;

                double actual = (heads[i] - heads[i - 1] - body[i]).Length;
                return actual < 0.92 * targetLen[i];
            }

            public bool TryGetMoveDeflection(Vector2[] heads, int i, out double deflectionRad)
            {
                deflectionRad = 0;
                if (kind[i] != MoveKind.Ray && kind[i] != MoveKind.Soft)
                    return false;

                Vector2 move = heads[i] - heads[i - 1] - body[i];
                if (move.Length < 1f)
                    return false;

                deflectionRad = signedAngle(originalUnit[i], move);
                return true;
            }

            public void DropAccents(bool[] dropped)
            {
                for (int i = 2; i < n; i++)
                {
                    if (dropped[i - 1])
                        turnTarget[i] = 0;
                }

                constant = null;
            }

            public void AddRepulsion(int i, Vector2 target, double weight)
            {
                repulsionTarget[i] = new Vector2(Math.Clamp(target.X, lo[i].X, hi[i].X), Math.Clamp(target.Y, lo[i].Y, hi[i].Y));
                repulsionWeight[i] = Math.Max(repulsionWeight[i], weight);
                system = null;
                constant = null;
            }

            public void StartFrom(PhraseProblem other)
            {
                if (other.q == null || other.n != n)
                    return;

                q = (double[])other.q.Clone();
                zq = (double[])other.zq!.Clone();
                yq = (double[])other.yq!.Clone();
                zd = (double[])other.zd!.Clone();
                yd = (double[])other.yd!.Clone();
            }

            public List<(int Start, int End)> FindStreamRuns()
            {
                var runs = new List<(int, int)>();

                for (int i = 1; i < n; i++)
                {
                    if (!isGrowingStreamStep(i))
                        continue;

                    int s0 = i;
                    while (isGrowingStreamStep(i + 1))
                        i++;

                    if (i - s0 + 1 >= min_burst_moves)
                        runs.Add((s0, i));
                }

                return runs;
            }

            public List<BurstTurn> ProposeStreamTurns(Vector2[] heads, List<(int Start, int End)> runs, HashSet<int> taken)
            {
                var proposed = new List<BurstTurn>();
                int skipUntil = 0;

                foreach (var (s0, e) in runs)
                {
                    if (taken.Contains(s0) || s0 <= skipUntil)
                        continue;

                    double theta = findFittingTurn(heads, s0, e);
                    if (theta == 0)
                        continue;

                    var dirs = new Vector2[e - s0 + 1];
                    for (int j = s0; j <= e; j++)
                        dirs[j - s0] = solvedDirection(heads, j);

                    proposed.Add(new BurstTurn(s0, e, theta, dirs));
                    skipUntil = e + burst_spacing_objects;
                }

                return proposed;
            }

            public double WantedGrowth(int s0, int e)
            {
                double before = 0, wanted = 0;
                for (int j = s0; j <= e; j++)
                {
                    before += length[j];
                    wanted += targetLen[j];
                }

                return before > 0 ? wanted / before : 1.0;
            }

            public double MeasureGrowth(Vector2[] heads, int s0, int e)
            {
                double before = 0, after = 0;
                for (int j = s0; j <= e; j++)
                {
                    before += length[j];
                    after += (heads[j] - heads[j - 1] - body[j]).Length;
                }

                return before > 0 ? after / before : 1.0;
            }

            public Vector2[] Solve(bool fast = false)
            {
                int m = 2 * n;
                bool warm = q != null;

                if (!warm)
                {
                    q = new double[m];
                    for (int i = 0; i < n; i++)
                    {
                        q[2 * i] = items[i].Start.X;
                        q[2 * i + 1] = items[i].Start.Y;
                    }

                    zq = (double[])q.Clone();
                    yq = new double[m];
                    zd = new double[m];
                    yd = new double[m];

                    for (int i = 1; i < n; i++)
                    {
                        Vector2 v = items[i].Start - items[i - 1].Start - body[i];
                        zd[2 * i] = v.X;
                        zd[2 * i + 1] = v.Y;
                    }
                }

                system ??= buildMatrix();
                constant ??= buildConstant();

                int maxIt = fast ? plan_max_iterations : max_iterations;
                int baseLeast = fast ? plan_min_iterations : min_iterations;
                double tol = fast ? plan_tolerance_px : tolerance_px;

                return iterate(q!, zq!, yq!, zd!, yd!, system, constant, warm ? baseLeast / 3 : baseLeast, maxIt, tol);
            }

            private void applyBurstTurn(BurstTurn t)
            {
                for (int j = t.Start; j <= t.End; j++)
                {
                    Vector2 d = Vector2.Lerp(unit[j], t.Directions[j - t.Start], (float)t.Share);
                    unit[j] = Rotate(d.Length > 1e-3f ? d.Normalized() : unit[j], (float)t.Angle);
                    cone[j] = Math.Tan(turned_cone_degrees * Math.PI / 180.0);
                }

                turnTie[t.Start] = 0;
                turnTarget[t.Start] = 0;
                system = null;
                constant = null;

                int o = t.End + 1;
                if (o >= n)
                    return;

                turnTie[o] = 0;
                turnTarget[o] = 0;
                growthTie[o] = 0;

                if (o + 1 < n)
                {
                    turnTie[o + 1] = 0;
                    turnTarget[o + 1] = 0;
                    growthTie[o + 1] = 0;
                }

                if (kind[o] == MoveKind.Ray)
                    kind[o] = MoveKind.Soft;
            }

            private bool isGrowingStreamStep(int j)
                => j >= 1 && j < n && kind[j] == MoveKind.Ray && isStream[j] && targetLen[j] > length[j] && !pinned[j];

            private Vector2 solvedDirection(Vector2[] heads, int j)
            {
                Vector2 v = heads[j] - heads[j - 1] - body[j];
                float d = v.Length;
                return d > 1f ? v / d : unit[j];
            }

            private Vector2[] buildBurstShape(Vector2[] heads, int s0, int e, double theta, double f)
            {
                var points = new Vector2[e - s0 + 2];
                points[0] = heads[s0 - 1];

                for (int j = s0; j <= e; j++)
                {
                    double z = length[j] + f * (targetLen[j] - length[j]);
                    points[j - s0 + 1] = points[j - s0] + body[j] + Rotate(solvedDirection(heads, j), (float)theta) * (float)z;
                }

                return points;
            }

            private double measureBurstReach(Vector2[] heads, int s0, int e, double theta)
            {
                bool fits(double f)
                {
                    var points = buildBurstShape(heads, s0, e, theta, f);
                    for (int j = s0; j <= e; j++)
                    {
                        var p = points[j - s0 + 1];
                        if (p.X < lo[j].X || p.Y < lo[j].Y || p.X > hi[j].X || p.Y > hi[j].Y)
                            return false;
                    }

                    return true;
                }

                if (fits(1.0))
                    return 1.0;

                if (!fits(0.0))
                    return 0.0;

                double low = 0.0, high = 1.0;
                for (int it = 0; it < 16; it++)
                {
                    double mid = 0.5 * (low + high);
                    if (fits(mid))
                        low = mid;
                    else
                        high = mid;
                }

                return low;
            }

            private double findFittingTurn(Vector2[] heads, int s0, int e)
            {
                double baseReach = measureBurstReach(heads, s0, e, 0);
                if (baseReach >= burst_reach_target)
                    return 0;

                double best = 0;
                double bestReach = baseReach;
                Vector2 centre = OsuPlayfield.BASE_SIZE / 2;

                for (int deg = 2; deg <= (int)burst_max_turn_degrees; deg += 2)
                {
                    double found = 0;
                    double foundDist = double.PositiveInfinity;

                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        double t = sign * deg * Math.PI / 180.0;
                        double r = measureBurstReach(heads, s0, e, t);

                        if (r < burst_reach_target)
                        {
                            if (r > bestReach + 1e-9)
                            {
                                best = t;
                                bestReach = r;
                            }

                            continue;
                        }

                        var shape = buildBurstShape(heads, s0, e, t, r);
                        double dist = Vector2.Distance(shape[^1], centre);
                        if (dist < foundDist)
                        {
                            found = t;
                            foundDist = dist;
                        }
                    }

                    if (foundDist < double.PositiveInfinity)
                        return found;
                }

                return bestReach >= baseReach + burst_min_gain ? best : 0;
            }

            private Vector2[] iterate(double[] qArr, double[] zqArr, double[] yqArr, double[] zdArr, double[] ydArr, BandedMatrix mat, double[] constVec, int least, int maxIt, double tol)
            {
                int m = 2 * n;
                double[] rhs = new double[m];
                double[] previous = new double[m];

                // Precompute per-move constants outside the inner ADMM loop.
                double[] invL2Arr = new double[n];
                double[] alphaArr = new double[n];
                double[] targetTermArr = new double[n];

                for (int i = 1; i < n; i++)
                {
                    if (kind[i] == MoveKind.Ray || kind[i] == MoveKind.Soft)
                    {
                        double origL = length[i];
                        double invL2 = 2.0 * grow_weight / (origL * origL);
                        invL2Arr[i] = invL2;
                        alphaArr[i] = invL2 + rho;
                        targetTermArr[i] = invL2 * targetLen[i];
                    }
                }

                for (int it = 0; it < maxIt; it++)
                {
                    Array.Copy(zqArr, previous, m);

                    for (int j = 0; j < m; j++)
                        rhs[j] = rho * (zqArr[j] - yqArr[j]) + constVec[j];

                    for (int i = 1; i < n; i++)
                    {
                        if (kind[i] == MoveKind.Free)
                            continue;

                        double tx = rho * (zdArr[2 * i] - ydArr[2 * i] + body[i].X);
                        double ty = rho * (zdArr[2 * i + 1] - ydArr[2 * i + 1] + body[i].Y);
                        rhs[2 * i] += tx;
                        rhs[2 * i + 1] += ty;
                        rhs[2 * i - 2] -= tx;
                        rhs[2 * i - 1] -= ty;
                    }

                    mat.Solve(rhs, qArr);

                    double residual = 0;

                    for (int j = 0; j < m; j++)
                    {
                        residual = Math.Max(residual, Math.Abs(qArr[j] - zqArr[j]));
                        double h = relax * qArr[j] + (1.0 - relax) * zqArr[j];
                        int idx = j >> 1;
                        double low = (j & 1) == 0 ? lo[idx].X : lo[idx].Y;
                        double high = (j & 1) == 0 ? hi[idx].X : hi[idx].Y;
                        zqArr[j] = Math.Clamp(h + yqArr[j], low, high);
                        yqArr[j] += h - zqArr[j];
                        residual = Math.Max(residual, Math.Abs(zqArr[j] - previous[j]));
                    }

                    for (int i = 1; i < n; i++)
                    {
                        if (kind[i] == MoveKind.Free)
                            continue;

                        double ex = qArr[2 * i] - qArr[2 * i - 2] - body[i].X;
                        double ey = qArr[2 * i + 1] - qArr[2 * i - 1] - body[i].Y;
                        residual = Math.Max(residual, Math.Max(Math.Abs(ex - zdArr[2 * i]), Math.Abs(ey - zdArr[2 * i + 1])));

                        double dx = relax * ex + (1.0 - relax) * zdArr[2 * i];
                        double dy = relax * ey + (1.0 - relax) * zdArr[2 * i + 1];
                        double vx = dx + ydArr[2 * i];
                        double vy = dy + ydArr[2 * i + 1];

                        if (kind[i] == MoveKind.Fixed)
                        {
                            zdArr[2 * i] = unit[i].X;
                            zdArr[2 * i + 1] = unit[i].Y;
                        }
                        else if (kind[i] == MoveKind.Soft)
                        {
                            Vector2 u = unit[i];
                            double along = vx * u.X + vy * u.Y;
                            double across = u.X * vy - u.Y * vx;
                            double origL = length[i];
                            double bound = Math.Max(origL, targetLen[i]);
                            double wb = 2.0 * soft_aim_weight / (origL * origL);
                            double a = Math.Clamp((targetTermArr[i] + rho * along) / alphaArr[i], soft_least_ratio * origL, bound);
                            double b = Math.Clamp(rho * across / (wb + rho), -bound, bound);
                            zdArr[2 * i] = a * u.X - b * u.Y;
                            zdArr[2 * i + 1] = a * u.Y + b * u.X;
                        }
                        else
                        {
                            Vector2 u = unit[i];
                            double along = vx * u.X + vy * u.Y;
                            double across = u.X * vy - u.Y * vx;
                            double alpha = alphaArr[i];
                            double a0 = (targetTermArr[i] + rho * along) / alpha;
                            double b = projectTrapezoid(a0, across, minLen[i], maxLen[i], cone[i], alpha, rho, out double a);
                            zdArr[2 * i] = a * u.X - b * u.Y;
                            zdArr[2 * i + 1] = a * u.Y + b * u.X;
                        }

                        ydArr[2 * i] += dx - zdArr[2 * i];
                        ydArr[2 * i + 1] += dy - zdArr[2 * i + 1];
                    }

                    if (it >= least && residual < tol)
                        break;
                }

                var heads = new Vector2[n];
                for (int i = 0; i < n; i++)
                    heads[i] = pinned[i] ? items[i].Start : new Vector2((float)zqArr[2 * i], (float)zqArr[2 * i + 1]);

                // Enforce exact preservation of stacks and locked moves within their feasible boxes.
                for (int i = 1; i < n; i++)
                {
                    if (pinned[i])
                        continue;

                    if (kind[i] == MoveKind.Fixed)
                    {
                        Vector2 exact = heads[i - 1] + body[i] + unit[i];
                        heads[i] = new Vector2(Math.Clamp(exact.X, lo[i].X, hi[i].X), Math.Clamp(exact.Y, lo[i].Y, hi[i].Y));
                    }
                    else if (kind[i] == MoveKind.Ray && cone[i] == 0 && Math.Abs(targetLen[i] - length[i]) < 1e-6)
                    {
                        Vector2 exact = heads[i - 1] + body[i] + unit[i] * (float)length[i];
                        heads[i] = new Vector2(Math.Clamp(exact.X, lo[i].X, hi[i].X), Math.Clamp(exact.Y, lo[i].Y, hi[i].Y));
                    }
                }

                return heads;
            }

            private BandedMatrix buildMatrix()
            {
                var mat = new BandedMatrix(2 * n, 5);

                for (int i = 0; i < n; i++)
                {
                    double diag = rho + 2.0 * (anchorWeight[i] + repulsionWeight[i]);
                    mat.Add(2 * i, 2 * i, diag);
                    mat.Add(2 * i + 1, 2 * i + 1, diag);
                }

                for (int i = 1; i < n; i++)
                {
                    if (kind[i] == MoveKind.Free)
                        continue;

                    for (int c = 0; c < 2; c++)
                    {
                        int x = 2 * i + c;
                        int y = 2 * (i - 1) + c;
                        mat.Add(x, x, rho);
                        mat.Add(y, y, rho);
                        mat.Add(x, y, -rho);
                    }
                }

                visitTies((index, coef, weight, _) =>
                {
                    for (int s = 0; s < 6; s++)
                    {
                        for (int t = 0; t <= s; t++)
                            mat.Add(index[s], index[t], 2.0 * weight * coef[s] * coef[t]);
                    }
                });

                mat.Factor();
                return mat;
            }

            private double[] buildConstant()
            {
                var vec = new double[2 * n];

                for (int i = 0; i < n; i++)
                {
                    vec[2 * i] += 2.0 * anchorWeight[i] * items[i].Start.X + 2.0 * repulsionWeight[i] * repulsionTarget[i].X;
                    vec[2 * i + 1] += 2.0 * anchorWeight[i] * items[i].Start.Y + 2.0 * repulsionWeight[i] * repulsionTarget[i].Y;
                }

                visitTies((index, coef, weight, c0) =>
                {
                    for (int s = 0; s < 6; s++)
                        vec[index[s]] -= 2.0 * weight * c0 * coef[s];
                });

                return vec;
            }

            private void visitTies(Action<int[], double[], double, double> visitor)
            {
                var coef = new double[6];
                var index = new int[6];

                for (int i = 2; i < n; i++)
                {
                    for (int pass = 0; pass < 2; pass++)
                    {
                        double weight = pass == 0 ? growthTie[i] : turnTie[i];
                        if (weight == 0)
                            continue;

                        Vector2 ui = unit[i] / (float)length[i];
                        Vector2 up = unit[i - 1] / (float)length[i - 1];

                        if (pass == 1)
                        {
                            ui = new Vector2(-ui.Y, ui.X);
                            up = new Vector2(-up.Y, up.X);
                        }

                        index[0] = 2 * (i - 2);
                        index[1] = index[0] + 1;
                        index[2] = 2 * (i - 1);
                        index[3] = index[2] + 1;
                        index[4] = 2 * i;
                        index[5] = index[4] + 1;

                        coef[0] = up.X;
                        coef[1] = up.Y;
                        coef[2] = -ui.X - up.X;
                        coef[3] = -ui.Y - up.Y;
                        coef[4] = ui.X;
                        coef[5] = ui.Y;

                        double c0 = -Vector2.Dot(ui, body[i]) + Vector2.Dot(up, body[i - 1]) - (pass == 1 ? turnTarget[i] : 0);
                        visitor(index, coef, weight, c0);
                    }
                }
            }
        }

        /// <summary>
        /// Symmetric positive-definite lower-banded matrix factored in place via Cholesky (<c>O(size * width^2)</c>).
        /// </summary>
        private sealed class BandedMatrix
        {
            private readonly int size;
            private readonly int width;
            private readonly double[] cells;

            public BandedMatrix(int size, int width)
            {
                this.size = size;
                this.width = width;
                cells = new double[size * (width + 1)];
            }

            private ref double at(int i, int j) => ref cells[i * (width + 1) + (i - j)];

            public void Add(int i, int j, double value)
            {
                if (i < j)
                    (i, j) = (j, i);

                at(i, j) += value;
            }

            public void Factor()
            {
                for (int i = 0; i < size; i++)
                {
                    int jStart = Math.Max(0, i - width);

                    for (int j = jStart; j <= i; j++)
                    {
                        double s = at(i, j);
                        for (int k = jStart; k < j; k++)
                            s -= at(i, k) * at(j, k);

                        at(i, j) = i == j ? Math.Sqrt(Math.Max(s, 1e-12)) : s / at(j, j);
                    }
                }
            }

            public void Solve(double[] b, double[] x)
            {
                for (int i = 0; i < size; i++)
                {
                    double s = b[i];
                    int kStart = Math.Max(0, i - width);
                    for (int k = kStart; k < i; k++)
                        s -= at(i, k) * x[k];

                    x[i] = s / at(i, i);
                }

                for (int i = size - 1; i >= 0; i--)
                {
                    double s = x[i];
                    int kEnd = Math.Min(size - 1, i + width);
                    for (int k = i + 1; k <= kEnd; k++)
                        s -= at(k, i) * x[k];

                    x[i] = s / at(i, i);
                }
            }
        }
    }
}
