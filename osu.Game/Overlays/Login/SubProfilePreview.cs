#if DEBUG
using System;
using System.Linq;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.Login
{
    internal static class SubProfilePreview
    {
        public static bool Enabled => Environment.GetEnvironmentVariable("TORII_SUBPROFILE_PREVIEW") == "1";

        private static readonly APIToriiSubProfilesList data = new APIToriiSubProfilesList
        {
            UserPoints = 5000,
            NextSlotCost = 5000,
            Profiles =
            {
                new APIToriiSubProfile { Id = 0, Name = "Main", Tag = "MAIN", IsMaster = true, IsActive = true, RestrictionMode = "free" },
                new APIToriiSubProfile { Id = 1, Name = "DT practice", Tag = "DT", RestrictionMode = "dt_only" },
            },
        };

        public static APIToriiSubProfilesList GetProfiles(int? rulesetId = null) => rulesetId == null ? data : new APIToriiSubProfilesList
        {
            UserPoints = data.UserPoints,
            NextSlotCost = data.NextSlotCost,
            Profiles = data.Profiles.Where(p => p.IsMaster || p.RulesetId == rulesetId).ToList(),
        };

        public static void Switch(int id)
        {
            foreach (var profile in data.Profiles)
                profile.IsActive = profile.Id == id;
        }

        public static void Create(string name, string tag, string restrictionMode, osu.Game.Configuration.ProfileModPolicy? policy = null)
        {
            data.UserPoints -= data.NextSlotCost;
            var profile = new APIToriiSubProfile
            {
                Id = data.Profiles.Max(p => p.Id) + 1,
                Name = name,
                Tag = tag.ToUpperInvariant(),
                RestrictionMode = restrictionMode,
                ModPolicy = policy,
                RulesetId = policy?.RulesetId ?? 0,
            };
            data.Profiles.Add(profile);
            Switch(profile.Id);
        }
    }
}
#endif
