// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;

namespace osu.Game.IO
{
    public static class ToriiStoragePathHelper
    {
        public static string? GetLikelyLazerStoragePath()
        {
            return null;
        }

        public static bool LooksLikeLazerStoragePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return false;

            return File.Exists(Path.Combine(path, OsuGameBase.CLIENT_DATABASE_FILENAME));
        }
    }
}
