// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.VR;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// Torii: a play in VR saves where the head and controllers were, next to its replay.
    /// </summary>
    public partial class TestSceneVRReplayRecording : VRReplayTestScene
    {
        [Test]
        public void TestExtrasSavedWithReplay()
        {
            PlayThrough();
            AddUntilStep("results displayed", () => Player.GetChildScreen() is ResultsScreen);

            VRReplayExtras? extras = null;

            AddUntilStep("score has VR extras", () => Store.GetTypes(SavedScore),
                () => Is.EqualTo(new[] { ReplayExtrasType.VRTracking }));
            AddStep("load extras", () => extras = VRReplayExtrasEncoder.Load(Store, SavedScore.Hash));
            AddAssert("extras loaded", () => extras, () => Is.Not.Null);

            AddAssert("extras name the replay", () => extras!.ScoreHash, () => Is.EqualTo(SavedScore.Hash));
            AddAssert("runtime saved", () => extras!.Runtime, () => Is.EqualTo("Test runtime"));
            AddAssert("frames recorded", () => extras!.Frames, () => Has.Count.GreaterThan(50));
            AddAssert("frames in order of time", () => extras!.Frames.Select(f => f.Time), () => Is.Ordered.Ascending);
            AddAssert("head recorded", () => extras!.Frames.All(f => f.Head != null));
            AddAssert("left hand recorded", () => extras!.Frames.All(f => f.Left.Grip != null && f.Left.Trigger > 0.4f));
            AddAssert("untracked hand recorded as such", () => extras!.Frames.All(f => f.Right.Grip == null));
            AddAssert("hand moves over the frames", () => extras!.Frames[^1].Left.Grip!.Value.Position.X, () => Is.GreaterThan(extras!.Frames[0].Left.Grip!.Value.Position.X));

            AddAssert("pointer saved for the tracked hand", () => extras!.LeftPointer!.Value.Position.Z, () => Is.EqualTo(-0.05f).Within(0.0001f));
            AddAssert("no pointer for the untracked hand", () => extras!.RightPointer, () => Is.Null);
            AddAssert("panel saved once", () => extras!.Panels.Single().Geometry, () => Is.EqualTo(TestTracking.PANEL));
            AddAssert("distance moved saved", () => extras!.LeftDistance, () => Is.GreaterThan(0));
            AddAssert("nothing moved by the other hand", () => extras!.RightDistance, () => Is.Zero);
            AddAssert("pen settings saved", () => extras!.PenOffsetForward, () => Is.EqualTo(5));

            // (By the controller having been moved alone.)
            AddAssert("score marked as a VR play", () => Realm.Run(r => r.Find<ScoreInfo>(Player.Score.ScoreInfo.ID)!.Mods.OfType<ModVR>().Count()), () => Is.EqualTo(1));
            AddAssert("submitted with the mark", () => SoloScoreInfo.ForSubmission(SavedScore).Mods,
                () => Has.Some.Matches<APIMod>(m => m.Acronym == "VR"));

            StatisticsPanel moreInfo = null!;

            AddStep("show more info", () => Add(moreInfo = new StatisticsPanel
            {
                RelativeSizeAxes = Axes.Both,
                State = { Value = Visibility.Visible },
                Score = { Value = SavedScore },
            }));
            AddUntilStep("movement shown", () => moreInfo.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == "Controllers moved"));
            AddStep("remove more info", () => Remove(moreInfo, true));
        }

        [Test]
        public void TestExtrasRemovedWithDeletedScore()
        {
            PlayThrough();
            string hash = string.Empty;

            AddStep("note the replay", () => hash = SavedScore.Hash);
            AddStep("delete the score", () => Dependencies.Get<ScoreManager>().Delete(SavedScore));
            AddAssert("score deleted", () => Realm.Run(r => r.Find<ScoreInfo>(Player.Score.ScoreInfo.ID)!.DeletePending));
            AddAssert("extras removed right away", () => !Store.Exists(hash));
        }
    }
}
