// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Torii: the file of extras kept next to a replay (see <see cref="ReplayExtrasStore"/>): any number of sections, each tagged with
    /// the <see cref="ReplayExtrasType"/> of what it carries, so a reader can find the kinds it handles and pass over the rest.
    /// </summary>
    /// <remarks>
    /// The file is: the four bytes "OSRX"; the version of this layout (a byte); then sections to the end of the file, each being its
    /// type and the length of its data in bytes (two ints, little endian) and the data, which is in a format of the type's own.
    /// </remarks>
    public class ReplayExtrasFile
    {
        public const byte VERSION = 1;

        private const int section_header_size = 8;

        private static readonly byte[] magic = "OSRX"u8.ToArray();

        /// <summary>
        /// The sections, in file order. At most one per type. Types this build doesn't know are kept, so that saving the file again
        /// doesn't lose them.
        /// </summary>
        public List<Section> Sections { get; } = new List<Section>();

        /// <summary>
        /// The data of the section of <paramref name="type"/>, or null if there is none.
        /// </summary>
        public byte[]? Get(ReplayExtrasType type) => Sections.FirstOrDefault(s => s.Type == type)?.Data;

        /// <summary>
        /// Adds a section, in place of any of the same type.
        /// </summary>
        public void Set(ReplayExtrasType type, byte[] data)
        {
            var section = new Section(type, data);
            int existing = Sections.FindIndex(s => s.Type == type);

            if (existing >= 0)
                Sections[existing] = section;
            else
                Sections.Add(section);
        }

        public void Encode(Stream stream)
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(magic);
                writer.Write(VERSION);

                foreach (var section in Sections)
                {
                    writer.Write((int)section.Type);
                    writer.Write(section.Data.Length);
                    writer.Write(section.Data);
                }
            }
        }

        /// <param name="stream">A seekable stream.</param>
        /// <exception cref="InvalidDataException">If the stream doesn't hold an extras file this version can read.</exception>
        public static ReplayExtrasFile Decode(Stream stream)
        {
            var file = new ReplayExtrasFile();

            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                while (readSectionHeader(reader, file.Sections.Count == 0) is var (type, length))
                {
                    byte[] data = reader.ReadBytes(length);

                    // The first of a type counts, as for Get().
                    if (file.Sections.All(s => s.Type != type))
                        file.Sections.Add(new Section(type, data));
                }
            }

            return file;
        }

        /// <summary>
        /// Reads which types a file carries, without reading their data.
        /// </summary>
        /// <param name="stream">A seekable stream.</param>
        /// <exception cref="InvalidDataException">If the stream doesn't hold an extras file this version can read.</exception>
        public static List<ReplayExtrasType> DecodeTypes(Stream stream)
        {
            var types = new List<ReplayExtrasType>();
            bool first = true;

            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                while (readSectionHeader(reader, first) is var (type, length))
                {
                    first = false;
                    stream.Seek(length, SeekOrigin.Current);

                    if (!types.Contains(type))
                        types.Add(type);
                }
            }

            return types;
        }

        /// <summary>
        /// Reads the header of the next section (after the header of the file, for the first).
        /// </summary>
        /// <returns>The section's type and the length of the data that follows, or null at the end of the file.</returns>
        private static (ReplayExtrasType type, int length)? readSectionHeader(BinaryReader reader, bool first)
        {
            var stream = reader.BaseStream;

            if (first)
            {
                if (!reader.ReadBytes(magic.Length).AsSpan().SequenceEqual(magic))
                    throw new InvalidDataException("Not a replay extras file.");

                if (stream.Position == stream.Length)
                    throw new InvalidDataException("Replay extras file is cut short.");

                byte version = reader.ReadByte();
                if (version != VERSION)
                    throw new InvalidDataException($"Replay extras file of version {version} can't be read (this build reads version {VERSION}).");
            }

            long remaining = stream.Length - stream.Position;

            if (remaining == 0)
                return null;

            if (remaining < section_header_size)
                throw new InvalidDataException("Replay extras file is cut short.");

            var type = (ReplayExtrasType)reader.ReadInt32();
            int length = reader.ReadInt32();

            if (length < 0 || length > remaining - section_header_size)
                throw new InvalidDataException("Replay extras file is cut short or damaged.");

            return (type, length);
        }

        /// <summary>
        /// One kind of extras.
        /// </summary>
        /// <param name="Type">What the data is. May be a value this build doesn't know.</param>
        /// <param name="Data">The data, in the format of the type.</param>
        public record Section(ReplayExtrasType Type, byte[] Data);
    }
}
