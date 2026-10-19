// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.BeatmapSet
{
    /// <summary>
    /// torii: este beatmap o set está en blacklist y no otorga Performance Points (PP).
    /// </summary>
    public partial class BlacklistedBeatmapBadge : BeatmapBadge
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            BadgeText = @"Blacklisted";
            BadgeColour = Colour4.FromHex(@"ff3b3b");
        }

        public static bool ShouldShowFor(APIBeatmapSet? beatmapSet) =>
            beatmapSet?.IsBlacklisted == true || beatmapSet?.HasBlacklistedDifficulties == true;
    }
}
