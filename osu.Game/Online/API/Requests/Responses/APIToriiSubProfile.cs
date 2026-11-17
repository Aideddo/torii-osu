// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Responses
{
    public class APIToriiSubProfile
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("profile_user_id")]
        public int ProfileUserId { get; set; }

        [JsonProperty("master_user_id")]
        public int MasterUserId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("restriction_mode")]
        public string RestrictionMode { get; set; } = "free";

        [JsonProperty("ruleset_id")]
        public int RulesetId { get; set; }

        [JsonProperty("mod_policy")]
        public osu.Game.Configuration.ProfileModPolicy? ModPolicy { get; set; }

        [JsonProperty("required_mods")]
        public List<string> RequiredMods { get; set; } = new List<string>();

        [JsonProperty("allowed_mods")]
        public List<string> AllowedMods { get; set; } = new List<string>();

        [JsonProperty("disallowed_mods")]
        public List<string> DisallowedMods { get; set; } = new List<string>();

        [JsonProperty("is_master")]
        public bool IsMaster { get; set; }

        [JsonProperty("is_active")]
        public bool IsActive { get; set; }

        [JsonProperty("pp")]
        public double PP { get; set; }

        [JsonProperty("global_rank")]
        public int GlobalRank { get; set; }

        [JsonProperty("accuracy")]
        public double Accuracy { get; set; }

        [JsonProperty("play_count")]
        public int PlayCount { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; } = string.Empty;
    }

    public class APIToriiSubProfilesList
    {
        [JsonProperty("master_user_id")]
        public int MasterUserId { get; set; }

        [JsonProperty("active_profile_user_id")]
        public int ActiveProfileUserId { get; set; }

        [JsonProperty("profiles")]
        public List<APIToriiSubProfile> Profiles { get; set; } = new List<APIToriiSubProfile>();

        [JsonProperty("total_profiles")]
        public int TotalProfiles { get; set; }

        [JsonProperty("user_points")]
        public int UserPoints { get; set; }

        [JsonProperty("next_slot_cost")]
        public int NextSlotCost { get; set; }
    }

    public class APISwitchSubProfileResponse
    {
        [JsonProperty("access_token", Required = Required.Always)]
        public string AccessToken { get; set; } = string.Empty;

        [JsonProperty("refresh_token", Required = Required.Always)]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonProperty("expires_in", Required = Required.Always)]
        public long ExpiresIn { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; } = "Bearer";

        [JsonProperty("profile_user_id")]
        public int ProfileUserId { get; set; }

        [JsonProperty("master_user_id")]
        public int MasterUserId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonProperty("restriction_mode")]
        public string RestrictionMode { get; set; } = "free";

        [System.Runtime.Serialization.OnDeserialized]
        private void validateSession(System.Runtime.Serialization.StreamingContext context)
        {
            if (string.IsNullOrWhiteSpace(AccessToken) || string.IsNullOrWhiteSpace(RefreshToken) || ExpiresIn <= 0)
                throw new JsonSerializationException("The profile switch response contains an invalid session.");
        }
    }
}
