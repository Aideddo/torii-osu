// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using osu.Framework.Logging;

namespace osu.Game.Online
{
    /// <summary>
    /// Generates a stable hardware fingerprint hash for the current device.
    /// Combines motherboard/BIOS UUIDs, CPU model info, OS identifiers, and persistent device markers
    /// to prevent mass account generation scripts and spoofing while allowing multi-device play.
    /// </summary>
    public static class HardwareFingerprint
    {
        private static readonly Lazy<string> fingerprint = new Lazy<string>(generateFingerprint);

        /// <summary>
        /// Returns a 64-character lowercase SHA-256 hex string identifying this machine.
        /// </summary>
        public static string Get() => fingerprint.Value;

        private static string generateFingerprint()
        {
            var parts = new List<string>();

            try
            {
                // 1. Operating system platform and basic architecture
                parts.Add(RuntimeInformation.OSDescription);
                parts.Add(RuntimeInformation.ProcessArchitecture.ToString());
                parts.Add(Environment.ProcessorCount.ToString());

                // 2. Platform-specific persistent hardware markers
                if (OperatingSystem.IsWindows())
                {
                    addWindowsHardware(parts);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    addMacHardware(parts);
                }
                else if (OperatingSystem.IsLinux())
                {
                    addLinuxHardware(parts);
                }

                // 3. Fallback persistent device marker
                string deviceFallback = getOrCreatePersistentDeviceId();
                parts.Add(deviceFallback);
            }
            catch (Exception ex)
            {
                Logger.Log($"[Torii] Error generating hardware fingerprint: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
            }

            // Ensure we never return an empty fingerprint
            if (parts.Count == 0)
                parts.Add(Environment.MachineName);

            string combined = string.Join("|", parts);
            return computeSha256Hex(combined);
        }

        private static void addWindowsHardware(List<string> parts)
        {
            // Windows MachineGuid from Cryptography registry
            string? machineGuid = readWindowsRegistryString(@"SOFTWARE\Microsoft\Cryptography", "MachineGuid");
            if (!string.IsNullOrEmpty(machineGuid))
                parts.Add($"win-guid:{machineGuid}");

            // Windows CPU Identifier environment variable
            string? cpuEnv = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            if (!string.IsNullOrEmpty(cpuEnv))
                parts.Add($"win-cpu-env:{cpuEnv}");

            // Windows CPU ProcessorNameString from Registry
            string? cpuName = readWindowsRegistryString(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString");
            if (!string.IsNullOrEmpty(cpuName))
                parts.Add($"win-cpu-reg:{cpuName.Trim()}");

            // Windows Motherboard / System Product Name
            string? board = readWindowsRegistryString(@"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardProduct");
            if (!string.IsNullOrEmpty(board))
                parts.Add($"win-board:{board.Trim()}");

            string? systemProduct = readWindowsRegistryString(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemProductName");
            if (!string.IsNullOrEmpty(systemProduct))
                parts.Add($"win-sys:{systemProduct.Trim()}");

            string? compName = Environment.MachineName;
            if (!string.IsNullOrEmpty(compName))
                parts.Add($"win-pc:{compName}");
        }

        private static void addMacHardware(List<string> parts)
        {
            // macOS IOPlatformUUID via ioreg
            string? macUuid = getMacPlatformUuid();
            if (!string.IsNullOrEmpty(macUuid))
                parts.Add($"mac-uuid:{macUuid}");

            // macOS Hardware Model via sysctl
            string? macModel = runProcessAndReadOutput("sysctl", "-n hw.model");
            if (!string.IsNullOrEmpty(macModel))
                parts.Add($"mac-model:{macModel.Trim()}");

            string? compName = Environment.MachineName;
            if (!string.IsNullOrEmpty(compName))
                parts.Add($"mac-pc:{compName}");
        }

        private static void addLinuxHardware(List<string> parts)
        {
            // Linux /etc/machine-id or dbus machine-id
            if (File.Exists("/etc/machine-id"))
            {
                try
                {
                    string id = File.ReadAllText("/etc/machine-id").Trim();
                    if (!string.IsNullOrEmpty(id))
                        parts.Add($"linux-mid:{id}");
                }
                catch { }
            }
            else if (File.Exists("/var/lib/dbus/machine-id"))
            {
                try
                {
                    string id = File.ReadAllText("/var/lib/dbus/machine-id").Trim();
                    if (!string.IsNullOrEmpty(id))
                        parts.Add($"linux-dbus:{id}");
                }
                catch { }
            }

            // Linux CPU model from /proc/cpuinfo
            if (File.Exists("/proc/cpuinfo"))
            {
                try
                {
                    foreach (string line in File.ReadLines("/proc/cpuinfo"))
                    {
                        if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                        {
                            var tokens = line.Split(':', 2);
                            if (tokens.Length > 1 && !string.IsNullOrWhiteSpace(tokens[1]))
                            {
                                parts.Add($"linux-cpu:{tokens[1].Trim()}");
                                break;
                            }
                        }
                    }
                }
                catch { }
            }

            string? compName = Environment.MachineName;
            if (!string.IsNullOrEmpty(compName))
                parts.Add($"linux-pc:{compName}");
        }

        private static string? readWindowsRegistryString(string subKey, string valueName)
        {
            if (!OperatingSystem.IsWindows())
                return null;

            try
            {
                // Loaded dynamically via reflection so non-Windows builds have zero runtime dependency errors
                Type? registryType = Type.GetType("Microsoft.Win32.Registry, Microsoft.Win32.Registry")
                                     ?? Type.GetType("Microsoft.Win32.Registry, mscorlib");
                if (registryType == null)
                    return null;

                PropertyInfo? localMachineProp = registryType.GetProperty("LocalMachine", BindingFlags.Public | BindingFlags.Static);
                object? localMachine = localMachineProp?.GetValue(null);
                if (localMachine == null)
                    return null;

                MethodInfo? openSubKeyMethod = localMachine.GetType().GetMethod("OpenSubKey", new[] { typeof(string) });
                using var key = openSubKeyMethod?.Invoke(localMachine, new object[] { subKey }) as IDisposable;
                if (key == null)
                    return null;

                MethodInfo? getValueMethod = key.GetType().GetMethod("GetValue", new[] { typeof(string) });
                object? val = getValueMethod?.Invoke(key, new object[] { valueName });
                return val?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string? getMacPlatformUuid()
        {
            try
            {
                string? output = runProcessAndReadOutput("ioreg", "-rd1 -c IOPlatformExpertDevice");
                if (string.IsNullOrEmpty(output))
                    return null;

                var match = Regex.Match(output, "\"IOPlatformUUID\"\\s*=\\s*\"([^\"]+)\"");
                if (match.Success)
                    return match.Groups[1].Value.Trim();
            }
            catch { }

            return null;
        }

        private static string? runProcessAndReadOutput(string filename, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = filename,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return null;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(1000);
                return output;
            }
            catch
            {
                return null;
            }
        }

        private static string getOrCreatePersistentDeviceId()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(appData))
                    appData = Path.GetTempPath();

                string toriiDir = Path.Combine(appData, "torii");
                Directory.CreateDirectory(toriiDir);

                string idFile = Path.Combine(toriiDir, "device.id");
                if (File.Exists(idFile))
                {
                    string existing = File.ReadAllText(idFile).Trim();
                    if (!string.IsNullOrEmpty(existing))
                        return existing;
                }

                string newId = Guid.NewGuid().ToString("N");
                File.WriteAllText(idFile, newId);
                return newId;
            }
            catch
            {
                return Environment.MachineName;
            }
        }

        private static string computeSha256Hex(string input)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
