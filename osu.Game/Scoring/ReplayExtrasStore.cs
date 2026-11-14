// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Torii: keeps what Torii saves about a play in addition to its replay: one <see cref="ReplayExtrasFile"/> per replay, named
    /// after the replay's hash, in the "torii/replay-extras" folder of the game's storage. A file carries any number of kinds of
    /// extras (<see cref="ReplayExtrasType"/>); a score asks for the kinds it can make use of.
    /// </summary>
    /// <remarks>
    /// Deliberately not files of the score in the game's realm database: that one may be shared with an osu!(lazer) install, which
    /// expects a score to have exactly one file.
    /// </remarks>
    public class ReplayExtrasStore
    {
        public const string EXTENSION = @".osrx";

        private readonly Storage storage;

        // Adding a section rewrites the whole file.
        private readonly object writeLock = new object();

        public ReplayExtrasStore(Storage storage)
        {
            this.storage = storage.GetStorageForDirectory(@"torii").GetStorageForDirectory(@"replay-extras");
        }

        /// <summary>
        /// Whether anything is saved for the replay with <paramref name="scoreHash"/>.
        /// </summary>
        public bool Exists(string? scoreHash) => getFilename(scoreHash) is string filename && storage.Exists(filename);

        /// <summary>
        /// Removes all that is saved for the replay with <paramref name="scoreHash"/>.
        /// </summary>
        public void Delete(string? scoreHash)
        {
            if (getFilename(scoreHash) is not string filename)
                return;

            lock (writeLock)
            {
                if (storage.Exists(filename))
                    storage.Delete(filename);
            }
        }

        /// <summary>
        /// Writes all that is saved for the replay with <paramref name="scoreHash"/> to <paramref name="destination"/>, as the file it's
        /// kept in. Nothing is written if there is nothing saved.
        /// </summary>
        public void CopyTo(string? scoreHash, Stream destination)
        {
            if (getFilename(scoreHash) is not string filename || !storage.Exists(filename))
                return;

            using (var stream = storage.GetStream(filename, FileAccess.Read, FileMode.Open))
                stream.CopyTo(destination);
        }

        /// <summary>
        /// Takes in an extras file from elsewhere (see <see cref="CopyTo"/>) for the replay with <paramref name="scoreHash"/>. Each kind
        /// of extras it has replaces what is saved of that kind; other kinds are kept.
        /// </summary>
        /// <param name="scoreHash">The hash of the replay (<see cref="ScoreInfo.Hash"/>).</param>
        /// <param name="source">The file. Read to its end.</param>
        /// <returns>Whether anything was taken in: false for a file which can't be read, or which has nothing in it.</returns>
        public bool Import(string scoreHash, Stream source)
        {
            string filename = getFilename(scoreHash) ?? throw new ArgumentException("Not the hash of a replay.", nameof(scoreHash));

            ReplayExtrasFile imported;

            try
            {
                // (Decoding needs to seek.)
                var memory = new MemoryStream();
                source.CopyTo(memory);
                memory.Position = 0;

                imported = ReplayExtrasFile.Decode(memory);
            }
            catch (Exception e)
            {
                Logger.Log($"Extras offered for replay {scoreHash} can't be read: {e.Message}");
                return false;
            }

            if (imported.Sections.Count == 0)
                return false;

            lock (writeLock)
            {
                var file = read(scoreHash, ReplayExtrasFile.Decode) ?? new ReplayExtrasFile();

                foreach (var section in imported.Sections)
                    file.Set(section.Type, section.Data);

                using (var stream = storage.CreateFileSafely(filename))
                    file.Encode(stream);
            }

            return true;
        }

        /// <summary>
        /// The kinds of extras saved for <paramref name="score"/>. May include kinds this build doesn't know.
        /// </summary>
        public IReadOnlyList<ReplayExtrasType> GetTypes(ScoreInfo score) => GetTypes(score.Hash);

        /// <summary>
        /// The kinds of extras saved for the replay with <paramref name="scoreHash"/>. May include kinds this build doesn't know.
        /// </summary>
        public IReadOnlyList<ReplayExtrasType> GetTypes(string? scoreHash)
            => read(scoreHash, ReplayExtrasFile.DecodeTypes) ?? (IReadOnlyList<ReplayExtrasType>)Array.Empty<ReplayExtrasType>();

        /// <summary>
        /// Whether extras of <paramref name="type"/> are saved for <paramref name="score"/>.
        /// </summary>
        public bool Has(ScoreInfo score, ReplayExtrasType type) => Has(score.Hash, type);

        /// <summary>
        /// Whether extras of <paramref name="type"/> are saved for the replay with <paramref name="scoreHash"/>.
        /// </summary>
        public bool Has(string? scoreHash, ReplayExtrasType type) => GetTypes(scoreHash).Contains(type);

        /// <summary>
        /// Reads the extras of <paramref name="type"/> saved for the replay with <paramref name="scoreHash"/>.
        /// </summary>
        /// <returns>The data, in the format of the type; or null if there is none or the file can't be read.</returns>
        public byte[]? Read(string? scoreHash, ReplayExtrasType type) => read(scoreHash, ReplayExtrasFile.Decode)?.Get(type);

        /// <summary>
        /// Saves extras of <paramref name="type"/> for the replay with <paramref name="scoreHash"/>, in place of any of that type.
        /// Extras of other types are kept.
        /// </summary>
        /// <param name="scoreHash">The hash of the replay (<see cref="ScoreInfo.Hash"/>).</param>
        /// <param name="type">What <paramref name="data"/> is.</param>
        /// <param name="data">The data, in the format of the type.</param>
        public void Write(string scoreHash, ReplayExtrasType type, byte[] data)
        {
            string filename = getFilename(scoreHash) ?? throw new ArgumentException("Not the hash of a replay.", nameof(scoreHash));

            lock (writeLock)
            {
                // A file which can't be read is replaced.
                var file = read(scoreHash, ReplayExtrasFile.Decode) ?? new ReplayExtrasFile();
                file.Set(type, data);

                // Written to a temporary file first: the game can be killed at any time (a headset does that without notice).
                using (var stream = storage.CreateFileSafely(filename))
                    file.Encode(stream);
            }
        }

        private T? read<T>(string? scoreHash, Func<Stream, T> decode)
            where T : class
        {
            if (getFilename(scoreHash) is not string filename || !storage.Exists(filename))
                return null;

            try
            {
                using (var stream = storage.GetStream(filename, FileAccess.Read, FileMode.Open))
                    return decode(stream);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to read replay extras ({filename}).");
                return null;
            }
        }

        // Hashes are hexadecimal; anything else isn't trusted as a file name.
        private static string? getFilename(string? scoreHash)
            => !string.IsNullOrEmpty(scoreHash) && scoreHash.All(char.IsAsciiLetterOrDigit) ? scoreHash + EXTENSION : null;
    }
}
