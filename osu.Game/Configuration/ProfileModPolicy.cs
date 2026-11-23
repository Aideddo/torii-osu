// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Configuration
{
    public class ProfileModPolicy
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("ruleset_id")]
        public int RulesetId { get; set; }

        [JsonProperty("required")]
        public APIMod[] Required { get; set; } = Array.Empty<APIMod>();

        [JsonProperty("optional")]
        public string[] Optional { get; set; } = Array.Empty<string>();

        [JsonProperty("allow_other_mods")]
        public bool AllowOtherMods { get; set; }

        public bool IsRequired(Mod mod) => Required.Any(m => m.Acronym == mod.Acronym);

        public static bool AllowsRateSettings(Mod mod) => mod is ModDoubleTime or ModHalfTime;

        public Mod[] CreateRequired(Ruleset ruleset) => Required.Select(m => m.ToMod(ruleset)).ToArray();

        public bool AllowsOptional(Mod mod, Ruleset ruleset) =>
            !IsRequired(mod) && (mod is ModNoFail || AllowOtherMods || Optional.Contains(mod.Acronym))
                            && Compatible(CreateRequired(ruleset).Append(mod));

        public static bool Compatible(IEnumerable<Mod> mods)
        {
            var selection = mods.ToArray();
            return ModUtils.CheckCompatibleSet(selection) && !(selection.Any(m => m is ModPitchAdjust)
                && selection.Any(m => m is ModDoubleTime dt && dt.AdjustPitch.Value || m is ModHalfTime ht && ht.AdjustPitch.Value));
        }

        public IReadOnlyList<Mod> Enforce(IReadOnlyList<Mod> selection, Ruleset ruleset)
        {
            var result = CreateRequired(ruleset).ToList();
            foreach (var required in result.Where(AllowsRateSettings))
            {
                var selected = selection.FirstOrDefault(m => m.Acronym == required.Acronym);
                if (required is ModDoubleTime dt && selected is ModDoubleTime selectedDt)
                {
                    dt.SpeedChange.Value = selectedDt.SpeedChange.Value;
                    dt.AdjustPitch.Value = selectedDt.AdjustPitch.Value && !result.Any(m => m is ModPitchAdjust);
                }
                else if (required is ModHalfTime ht && selected is ModHalfTime selectedHt)
                {
                    ht.SpeedChange.Value = selectedHt.SpeedChange.Value;
                    ht.AdjustPitch.Value = selectedHt.AdjustPitch.Value && !result.Any(m => m is ModPitchAdjust);
                }
            }
            foreach (var mod in selection.Where(m => AllowsOptional(m, ruleset)))
            {
                if (Compatible(result.Append(mod)))
                    result.Add(mod);
            }
            return result;
        }

        public static string Describe(IEnumerable<APIMod> mods) => string.Join(", ", mods.Select(mod =>
        {
            if (mod.Acronym != "DA" || mod.Settings.Count == 0)
                return mod.ToString();
            return string.Join(" ", mod.Settings.Select(s => $"{s.Key switch
            {
                "circle_size" => "CS",
                "approach_rate" => "AR",
                "overall_difficulty" => "OD",
                "drain_rate" => "HP",
                _ => s.Key + " ",
            }}{Convert.ToString(s.Value, CultureInfo.InvariantCulture)}"));
        }));
    }
}
