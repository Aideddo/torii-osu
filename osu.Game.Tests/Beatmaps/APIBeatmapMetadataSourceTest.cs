// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Net;
using Moq;
using NUnit.Framework;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class APIBeatmapMetadataSourceTest
    {
        [TestCase(HttpStatusCode.InternalServerError)]
        [TestCase(HttpStatusCode.BadGateway)]
        [TestCase(HttpStatusCode.TooManyRequests)]
        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.NotFound)]
        public void TestFailedLookupPreservesOnlineInfo(HttpStatusCode statusCode)
        {
            verifyFailurePreservesOnlineInfo(new APIException("lookup failed", null, statusCode));
        }

        [Test]
        public void TestTimeoutPreservesOnlineInfo() => verifyFailurePreservesOnlineInfo(new TimeoutException());

        [Test]
        public void TestCancellationPreservesOnlineInfo() => verifyFailurePreservesOnlineInfo(new OperationCanceledException());

        private static void verifyFailurePreservesOnlineInfo(Exception exception)
        {
            using var api = new DummyAPIAccess
            {
                HandleRequest = request =>
                {
                    request.TriggerFailure(exception);
                    return true;
                }
            };
            using var source = new APIBeatmapMetadataSource(api);
            var localSource = new Mock<IOnlineBeatmapMetadataSource>();
            using var lookup = new BeatmapUpdaterMetadataLookup(source, localSource.Object);
            var updated = DateTimeOffset.UtcNow;
            var beatmap = new BeatmapInfo
            {
                OnlineID = 123,
                MD5Hash = "checksum",
                OnlineMD5Hash = "checksum",
                Status = BeatmapOnlineStatus.Ranked,
                LastOnlineUpdate = updated,
            };
            var set = new BeatmapSetInfo(beatmap.Yield()) { OnlineID = 456, Status = BeatmapOnlineStatus.Ranked };
            beatmap.BeatmapSet = set;

            lookup.Update(set, preferOnlineFetch: true);

            Assert.That(beatmap.OnlineID, Is.EqualTo(123));
            Assert.That(beatmap.Status, Is.EqualTo(BeatmapOnlineStatus.Ranked));
            Assert.That(beatmap.OnlineMD5Hash, Is.EqualTo("checksum"));
            Assert.That(beatmap.LastOnlineUpdate, Is.EqualTo(updated));
            Assert.That(set.Status, Is.EqualTo(BeatmapOnlineStatus.Ranked));
        }

        [Test]
        public void TestSuccessfulLookupStillUpdatesMetadata()
        {
            using var api = new DummyAPIAccess
            {
                HandleRequest = request =>
                {
                    ((GetBeatmapRequest)request).TriggerSuccess(new APIBeatmap
                    {
                        OnlineID = 123,
                        OnlineBeatmapSetID = 456,
                        Status = BeatmapOnlineStatus.Ranked,
                        Checksum = "checksum",
                    });
                    return true;
                }
            };
            using var source = new APIBeatmapMetadataSource(api);
            var beatmap = new BeatmapInfo { BeatmapSet = new BeatmapSetInfo() };

            Assert.That(source.TryLookup(beatmap, out var metadata), Is.True);
            Assert.That(metadata!.BeatmapID, Is.EqualTo(123));
            Assert.That(metadata.BeatmapSetID, Is.EqualTo(456));
            Assert.That(metadata.MD5Hash, Is.EqualTo("checksum"));
        }
    }
}
