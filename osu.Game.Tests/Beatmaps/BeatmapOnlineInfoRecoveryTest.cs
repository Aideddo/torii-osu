// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.Select;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public partial class BeatmapOnlineInfoRecoveryTest : RealmTest
    {
        [Test]
        public void TestRecoveryRequiresExactChecksum([Values] bool checksumMatches)
        {
            RunTestWithRealm((realm, _) =>
            {
                var set = CreateBeatmapSet(CreateRuleset());
                set.OnlineID = 456;
                var beatmap = set.Beatmaps.First();
                beatmap.MD5Hash = "checksum";
                realm.Write(r => r.Add(set));
                var snapshot = beatmap.Detach();

                using var api = new DummyAPIAccess
                {
                    HandleRequest = request =>
                    {
                        ((GetBeatmapRequest)request).TriggerSuccess(new APIBeatmap
                        {
                            OnlineID = 123,
                            OnlineBeatmapSetID = 456,
                            Status = BeatmapOnlineStatus.Ranked,
                            Checksum = checksumMatches ? "checksum" : "another version",
                        });
                        return true;
                    }
                };

                Assert.That(BeatmapOnlineInfoRecovery.TryRecover(snapshot, api, realm), Is.EqualTo(checksumMatches));
                Assert.That(snapshot.OnlineID, Is.EqualTo(checksumMatches ? 123 : -1));
                Assert.That(realm.Run(r => r.Find<BeatmapInfo>(beatmap.ID)!.OnlineID), Is.EqualTo(snapshot.OnlineID));
                Assert.That(snapshot.Status, Is.EqualTo(checksumMatches ? BeatmapOnlineStatus.Ranked : BeatmapOnlineStatus.None));
            });
        }

        [Test]
        public void TestStaleSnapshotRecoversWithoutNetwork()
        {
            RunTestWithRealm((realm, _) =>
            {
                var set = CreateBeatmapSet(CreateRuleset());
                var beatmap = set.Beatmaps.First();
                beatmap.MD5Hash = "checksum";
                realm.Write(r => r.Add(set));
                var snapshot = beatmap.Detach();
                realm.Write(r =>
                {
                    var stored = r.Find<BeatmapInfo>(beatmap.ID)!;
                    stored.OnlineID = 123;
                    stored.OnlineMD5Hash = "checksum";
                    stored.Status = BeatmapOnlineStatus.Ranked;
                });

                using var api = new DummyAPIAccess { HandleRequest = _ => throw new AssertionException("Unexpected lookup") };
                Assert.That(BeatmapOnlineInfoRecovery.TryRecover(snapshot, api, realm), Is.True);
                Assert.That(snapshot.OnlineID, Is.EqualTo(123));
                Assert.That(snapshot.Status, Is.EqualTo(BeatmapOnlineStatus.Ranked));
            });
        }

        [Test]
        public void TestSongSelectRepairsMissingID([Values] bool checksumMatches, [Values] bool locallyModified)
        {
            RunTestWithRealm((realm, _) =>
            {
                var set = CreateBeatmapSet(CreateRuleset());
                set.OnlineID = 456;
                var beatmap = set.Beatmaps.First();
                beatmap.MD5Hash = "checksum";
                beatmap.Status = locallyModified ? BeatmapOnlineStatus.LocallyModified : BeatmapOnlineStatus.None;
                realm.Write(r => r.Add(set));
                var id = beatmap.ID;
                var response = new APIBeatmapSet
                {
                    OnlineID = 456,
                    Status = BeatmapOnlineStatus.Ranked,
                    Beatmaps = new[]
                    {
                        new APIBeatmap
                        {
                            OnlineID = 123,
                            OnlineBeatmapSetID = 456,
                            Status = BeatmapOnlineStatus.Ranked,
                            Checksum = checksumMatches ? "checksum" : "another version",
                        }
                    }
                };

                realm.Write(r => RealmPopulatingOnlineLookupSource.UpdateRealmBeatmapSet(r, response));

                var stored = realm.Run(r => r.Find<BeatmapInfo>(id)!.Detach());
                Assert.That(stored.OnlineID, Is.EqualTo(checksumMatches && !locallyModified ? 123 : -1));
                if (locallyModified)
                    Assert.That(stored.Status, Is.EqualTo(BeatmapOnlineStatus.LocallyModified));
                else
                    Assert.That(stored.Status, Is.EqualTo(checksumMatches ? BeatmapOnlineStatus.Ranked : BeatmapOnlineStatus.None));
            });
        }

        [Test]
        public void TestMissingChecksumDoesNotEraseVerifiedMetadata([Values] bool previouslyVerified)
        {
            RunTestWithRealm((realm, _) =>
            {
                var set = CreateBeatmapSet(CreateRuleset());
                set.OnlineID = 456;
                var beatmap = set.Beatmaps.First();
                beatmap.OnlineID = 123;
                beatmap.MD5Hash = "checksum";
                beatmap.OnlineMD5Hash = previouslyVerified ? "checksum" : string.Empty;
                beatmap.Status = previouslyVerified ? BeatmapOnlineStatus.Ranked : BeatmapOnlineStatus.None;
                beatmap.LastOnlineUpdate = previouslyVerified ? DateTimeOffset.UtcNow : null;
                var originalUpdate = beatmap.LastOnlineUpdate;
                var id = beatmap.ID;
                realm.Write(r => r.Add(set));

                realm.Write(r => RealmPopulatingOnlineLookupSource.UpdateRealmBeatmapSet(r, new APIBeatmapSet
                {
                    OnlineID = 456,
                    Beatmaps = new[] { new APIBeatmap { OnlineID = 123, Status = BeatmapOnlineStatus.Ranked } }
                }));

                var stored = realm.Run(r => r.Find<BeatmapInfo>(id)!.Detach());
                Assert.That(stored.OnlineMD5Hash, Is.EqualTo(previouslyVerified ? "checksum" : string.Empty));
                Assert.That(stored.LastOnlineUpdate, Is.EqualTo(originalUpdate));
                Assert.That(stored.Status, Is.EqualTo(previouslyVerified ? BeatmapOnlineStatus.Ranked : BeatmapOnlineStatus.None));
            });
        }

        [Test]
        public void TestLocallyModifiedBeatmapIsNotRecovered()
        {
            RunTestWithRealm((realm, _) =>
            {
                using var api = new DummyAPIAccess { HandleRequest = _ => throw new AssertionException("Unexpected lookup") };
                var beatmap = new BeatmapInfo { MD5Hash = "checksum", Status = BeatmapOnlineStatus.LocallyModified };
                Assert.That(BeatmapOnlineInfoRecovery.TryRecover(beatmap, api, realm), Is.False);
                Assert.That(beatmap.OnlineID, Is.EqualTo(-1));
            });
        }
    }
}
