// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Online
{
    /// <summary>
    /// Lightweight, safe provider for client environment and peripheral metadata
    /// (tablet model, active area in mm, monitor refresh rate, audio offset).
    /// Used by anti-cheat and multi-account correlation services.
    /// </summary>
    public static class EnvironmentTelemetry
    {
        /// <summary>
        /// Delegate registered by the game host to provide live hardware/peripherals state.
        /// </summary>
        public static Func<string?>? Provider { get; set; }

        /// <summary>
        /// Returns the formatted environment telemetry string, or null if uninitialized/unavailable.
        /// Guaranteed not to throw exceptions.
        /// </summary>
        public static string? Get()
        {
            try
            {
                return Provider?.Invoke();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Scans known OpenTabletDriver plugin directories for installed filters / plugins.
        /// Returns a comma-separated list of plugin basenames (e.g. "HawkuFilter,AimAssist"), or null.
        /// Completely non-invasive, safe, and catches all IO exceptions.
        /// </summary>
        public static string? GetDetectedTabletPlugins()
        {
            try
            {
                var candidateDirs = new System.Collections.Generic.List<string>();

                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localApp))
                    candidateDirs.Add(System.IO.Path.Combine(localApp, "OpenTabletDriver", "Plugins"));

                string roamingApp = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrEmpty(roamingApp))
                    candidateDirs.Add(System.IO.Path.Combine(roamingApp, "OpenTabletDriver", "Plugins"));

                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfile))
                {
                    candidateDirs.Add(System.IO.Path.Combine(userProfile, ".config", "OpenTabletDriver", "Plugins"));
                    candidateDirs.Add(System.IO.Path.Combine(userProfile, "Library", "Application Support", "OpenTabletDriver", "Plugins"));
                }

                var pluginNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string dir in candidateDirs)
                {
                    if (System.IO.Directory.Exists(dir))
                    {
                        var files = System.IO.Directory.GetFiles(dir, "*.*");
                        foreach (string file in files)
                        {
                            string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                            if (ext == ".dll" || ext == ".so" || ext == ".dylib")
                            {
                                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                                if (!string.IsNullOrWhiteSpace(name) && name.Length <= 40)
                                    pluginNames.Add(Uri.EscapeDataString(name));
                            }
                        }
                    }
                }

                if (pluginNames.Count > 0)
                    return string.Join(",", pluginNames);
            }
            catch
            {
                // ignored
            }

            return null;
        }
    }
}
