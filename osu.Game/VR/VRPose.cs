// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using Newtonsoft.Json;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: where something is and which way it faces in VR. Positions are in metres, +Y up.
    /// </summary>
    /// <remarks>
    /// Serialised to JSON as seven numbers: position x, y, z, then rotation x, y, z, w.
    /// </remarks>
    [JsonConverter(typeof(VRPoseJsonConverter))]
    public readonly record struct VRPose(Vector3 Position, Quaternion Rotation)
    {
        public static readonly VRPose IDENTITY = new VRPose(Vector3.Zero, Quaternion.Identity);

        /// <summary>
        /// This pose as seen from <paramref name="parent"/>: both are in the same space, the result is in the parent's own frame.
        /// </summary>
        public VRPose RelativeTo(VRPose parent)
        {
            var inverse = Quaternion.Inverse(parent.Rotation);
            return new VRPose(Vector3.Transform(Position - parent.Position, inverse), Quaternion.Normalize(inverse * Rotation));
        }

        /// <summary>
        /// The reverse of <see cref="RelativeTo"/>: takes <paramref name="child"/>, given in this pose's own frame, to the space this pose is in.
        /// </summary>
        public VRPose Apply(VRPose child)
            => new VRPose(Position + Vector3.Transform(child.Position, Rotation), Quaternion.Normalize(Rotation * child.Rotation));

        /// <summary>
        /// This pose stood upright: at the same place and facing the same way round, but level (+Y up).
        /// </summary>
        public VRPose Levelled
        {
            get
            {
                var forward = Vector3.Transform(-Vector3.UnitZ, Rotation);

                // Facing straight up or down: no way round to keep.
                if (forward.X * forward.X + forward.Z * forward.Z < 1e-6f)
                    return this with { Rotation = Quaternion.Identity };

                return this with { Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(-forward.X, -forward.Z)) };
            }
        }

        /// <summary>
        /// The pose part-way from <paramref name="a"/> to <paramref name="b"/>.
        /// </summary>
        public static VRPose Lerp(VRPose a, VRPose b, float amount)
            => new VRPose(Vector3.Lerp(a.Position, b.Position, amount), Quaternion.Slerp(a.Rotation, b.Rotation, amount));
    }

    public class VRPoseJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(VRPose) || objectType == typeof(VRPose?);

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is not VRPose pose)
            {
                writer.WriteNull();
                return;
            }

            // Without indentation, whatever the document uses: seven numbers read better on one line.
            var formatting = writer.Formatting;
            writer.WriteStartArray();
            writer.Formatting = Formatting.None;

            writer.WriteValue(pose.Position.X);
            writer.WriteValue(pose.Position.Y);
            writer.WriteValue(pose.Position.Z);
            writer.WriteValue(pose.Rotation.X);
            writer.WriteValue(pose.Rotation.Y);
            writer.WriteValue(pose.Rotation.Z);
            writer.WriteValue(pose.Rotation.W);

            writer.WriteEndArray();
            writer.Formatting = formatting;
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                if (objectType == typeof(VRPose))
                    throw new JsonSerializationException("A pose is missing.");

                return null;
            }

            float[] values = serializer.Deserialize<float[]>(reader) ?? Array.Empty<float>();

            if (values.Length != 7)
                throw new JsonSerializationException($"A pose is seven numbers, not {values.Length}.");

            return new VRPose(new Vector3(values[0], values[1], values[2]), new Quaternion(values[3], values[4], values[5], values[6]));
        }
    }
}
