// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;

namespace osu.Game.Tests.Database
{
    /// <summary>
    /// Torii: what was saved along with a replay is removed once its score is.
    /// </summary>
    [TestFixture]
    public class ReplayExtrasCleanupTest : RealmTest
    {
        [Test]
        public void TestExtrasRemovedWithDeletedScore()
        {
            const string deleted = "aaaa0001", kept = "bbbb0002", shared = "cccc0003", restored = "dddd0004";

            RunTestWithRealm((realm, storage) =>
            {
                var extras = new ReplayExtrasStore(storage);

                realm.Write(r =>
                {
                    var ruleset = new OsuRuleset().RulesetInfo;
                    var beatmap = new BeatmapInfo(ruleset, new BeatmapDifficulty(), new BeatmapMetadata());

                    r.Add(ruleset);
                    r.Add(beatmap);

                    ScoreInfo add(string hash, bool deletePending)
                    {
                        var score = new ScoreInfo(beatmap, ruleset) { ID = Guid.NewGuid(), Hash = hash, DeletePending = deletePending };
                        r.Add(score);
                        return score;
                    }

                    add(deleted, true);
                    add(kept, false);

                    // The same replay twice (hashes aren't unique in the database), one of them deleted.
                    add(shared, true);
                    add(shared, false);

                    // Deleted, then brought back before anything was purged.
                    add(restored, true).DeletePending = false;
                });

                foreach (string hash in new[] { deleted, kept, shared, restored })
                    extras.Write(hash, ReplayExtrasType.VRTracking, new byte[] { 1, 2, 3 });

                // Scores pending deletion are purged when the database is next opened (by the game, at startup).
                using (var reopened = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME))
                    Assert.That(reopened.Run(r => r.All<ScoreInfo>().Count()), Is.EqualTo(3));

                Assert.That(extras.Exists(deleted), Is.False);
                Assert.That(extras.Exists(kept));
                Assert.That(extras.Exists(shared));
                Assert.That(extras.Exists(restored));
            });
        }
    }
}
