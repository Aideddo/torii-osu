// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.IO.Network;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// Torii: downloads what the player of a score saved along with its replay (see <see cref="Scoring.ReplayExtrasFile"/>).
    /// Fails (404) for a score which has none.
    /// </summary>
    public class GetReplayExtrasRequest : APIRequest
    {
        /// <summary>
        /// The extras file, once the request has succeeded.
        /// </summary>
        public byte[]? Extras { get; private set; }

        public long ScoreId => scoreId;

        private readonly long scoreId;

        /// <param name="scoreId">The online ID of the score.</param>
        public GetReplayExtrasRequest(long scoreId)
        {
            this.scoreId = scoreId;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Finished += () => Extras = req.GetResponseData();
            return req;
        }

        /// <summary>
        /// Completes the request with <paramref name="extras"/> as what was downloaded, without asking a server.
        /// </summary>
        internal void TriggerSuccess(byte[] extras)
        {
            Extras = extras;
            TriggerSuccess();
        }

        protected override string Target => $@"private/score/{scoreId}/extras";

        // Torii's own endpoints are under /api/private, not /api/v2.
        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/{Target}";
    }
}
