// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace osu.Game.Beatmaps.Drawables
{
    [LongRunningLoad]
    public partial class OnlineBeatmapSetCover : Sprite
    {
        private readonly IBeatmapSetOnlineInfo set;
        private readonly BeatmapSetCoverType type;

        public OnlineBeatmapSetCover(IBeatmapSetOnlineInfo set, BeatmapSetCoverType type = BeatmapSetCoverType.Cover)
        {
            ArgumentNullException.ThrowIfNull(set);

            this.set = set;
            this.type = type;
        }

        [Resolved(canBeNull: true)]
        private BeatmapManager beatmaps { get; set; }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures)
        {
            string resource = null;

            switch (type)
            {
                case BeatmapSetCoverType.Cover:
                    resource = set.Covers.Cover;
                    break;

                case BeatmapSetCoverType.Card:
                    resource = set.Covers.Card;
                    break;

                case BeatmapSetCoverType.List:
                    resource = set.Covers.List;
                    break;
            }

            if (resource != null)
                Texture = textures.Get(resource);

            if (Texture == null && beatmaps != null)
            {
                int onlineId = (set as IBeatmapSetInfo)?.OnlineID ?? 0;
                var localSet = onlineId > 0
                    ? beatmaps.QueryBeatmapSet(s => s.OnlineID == onlineId && !s.DeletePending)
                    : null;

                Texture = localSet?.PerformRead(s =>
                {
                    var first = s.Beatmaps.FirstOrDefault();
                    if (first == null) return null;

                    var working = beatmaps.GetWorkingBeatmap(first);
                    return working.GetPanelBackground() ?? working.GetBackground();
                });
            }
        }
    }

    public enum BeatmapSetCoverType
    {
        Cover,
        Card,
        List,
    }
}
