// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using osu.Framework.Logging;

namespace osu.Desktop.Windows
{
    [SupportedOSPlatform("windows")]
    internal static class NovaRendererBootstrap
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string? lpPathName);

        private static bool isInitialized;

        public static void PrepareRenderer(string? requestedRenderer)
        {
            if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(requestedRenderer) || isInitialized)
                return;

            string rendererLower = requestedRenderer.Trim().ToLowerInvariant();

            if (rendererLower.Contains("dxvk"))
            {
                setupDxvk();
            }
            else if (rendererLower.Contains("zink"))
            {
                setupZink();
            }
        }

        private static void setupDxvk()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string? dxvkDir = findRuntimeDirectory(baseDir, "dxvk");

            if (dxvkDir == null)
            {
                Logger.Log("[Nova DXVK] Runtime directory not found.", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            string dxgiPath = Path.Combine(dxvkDir, "dxgi.dll");
            string d3d11Path = Path.Combine(dxvkDir, "d3d11.dll");

            if (!File.Exists(dxgiPath) || !File.Exists(d3d11Path))
            {
                Logger.Log($"[Nova DXVK] Required DLLs missing in {dxvkDir}", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            try
            {
                SetDllDirectory(dxvkDir);

                // Configure DXVK environment defaults for smooth rhythm game performance
                Environment.SetEnvironmentVariable("DXVK_LOG_LEVEL", "info");
                Environment.SetEnvironmentVariable("DXVK_STATE_CACHE", "1");

                // Preload DXGI then D3D11 into the process address space
                NativeLibrary.Load(dxgiPath);
                NativeLibrary.Load(d3d11Path);

                isInitialized = true;
                Logger.Log($"[Nova DXVK] Intercepted Direct3D 11 via DXVK (Vulkan translation) from {dxvkDir}", LoggingTarget.Runtime, LogLevel.Important);
            }
            catch (Exception ex)
            {
                Logger.Log($"[Nova DXVK] Failed to initialise DXVK: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
            }
        }

        private static void setupZink()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string? zinkDir = findRuntimeDirectory(baseDir, "zink");

            if (zinkDir == null)
            {
                Logger.Log("[Nova Zink] Runtime directory not found.", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            string openglPath = Path.Combine(zinkDir, "opengl32.dll");
            string galliumPath = Path.Combine(zinkDir, "libgallium_wgl.dll");

            if (!File.Exists(openglPath) || !File.Exists(galliumPath))
            {
                Logger.Log($"[Nova Zink] Required DLLs missing in {zinkDir}", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            try
            {
                Environment.SetEnvironmentVariable("MESA_LOADER_DRIVER_OVERRIDE", "zink");
                Environment.SetEnvironmentVariable("GALLIUM_DRIVER", "zink");

                SetDllDirectory(zinkDir);

                // Preload Gallium driver, then Mesa opengl32
                NativeLibrary.Load(galliumPath);
                NativeLibrary.Load(openglPath);

                isInitialized = true;
                Logger.Log($"[Nova Zink] Intercepted OpenGL via Mesa Zink (Vulkan translation) from {zinkDir}", LoggingTarget.Runtime, LogLevel.Important);
            }
            catch (Exception ex)
            {
                Logger.Log($"[Nova Zink] Failed to initialise Zink: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
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
