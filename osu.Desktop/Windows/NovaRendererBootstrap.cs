// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Runtime.Versioning;
using osu.Framework.Logging;

namespace osu.Desktop.Windows
{
    internal static class NovaRendererBootstrap
    {
        private static readonly string[] dxvk_files = ["dxgi.dll", "d3d11.dll"];
        private static readonly string[] zink_files = ["opengl32.dll", "libgallium_wgl.dll"];

        public static void PrepareRenderer(string? requestedRenderer)
        {
            string rendererLower = (requestedRenderer ?? string.Empty).Trim().ToLowerInvariant();

            if (OperatingSystem.IsWindows())
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                if (rendererLower.Contains("dxvk"))
                {
                    setupDxvk(baseDir);
                }
                else if (rendererLower.Contains("zink"))
                {
                    setupZink(baseDir);
                }
                else
                {
                    cleanupAll(baseDir);
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                if (rendererLower.Contains("zink"))
                {
                    Environment.SetEnvironmentVariable("MESA_LOADER_DRIVER_OVERRIDE", "zink");
                    Environment.SetEnvironmentVariable("GALLIUM_DRIVER", "zink");
                    Logger.Log("[Nova Zink] Enabled Mesa Zink (OpenGL over Vulkan) for Linux.", LoggingTarget.Runtime, LogLevel.Important);
                }
                else
                {
                    Environment.SetEnvironmentVariable("MESA_LOADER_DRIVER_OVERRIDE", null);
                    Environment.SetEnvironmentVariable("GALLIUM_DRIVER", null);
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private static void setupDxvk(string baseDir)
        {
            cleanFiles(baseDir, zink_files);

            string? dxvkDir = findRuntimeDirectory(baseDir, "dxvk");
            if (dxvkDir == null)
            {
                Logger.Log("[Nova DXVK] Runtime directory not found.", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            if (deployFiles(dxvkDir, baseDir, dxvk_files))
            {
                Environment.SetEnvironmentVariable("DXVK_LOG_LEVEL", "info");
                Environment.SetEnvironmentVariable("DXVK_STATE_CACHE", "1");
                Logger.Log("[Nova DXVK] Deployed Direct3D 11 via DXVK (Vulkan translation) to application directory.", LoggingTarget.Runtime, LogLevel.Important);
            }
        }

        private static void setupZink(string baseDir)
        {
            cleanFiles(baseDir, dxvk_files);

            string? zinkDir = findRuntimeDirectory(baseDir, "zink");
            if (zinkDir == null)
            {
                Logger.Log("[Nova Zink] Runtime directory not found.", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            if (deployFiles(zinkDir, baseDir, zink_files))
            {
                Environment.SetEnvironmentVariable("MESA_LOADER_DRIVER_OVERRIDE", "zink");
                Environment.SetEnvironmentVariable("GALLIUM_DRIVER", "zink");
                Logger.Log("[Nova Zink] Deployed OpenGL via Mesa Zink (Vulkan translation) to application directory.", LoggingTarget.Runtime, LogLevel.Important);
            }
        }

        private static void cleanupAll(string baseDir)
        {
            cleanFiles(baseDir, dxvk_files);
            cleanFiles(baseDir, zink_files);

            Environment.SetEnvironmentVariable("DXVK_LOG_LEVEL", null);
            Environment.SetEnvironmentVariable("DXVK_STATE_CACHE", null);
            Environment.SetEnvironmentVariable("MESA_LOADER_DRIVER_OVERRIDE", null);
            Environment.SetEnvironmentVariable("GALLIUM_DRIVER", null);
        }

        private static bool deployFiles(string sourceDir, string targetDir, string[] fileNames)
        {
            try
            {
                foreach (string file in fileNames)
                {
                    string source = Path.Combine(sourceDir, file);
                    string target = Path.Combine(targetDir, file);

                    if (!File.Exists(source))
                    {
                        Logger.Log($"[Nova Renderer] Missing required file: {source}", LoggingTarget.Runtime, LogLevel.Important);
                        return false;
                    }

                    if (File.Exists(target))
                    {
                        var srcInfo = new FileInfo(source);
                        var dstInfo = new FileInfo(target);
                        if (srcInfo.Length == dstInfo.Length)
                            continue;
                    }

                    File.Copy(source, target, overwrite: true);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"[Nova Renderer] Failed to deploy translation files: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                return false;
            }
        }

        private static void cleanFiles(string baseDir, string[] fileNames)
        {
            foreach (string file in fileNames)
            {
                try
                {
                    string target = Path.Combine(baseDir, file);
                    if (File.Exists(target))
                        File.Delete(target);
                }
                catch
                {
                    // best-effort cleanup
                }
            }
        }

        private static string? findRuntimeDirectory(string baseDir, string name)
        {
            string[] candidates =
            [
                Path.Combine(baseDir, "runtimes", "win-x64", name),
                Path.Combine(baseDir, name),
                Path.Combine(baseDir, "runtimes", name),
            ];

            foreach (string candidate in candidates)
            {
                if (Directory.Exists(candidate))
                    return candidate;
            }

            return null;
        }
    }
}
