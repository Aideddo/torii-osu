// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: keeps the statistics of VR sessions (<see cref="VRSession"/>) in a file of their own, in the "torii" folder of the game's
    /// storage along with Torii's other data.
    /// </summary>
    /// <remarks>
    /// Deliberately not in the game's realm database: that one may be shared with an osu!(lazer) install (the storage can point at its
    /// folder), which can no longer open it once a schema version of ours has been applied to it.
    /// </remarks>
    public class VRSessionStore
    {
        public const string FILENAME = @"vr-sessions.json";

        private readonly Storage storage;
        private readonly object fileLock = new object();

        public VRSessionStore(Storage storage)
        {
            this.storage = storage.GetStorageForDirectory(@"torii");
        }

        /// <summary>
        /// Reads all saved sessions. A file which can't be read is set aside, and counts as no sessions.
        /// </summary>
        public List<VRSession> Load()
        {
            lock (fileLock)
            {
                if (!storage.Exists(FILENAME))
                    return new List<VRSession>();

                try
                {
                    using (var stream = storage.GetStream(FILENAME, FileAccess.Read, FileMode.Open))
                    using (var reader = new StreamReader(stream))
                        return JsonConvert.DeserializeObject<List<VRSession>>(reader.ReadToEnd()) ?? new List<VRSession>();
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Failed to read VR session statistics ({FILENAME}); starting over.");

                    try
                    {
                        string aside = $@"{FILENAME}.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.unreadable";
                        storage.Move(FILENAME, aside);
                    }
                    catch
                    {
                        // Overwritten by the next save then.
                    }

                    return new List<VRSession>();
                }
            }
        }

        /// <summary>
        /// Replaces the saved sessions.
        /// </summary>
        public void Save(IReadOnlyList<VRSession> sessions)
        {
            string json = JsonConvert.SerializeObject(sessions, Formatting.Indented);

            lock (fileLock)
            {
                // Written to a temporary file first: the game can be killed at any time (see VRMovementTracker).
                using (var stream = storage.CreateFileSafely(FILENAME))
                using (var writer = new StreamWriter(stream))
                    writer.Write(json);
            }
        }
    }
}
