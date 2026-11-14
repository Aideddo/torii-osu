// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Http;
using osu.Framework.IO.Network;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// Torii: uploads what was saved along with the replay of one of the user's own scores (see
    /// <see cref="Scoring.ReplayExtrasFile"/>), for those who download the replay to get as well.
    /// </summary>
    public class UploadReplayExtrasRequest : APIRequest
    {
        public long ScoreId => scoreId;
        public byte[] Extras => extras;

        private readonly long scoreId;
        private readonly byte[] extras;

        /// <param name="scoreId">The online ID of the score.</param>
        /// <param name="extras">The extras file, whole.</param>
        public UploadReplayExtrasRequest(long scoreId, byte[] extras)
        {
            this.scoreId = scoreId;
            this.extras = extras;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Method = HttpMethod.Put;
            req.AddFile(@"content", extras, @"replay.osrx");
            return req;
        }

        protected override string Target => $@"private/score/{scoreId}/extras";

        // Torii's own endpoints are under /api/private, not /api/v2.
        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/{Target}";
    }
}
