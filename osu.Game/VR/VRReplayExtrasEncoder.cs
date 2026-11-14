// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Scoring;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: reads and writes <see cref="VRReplayExtras"/>, as the <see cref="ReplayExtrasType.VRTracking"/> section of a replay's
    /// extras file (<see cref="ReplayExtrasFile"/>).
    /// </summary>
    /// <remarks>
    /// <para>The data is: the format version (a byte); the length of the description in bytes (an int, little endian) and the
    /// description, which is everything but the frames as UTF-8 JSON; the number of frames (an int); how the frames are compressed
    /// (a byte: <see cref="codec_brotli"/> or <see cref="codec_zlib"/>); the frames, compressed.</para>
    /// <para>The frames are a run of channels, each one kind of value for all frames together (values of a kind are alike, and
    /// compress far better side by side). A channel is what it holds (a <see cref="Channel"/>, as a variable-length integer), the
    /// length of its data in bytes (likewise), and the data.</para>
    /// <para>A pose channel holds the poses of one tracked thing for the frames it was tracked in (which those are is in
    /// <see cref="Channel.Flags"/>), as seven kinds of value in turn: its position (x, y, z); the three smaller components of its
    /// rotation; and which component is the largest, left out because it follows from the others (a byte each). All but the last
    /// are whole numbers of the units the description gives, stored as their change from the frame before in a variable-length
    /// integer. A value is only stored as changed once it has moved clearly off what was stored last, so that tracking jitter
    /// doesn't make a still hand look busy.</para>
    /// <para>What can be added without older builds losing anything else: fields in the description (unknown ones are ignored),
    /// channels (unknown ones are passed over; one whose values need to be stored differently is added as a new channel, in
    /// place of the old), bits in the flags, and units (read from the description). Every channel but the times may be missing:
    /// what it holds is then unknown for the play. The version only changes when the above can't be kept.</para>
    /// </remarks>
    public static class VRReplayExtrasEncoder
    {
        public const byte VERSION = 1;

        /// <summary>
        /// The resolution times are stored at, in milliseconds.
        /// </summary>
        public const double TIME_UNIT = 1;

        /// <summary>
        /// The resolution positions are stored at, in metres.
        /// </summary>
        public const float POSITION_UNIT = 0.0005f;

        /// <summary>
        /// The resolution rotations are stored at (each component of the quaternion): about 0.03 degrees, which is less than a
        /// millimetre where a pointer meets a panel two metres away.
        /// </summary>
        public const float ROTATION_UNIT = 1f / 4096;

        /// <summary>
        /// How far a value may be from what's stored for it, in units: it's stored anew once it's further off than this.
        /// (Half a unit would be plain rounding.)
        /// </summary>
        public const float TOLERANCE = 0.8f;

        /// <summary>
        /// What a channel of the frames holds. Saved in files: values never change or get reused.
        /// </summary>
        internal enum Channel
        {
            /// <summary>
            /// The time of each frame, as its change from the frame before.
            /// </summary>
            Times = 1,

            /// <summary>
            /// A byte per frame: what was tracked in it (<see cref="flag_head"/>, <see cref="flag_left"/>, <see cref="flag_right"/>)
            /// and which hand drove the cursor (<see cref="flag_right_active"/>).
            /// </summary>
            Flags = 2,

            HeadPoses = 3,
            LeftGripPoses = 4,
            RightGripPoses = 5,

            /// <summary>
            /// A byte per frame: 0 (released) to 255 (pulled all the way). Likewise the three below.
            /// </summary>
            LeftTrigger = 6,

            LeftSqueeze = 7,
            RightTrigger = 8,
            RightSqueeze = 9,
        }

        private const byte flag_head = 1;
        private const byte flag_left = 2;
        private const byte flag_right = 4;
        private const byte flag_right_active = 8;

        private const byte codec_zlib = 0;
        private const byte codec_brotli = 1;

        /// <summary>
        /// More than any play's frames come to once decompressed; a file claiming more is damaged or isn't one of ours.
        /// </summary>
        private const int max_decompressed_size = 256 * 1024 * 1024;

        public static void Encode(VRReplayExtras extras, Stream stream) => Encode(extras, stream, null);

        /// <param name="extras">What to write.</param>
        /// <param name="stream">Where to.</param>
        /// <param name="otherChannels">Channels to write besides those of <paramref name="extras"/>, as a newer build might.</param>
        internal static void Encode(VRReplayExtras extras, Stream stream, IEnumerable<(int channel, byte[] data)>? otherChannels)
        {
            // The units the values are in are part of the file: a build which stores them finer or coarser needs no new version.
            extras.TimeUnit = extras.TimeUnit > 0 ? extras.TimeUnit : TIME_UNIT;
            extras.PositionUnit = extras.PositionUnit > 0 ? extras.PositionUnit : POSITION_UNIT;
            extras.RotationUnit = extras.RotationUnit > 0 ? extras.RotationUnit : ROTATION_UNIT;

            var frames = extras.Frames;
            byte[] compressed = compress(encodeFrames(extras, otherChannels), out byte codec);

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(VERSION);

                byte[] description = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(extras));
                writer.Write(description.Length);
                writer.Write(description);

                writer.Write(frames.Count);
                writer.Write(codec);
                writer.Write(compressed);
            }
        }

        private static byte[] encodeFrames(VRReplayExtras extras, IEnumerable<(int channel, byte[] data)>? otherChannels)
        {
            var frames = extras.Frames;
            var output = new MemoryStream();

            using (var writer = new BinaryWriter(output))
            {
                void writeChannel(int channel, Action<BinaryWriter> write)
                {
                    var data = new MemoryStream();

                    using (var dataWriter = new BinaryWriter(data, Encoding.UTF8, true))
                        write(dataWriter);

                    writer.Write7BitEncodedInt(channel);
                    writer.Write7BitEncodedInt((int)data.Length);
                    writer.Write(data.GetBuffer(), 0, (int)data.Length);
                }

                writeChannel((int)Channel.Times, w =>
                {
                    long lastTime = 0;

                    foreach (var frame in frames)
                    {
                        long time = (long)Math.Round(frame.Time / extras.TimeUnit);
                        w.Write7BitEncodedInt64(zigzag(time - lastTime));
                        lastTime = time;
                    }
                });

                writeChannel((int)Channel.Flags, w =>
                {
                    foreach (var frame in frames)
                    {
                        byte flags = 0;
                        if (frame.Head != null) flags |= flag_head;
                        if (frame.Left.Grip != null) flags |= flag_left;
                        if (frame.Right.Grip != null) flags |= flag_right;
                        if (frame.ActiveHand == IVRControllers.RIGHT) flags |= flag_right_active;
                        w.Write(flags);
                    }
                });

                writeChannel((int)Channel.HeadPoses, w => writePoses(w, extras, static f => f.Head));
                writeChannel((int)Channel.LeftGripPoses, w => writePoses(w, extras, static f => f.Left.Grip));
                writeChannel((int)Channel.RightGripPoses, w => writePoses(w, extras, static f => f.Right.Grip));

                writeChannel((int)Channel.LeftTrigger, w => { foreach (var frame in frames) w.Write(toByte(frame.Left.Trigger)); });
                writeChannel((int)Channel.LeftSqueeze, w => { foreach (var frame in frames) w.Write(toByte(frame.Left.Squeeze)); });
                writeChannel((int)Channel.RightTrigger, w => { foreach (var frame in frames) w.Write(toByte(frame.Right.Trigger)); });
                writeChannel((int)Channel.RightSqueeze, w => { foreach (var frame in frames) w.Write(toByte(frame.Right.Squeeze)); });

                if (otherChannels != null)
                {
                    foreach (var (channel, data) in otherChannels)
                        writeChannel(channel, w => w.Write(data));
                }
            }

            return output.ToArray();
        }

        /// <summary>
        /// Writes the poses of one tracked thing, for the frames it was tracked in: a kind of value at a time.
        /// </summary>
        private static void writePoses(BinaryWriter writer, VRReplayExtras extras, Func<VRReplayFrame, VRPose?> select)
        {
            var poses = new List<VRPose>(extras.Frames.Count);

            foreach (var frame in extras.Frames)
            {
                if (select(frame) is VRPose pose)
                    poses.Add(pose);
            }

            // For each pose: the three smaller components of its rotation, and which the largest is.
            float[] rotations = new float[poses.Count * 3];
            byte[] largest = new byte[poses.Count];
            int lastLargest = 3;

            Span<float> components = stackalloc float[4];

            for (int i = 0; i < poses.Count; i++)
            {
                var rotation = Quaternion.Normalize(poses[i].Rotation);
                if (!float.IsFinite(rotation.W))
                    rotation = Quaternion.Identity;

                components[0] = rotation.X;
                components[1] = rotation.Y;
                components[2] = rotation.Z;
                components[3] = rotation.W;

                // The one it was last, unless another is clearly larger by now: what's left out shouldn't keep changing.
                int index = lastLargest;

                for (int c = 0; c < 4; c++)
                {
                    if (MathF.Abs(components[c]) > MathF.Abs(components[index]) + 0.05f)
                        index = c;
                }

                lastLargest = index;
                largest[i] = (byte)index;

                // A rotation and its negation are the same rotation: the one whose largest component is positive is stored.
                float sign = components[index] < 0 ? -1 : 1;

                for (int c = 0, n = 0; c < 4; c++)
                {
                    if (c != index)
                        rotations[i * 3 + n++] = components[c] * sign;
                }
            }

            writeValues(writer, poses.Count, i => poses[i].Position.X, extras.PositionUnit);
            writeValues(writer, poses.Count, i => poses[i].Position.Y, extras.PositionUnit);
            writeValues(writer, poses.Count, i => poses[i].Position.Z, extras.PositionUnit);
            writeValues(writer, poses.Count, i => rotations[i * 3], extras.RotationUnit);
            writeValues(writer, poses.Count, i => rotations[i * 3 + 1], extras.RotationUnit);
            writeValues(writer, poses.Count, i => rotations[i * 3 + 2], extras.RotationUnit);
            writer.Write(largest);
        }

        private static void writeValues(BinaryWriter writer, int count, Func<int, float> get, float unit)
        {
            long last = 0;

            for (int i = 0; i < count; i++)
            {
                float value = get(i);

                // Tracking can't be further out than this; keeps a wild value from overflowing. One which isn't a number stays as it was.
                float units = float.IsFinite(value) ? Math.Clamp(value, -100_000, 100_000) / unit : last;

                long stored = i > 0 && MathF.Abs(units - last) <= TOLERANCE ? last : (long)MathF.Round(units);

                writer.Write7BitEncodedInt64(zigzag(stored - last));
                last = stored;
            }
        }

        private static byte[] compress(byte[] data, out byte codec)
        {
            try
            {
                codec = codec_brotli;
                return compress(data, s => new BrotliStream(s, CompressionLevel.SmallestSize, true));
            }
            catch (Exception e) when (e is DllNotFoundException or PlatformNotSupportedException or EntryPointNotFoundException or TypeInitializationException)
            {
                // Not on this platform: larger, but everywhere.
                codec = codec_zlib;
                return compress(data, s => new ZLibStream(s, CompressionLevel.SmallestSize, true));
            }
        }

        private static byte[] compress(byte[] data, Func<Stream, Stream> createCompressor)
        {
            var output = new MemoryStream();

            using (var compressor = createCompressor(output))
                compressor.Write(data);

            return output.ToArray();
        }

        /// <summary>
        /// Saves <paramref name="extras"/> for the replay with <paramref name="scoreHash"/>.
        /// </summary>
        public static void Save(ReplayExtrasStore store, string scoreHash, VRReplayExtras extras)
        {
            extras.ScoreHash = scoreHash;

            var stream = new MemoryStream();
            Encode(extras, stream);
            store.Write(scoreHash, ReplayExtrasType.VRTracking, stream.ToArray());
        }

        /// <summary>
        /// Reads what a play in VR saved for the replay with <paramref name="scoreHash"/>.
        /// </summary>
        /// <param name="store">Where extras are kept.</param>
        /// <param name="scoreHash">The hash of the replay.</param>
        /// <param name="frames">Whether to read <see cref="VRReplayExtras.Frames"/>: the rest is quick to read without.</param>
        /// <returns>The extras, or null if there are none (the play wasn't in VR) or they can't be read.</returns>
        public static VRReplayExtras? Load(ReplayExtrasStore store, string? scoreHash, bool frames = true)
        {
            if (store.Read(scoreHash, ReplayExtrasType.VRTracking) is not byte[] data)
                return null;

            try
            {
                return Decode(new MemoryStream(data), frames);
            }
            catch (InvalidDataException e)
            {
                Logger.Error(e, $"Failed to read VR replay extras of {scoreHash}.");
                return null;
            }
        }

        /// <summary>
        /// Reads what a play in VR saved for the replay with <paramref name="scoreHash"/>, in the background.
        /// </summary>
        /// <param name="store">Where extras are kept.</param>
        /// <param name="scoreHash">The hash of the replay.</param>
        /// <param name="scheduler">Runs <paramref name="loaded"/>, on the thread the extras are wanted on.</param>
        /// <param name="loaded">Given the extras, if there are any which can be read.</param>
        public static void LoadAsync(ReplayExtrasStore store, string? scoreHash, Scheduler scheduler, Action<VRReplayExtras> loaded)
        {
            // Not a saved play (e.g. autoplay), so nothing was saved with it.
            if (string.IsNullOrEmpty(scoreHash))
                return;

            Task.Run(() => Load(store, scoreHash)).ContinueWith(task => scheduler.Add(() =>
            {
                if (task.IsFaulted)
                    Logger.Error(task.Exception, "Failed to load the VR extras of a replay.");
                else if (task.GetResultSafely() is VRReplayExtras extras)
                    loaded(extras);
            }));
        }

        /// <param name="stream">The data of the section.</param>
        /// <param name="readFrames">Whether to read <see cref="VRReplayExtras.Frames"/> (left empty otherwise).</param>
        /// <exception cref="InvalidDataException">If the stream doesn't hold extras this version can read.</exception>
        public static VRReplayExtras Decode(Stream stream, bool readFrames = true)
        {
            VRReplayExtras extras;
            int frameCount;
            byte codec;

            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                try
                {
                    byte version = reader.ReadByte();
                    if (version != VERSION)
                        throw new InvalidDataException($"VR replay extras of version {version} can't be read (this build reads version {VERSION}).");

                    int descriptionLength = reader.ReadInt32();
                    if (descriptionLength < 0 || descriptionLength > 64 * 1024 * 1024)
                        throw new InvalidDataException("VR replay extras are damaged.");

                    byte[] description = reader.ReadBytes(descriptionLength);
                    if (description.Length != descriptionLength)
                        throw new InvalidDataException("VR replay extras are cut short.");

                    try
                    {
                        extras = JsonConvert.DeserializeObject<VRReplayExtras>(Encoding.UTF8.GetString(description))
                                 ?? throw new InvalidDataException("VR replay extras have no description.");
                    }
                    catch (JsonException e)
                    {
                        throw new InvalidDataException("VR replay extras have an unreadable description.", e);
                    }

                    frameCount = reader.ReadInt32();
                    if (frameCount < 0 || frameCount > max_trusted_frames)
                        throw new InvalidDataException("VR replay extras are damaged.");

                    codec = reader.ReadByte();
                }
                catch (EndOfStreamException e)
                {
                    throw new InvalidDataException("VR replay extras are cut short.", e);
                }
            }

            // Written by something which didn't say: as this build writes them.
            if (!(extras.TimeUnit > 0) || !double.IsFinite(extras.TimeUnit)) extras.TimeUnit = TIME_UNIT;
            if (!(extras.PositionUnit > 0) || !float.IsFinite(extras.PositionUnit)) extras.PositionUnit = POSITION_UNIT;
            if (!(extras.RotationUnit > 0) || !float.IsFinite(extras.RotationUnit)) extras.RotationUnit = ROTATION_UNIT;

            if (!readFrames)
                return extras;

            try
            {
                using (Stream decompressor = codec switch
                       {
                           codec_brotli => new BrotliStream(stream, CompressionMode.Decompress, true),
                           codec_zlib => new ZLibStream(stream, CompressionMode.Decompress, true),
                           _ => throw new InvalidDataException($"VR replay extras are compressed in a way this build doesn't know ({codec})."),
                       })
                    extras.Frames = decodeFrames(readChannels(decompressor), extras, frameCount);
            }
            catch (Exception e) when (e is EndOfStreamException or FormatException or InvalidDataException or IOException)
            {
                throw new InvalidDataException("VR replay extras are cut short or damaged.", e);
            }

            return extras;
        }

        /// <summary>
        /// Reads the channels there are, whatever they hold.
        /// </summary>
        private static Dictionary<int, byte[]> readChannels(Stream decompressor)
        {
            var data = new MemoryStream();
            byte[] buffer = new byte[81920];
            int read;

            while ((read = decompressor.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (data.Length + read > max_decompressed_size)
                    throw new InvalidDataException("VR replay extras are damaged.");

                data.Write(buffer, 0, read);
            }

            data.Position = 0;

            var channels = new Dictionary<int, byte[]>();

            using (var reader = new BinaryReader(data))
            {
                while (data.Position < data.Length)
                {
                    int channel = reader.Read7BitEncodedInt();
                    int length = reader.Read7BitEncodedInt();

                    if (length < 0 || length > data.Length - data.Position)
                        throw new InvalidDataException("VR replay extras are damaged.");

                    byte[] content = reader.ReadBytes(length);

                    // The first of a kind counts.
                    channels.TryAdd(channel, content);
                }
            }

            return channels;
        }

        private static List<VRReplayFrame> decodeFrames(Dictionary<int, byte[]> channels, VRReplayExtras extras, int frameCount)
        {
            var frames = new VRReplayFrame[frameCount];

            // Without times there's no telling when anything was.
            if (!channels.TryGetValue((int)Channel.Times, out byte[]? times))
                throw new InvalidDataException("VR replay extras have no frame times.");

            using (var reader = new BinaryReader(new MemoryStream(times)))
            {
                long time = 0;

                for (int i = 0; i < frameCount; i++)
                {
                    time += unzigzag(reader.Read7BitEncodedInt64());
                    frames[i].Time = time * extras.TimeUnit;
                }
            }

            // Anything else may be missing: it's then unknown for the play (nothing tracked, nothing pressed).
            byte[] flags = channels.TryGetValue((int)Channel.Flags, out byte[]? f) ? f : new byte[frameCount];

            if (flags.Length != frameCount)
                throw new InvalidDataException("VR replay extras are damaged.");

            for (int i = 0; i < frameCount; i++)
                frames[i].ActiveHand = (flags[i] & flag_right_active) != 0 ? IVRControllers.RIGHT : IVRControllers.LEFT;

            readPoses(channels, Channel.HeadPoses, extras, frames, flags, flag_head, static (ref VRReplayFrame frame, VRPose p) => frame.Head = p);
            readPoses(channels, Channel.LeftGripPoses, extras, frames, flags, flag_left, static (ref VRReplayFrame frame, VRPose p) => frame.Left.Grip = p);
            readPoses(channels, Channel.RightGripPoses, extras, frames, flags, flag_right, static (ref VRReplayFrame frame, VRPose p) => frame.Right.Grip = p);

            readBytes(channels, Channel.LeftTrigger, frames, static (ref VRReplayFrame frame, float v) => frame.Left.Trigger = v);
            readBytes(channels, Channel.LeftSqueeze, frames, static (ref VRReplayFrame frame, float v) => frame.Left.Squeeze = v);
            readBytes(channels, Channel.RightTrigger, frames, static (ref VRReplayFrame frame, float v) => frame.Right.Trigger = v);
            readBytes(channels, Channel.RightSqueeze, frames, static (ref VRReplayFrame frame, float v) => frame.Right.Squeeze = v);

            return new List<VRReplayFrame>(frames);
        }

        /// <summary>
        /// Far more frames than a play has: ten hours at 120 a second.
        /// </summary>
        private const int max_trusted_frames = 10 * 3600 * 120;

        private delegate void PoseSetter(ref VRReplayFrame frame, VRPose pose);

        private delegate void ValueSetter(ref VRReplayFrame frame, float value);

        private static void readBytes(Dictionary<int, byte[]> channels, Channel channel, VRReplayFrame[] frames, ValueSetter set)
        {
            if (!channels.TryGetValue((int)channel, out byte[]? data))
                return;

            if (data.Length != frames.Length)
                throw new InvalidDataException("VR replay extras are damaged.");

            for (int i = 0; i < frames.Length; i++)
                set(ref frames[i], data[i] / 255f);
        }

        private static void readPoses(Dictionary<int, byte[]> channels, Channel channel, VRReplayExtras extras, VRReplayFrame[] frames, byte[] flags, byte flag, PoseSetter set)
        {
            // (Tracked according to the flags, but stored in a way this build doesn't know: not tracked, as far as it can tell.)
            if (!channels.TryGetValue((int)channel, out byte[]? data))
                return;

            int count = 0;

            foreach (byte f in flags)
            {
                if ((f & flag) != 0)
                    count++;
            }

            float[][] values = new float[6][];
            byte[] largest;

            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                for (int v = 0; v < 6; v++)
                    values[v] = readValues(reader, count, v < 3 ? extras.PositionUnit : extras.RotationUnit);

                largest = reader.ReadBytes(count);

                if (largest.Length != count)
                    throw new EndOfStreamException();
            }

            Span<float> components = stackalloc float[4];
            int n = 0;

            for (int i = 0; i < frames.Length; i++)
            {
                if ((flags[i] & flag) == 0)
                    continue;

                int index = largest[n];
                if (index > 3)
                    throw new InvalidDataException("VR replay extras are damaged.");

                float a = values[3][n], b = values[4][n], c = values[5][n];

                for (int k = 0, m = 0; k < 4; k++)
                    components[k] = k == index ? MathF.Sqrt(MathF.Max(1 - a * a - b * b - c * c, 0)) : values[3 + m++][n];

                set(ref frames[i], new VRPose(
                    new Vector3(values[0][n], values[1][n], values[2][n]),
                    Quaternion.Normalize(new Quaternion(components[0], components[1], components[2], components[3]))));

                n++;
            }
        }

        private static float[] readValues(BinaryReader reader, int count, float unit)
        {
            float[] values = new float[count];
            long last = 0;

            for (int i = 0; i < count; i++)
            {
                last += unzigzag(reader.Read7BitEncodedInt64());
                values[i] = last * unit;
            }

            return values;
        }

        private static byte toByte(float value) => (byte)MathF.Round(Math.Clamp(float.IsNaN(value) ? 0 : value, 0, 1) * 255);

        private static long zigzag(long value) => (value << 1) ^ (value >> 63);

        private static long unzigzag(long value) => (long)((ulong)value >> 1) ^ -(value & 1);
    }
}
