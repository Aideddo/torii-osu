// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.API.Requests
{
    public class GetToriiSubProfilesRequest : APIRequest<APIToriiSubProfilesList>
    {
        protected override string Target => @"torii/sub-profiles";
    }

    public class CreateToriiSubProfileRequest : APIRequest<APISwitchSubProfileResponse>
    {
        private readonly string name;
        private readonly string tag;
        private readonly string restrictionMode;
        private readonly List<string> requiredMods;
        private readonly List<string> allowedMods;

        public CreateToriiSubProfileRequest(string name, string tag, string restrictionMode = "free", List<string>? requiredMods = null, List<string>? allowedMods = null)
        {
            this.name = name;
            this.tag = tag;
            this.restrictionMode = restrictionMode;
            this.requiredMods = requiredMods ?? new List<string>();
            this.allowedMods = allowedMods ?? new List<string>();
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Method = HttpMethod.Post;
            req.AddRaw(JsonConvert.SerializeObject(new
            {
                name = name,
                tag = tag,
                restriction_mode = restrictionMode,
                required_mods = requiredMods,
                allowed_mods = allowedMods,
            }));
            return req;
        }

        protected override string Target => @"torii/sub-profiles";
    }

    public class SwitchToriiSubProfileRequest : APIRequest<APISwitchSubProfileResponse>
    {
        private readonly int profileId;

        public SwitchToriiSubProfileRequest(int profileId)
        {
            this.profileId = profileId;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Method = HttpMethod.Post;
            return req;
        }

        protected override string Target => $@"torii/sub-profiles/{profileId}/switch";
    }

    public class SwitchToMasterSubProfileRequest : APIRequest<APISwitchSubProfileResponse>
    {
        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Method = HttpMethod.Post;
            return req;
        }

        protected override string Target => @"torii/sub-profiles/switch-master";
    }
}
