// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using osu.Game.Configuration;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: the stored form of <see cref="OsuSetting.VRPanelPlacement"/>: where the user moved the VR panel to, relative to where they face
    /// when recentering. Seven numbers: the offset of the panel's centre of curvature (x, y, z in metres) and its rotation (a quaternion).
    /// </summary>
    public static class VRPanelPlacement
    {
        public static string Format(Vector3 offset, Quaternion rotation)
            => string.Join(' ', new[] { offset.X, offset.Y, offset.Z, rotation.X, rotation.Y, rotation.Z, rotation.W }
                .Select(v => v.ToString("0.#####", CultureInfo.InvariantCulture)));

        /// <returns>Whether <paramref name="value"/> holds a placement (false for the default placement, an empty string).</returns>
        public static bool TryParse(string? value, out Vector3 offset, out Quaternion rotation)
        {
            offset = default;
            rotation = Quaternion.Identity;

            string[] parts = (value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            float[] numbers = new float[7];

            if (parts.Length != 7)
                return false;

            for (int i = 0; i < 7; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]) || !float.IsFinite(numbers[i]))
                    return false;
            }

            var parsedRotation = new Quaternion(numbers[3], numbers[4], numbers[5], numbers[6]);
            if (parsedRotation.LengthSquared() < 0.5f)
                return false;

            offset = new Vector3(numbers[0], numbers[1], numbers[2]);
            rotation = Quaternion.Normalize(parsedRotation);
            return true;
        }
    }
}
