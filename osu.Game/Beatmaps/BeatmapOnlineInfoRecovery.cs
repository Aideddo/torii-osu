// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Online.API;

namespace osu.Game.Beatmaps
{
    internal static class BeatmapOnlineInfoRecovery
    {
        public static void RefreshFromRealm(BeatmapInfo beatmap, RealmAccess realm)
        {
            if (beatmap.IsManaged || beatmap.Status == BeatmapOnlineStatus.LocallyModified)
                return;

            realm.Run(r =>
            {
                var stored = r.Find<BeatmapInfo>(beatmap.ID);
                if (stored == null || stored.OnlineID <= 0 || stored.MD5Hash != beatmap.MD5Hash
                    || stored.Status == BeatmapOnlineStatus.LocallyModified)
                    return;

                beatmap.OnlineID = stored.OnlineID;
                beatmap.OnlineMD5Hash = stored.OnlineMD5Hash;
                beatmap.LastOnlineUpdate = stored.LastOnlineUpdate;
                beatmap.Status = stored.Status;

                if (beatmap.BeatmapSet != null && stored.BeatmapSet != null)
                    beatmap.BeatmapSet.OnlineID = stored.BeatmapSet.OnlineID;
            });
        }

        public static bool TryRecover(BeatmapInfo beatmap, IAPIProvider api, RealmAccess realm)
        {
            RefreshFromRealm(beatmap, realm);

            if (beatmap.Status == BeatmapOnlineStatus.LocallyModified)
                return false;

            if (beatmap.OnlineID > 0)
                return true;

            if (beatmap.IsManaged || string.IsNullOrEmpty(beatmap.MD5Hash))
                return false;

            using var source = new APIBeatmapMetadataSource(api);
            if (!source.TryLookup(beatmap, out var metadata) || metadata == null || metadata.BeatmapID <= 0
                || metadata.BeatmapSetID <= 0 || metadata.MD5Hash != beatmap.MD5Hash)
                return false;

            // nunca recuperar por filename: podria ser otra version de la dificultad.
            apply(beatmap, metadata);
            try
            {
                realm.Write(r =>
                {
                    var stored = r.Find<BeatmapInfo>(beatmap.ID);
                    if (stored != null && stored.OnlineID <= 0 && stored.MD5Hash == metadata.MD5Hash
                        && stored.Status != BeatmapOnlineStatus.LocallyModified)
                        apply(stored, metadata);
                });
            }
            catch (Exception e)
            {
                Logger.Log($"Could not persist recovered metadata for {beatmap}: {e.Message}", LoggingTarget.Database);
            }

            Logger.Log($"Recovered online metadata for {beatmap} ({metadata.BeatmapID}).", LoggingTarget.Database);
            return true;
        }

        private static void apply(BeatmapInfo beatmap, OnlineBeatmapMetadata metadata)
        {
            beatmap.OnlineID = metadata.BeatmapID;
            beatmap.OnlineMD5Hash = metadata.MD5Hash;
            beatmap.LastOnlineUpdate = metadata.LastUpdated;
            if (beatmap.MatchesOnlineVersion)
                beatmap.Status = metadata.BeatmapStatus;

            if (beatmap.BeatmapSet != null && metadata.BeatmapSetID > 0)
                beatmap.BeatmapSet.OnlineID = metadata.BeatmapSetID;
        }
    }
}
