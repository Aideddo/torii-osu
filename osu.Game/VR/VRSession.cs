// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: statistics of one run of the game in VR, from launch to exit. Kept by <see cref="VRSessionStore"/>.
    /// </summary>
    public class VRSession
    {
        [JsonProperty("id")]
        public Guid ID { get; set; }

        /// <summary>
        /// When the game was launched.
        /// </summary>
        [JsonProperty("start_time")]
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// When these statistics were last written, which is close to when the session ended (or the last time the controllers moved).
        /// </summary>
        [JsonProperty("last_updated")]
        public DateTimeOffset LastUpdated { get; set; }

        /// <summary>
        /// How far the left controller moved over the whole session, in metres.
        /// </summary>
        [JsonProperty("left_distance")]
        public double LeftDistance { get; set; }

        /// <summary>
        /// How far the right controller moved over the whole session, in metres.
        /// </summary>
        [JsonProperty("right_distance")]
        public double RightDistance { get; set; }

        /// <summary>
        /// The part of <see cref="LeftDistance"/> moved during gameplay (breaks and pauses included).
        /// </summary>
        [JsonProperty("left_gameplay_distance")]
        public double LeftGameplayDistance { get; set; }

        /// <summary>
        /// The part of <see cref="RightDistance"/> moved during gameplay (breaks and pauses included).
        /// </summary>
        [JsonProperty("right_gameplay_distance")]
        public double RightGameplayDistance { get; set; }

        [JsonConstructor]
        public VRSession()
        {
        }

        public VRSession(Guid id, DateTimeOffset startTime)
        {
            ID = id;
            StartTime = LastUpdated = startTime;
        }
    }
}
