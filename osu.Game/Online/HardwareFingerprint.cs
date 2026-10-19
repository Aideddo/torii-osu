// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using osu.Framework.Logging;

namespace osu.Game.Online
{
    /// <summary>
    /// Generates stable hardware fingerprint hashes and persistent device markers for the current device.
    /// Combines motherboard/BIOS UUIDs, CPU model info, physical MAC address, disk volume serials,
    /// OS identifiers, and multi-location persistent device breadcrumbs (files + registry)
    /// to prevent mass account generation scripts, evasion, and spoofing while allowing multi-device play.
    /// </summary>
    public static class HardwareFingerprint
    {
        private static readonly Lazy<string> fingerprint = new Lazy<string>(generateFingerprint);
        private static readonly Lazy<string> fingerprintV2 = new Lazy<string>(generateFingerprintV2);
        private static readonly Lazy<string> deviceToken = new Lazy<string>(getOrCreatePersistentDeviceId);

        /// <summary>
        /// Returns the legacy 64-character lowercase SHA-256 hex string identifying this machine (v1).
        /// Maintained for backwards compatibility with existing database and Redis indexes.
        /// </summary>
        public static string Get() => fingerprint.Value;

        /// <summary>
        /// Returns the deep hardware entropy 64-character lowercase SHA-256 hex string (v2).
        /// Combines primary physical MAC address, disk volume serial, GPU display adapter,
        /// CPU/motherboard identifiers, and persistent breadcrumbs.
        /// </summary>
        public static string GetV2() => fingerprintV2.Value;

        /// <summary>
        /// Returns the persistent self-healing device breadcrumb token (32-character hex).
        /// Stored across multiple locations (AppData, LocalAppData, Registry, UserProfile)
        /// and automatically restored if any location is wiped or deleted.
        /// </summary>
        public static string GetDeviceToken() => deviceToken.Value;

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
                Logger.Log($"[Torii] Error generating hardware fingerprint v1: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
            }

            // Ensure we never return an empty fingerprint
            if (parts.Count == 0)
                parts.Add(Environment.MachineName);

            string combined = string.Join("|", parts);
            return computeSha256Hex(combined);
        }

        private static string generateFingerprintV2()
        {
            var parts = new List<string>();

            try
            {
                // 1. Operating system and architecture
                parts.Add(RuntimeInformation.OSDescription);
                parts.Add(RuntimeInformation.ProcessArchitecture.ToString());
                parts.Add(Environment.ProcessorCount.ToString());

                // 2. Primary physical network MAC address
                string? physicalMac = getPrimaryPhysicalMacAddress();
                if (!string.IsNullOrEmpty(physicalMac))
                    parts.Add($"mac:{physicalMac}");

                // 3. Platform-specific hardware markers
                if (OperatingSystem.IsWindows())
                {
                    addWindowsHardware(parts);

                    // Deep Windows hardware: System drive volume serial
                    string? volSerial = getWindowsSystemDriveVolumeSerial();
                    if (!string.IsNullOrEmpty(volSerial))
                        parts.Add($"win-vol:{volSerial}");

                    // Deep Windows hardware: Primary GPU display adapter
                    string? gpuDesc = readWindowsRegistryString(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DriverDesc");
                    if (!string.IsNullOrEmpty(gpuDesc))
                        parts.Add($"win-gpu:{gpuDesc.Trim()}");
                }
                else if (OperatingSystem.IsMacOS())
                {
                    addMacHardware(parts);
                }
                else if (OperatingSystem.IsLinux())
                {
                    addLinuxHardware(parts);

                    // Linux DMI product UUID
                    if (File.Exists("/sys/class/dmi/id/product_uuid"))
                    {
                        try
                        {
                            string dmiUuid = File.ReadAllText("/sys/class/dmi/id/product_uuid").Trim();
                            if (!string.IsNullOrEmpty(dmiUuid))
                                parts.Add($"linux-dmi:{dmiUuid}");
                        }
                        catch { }
                    }
                }

                // 4. Persistent device breadcrumb
                string persistentToken = getOrCreatePersistentDeviceId();
                parts.Add($"token:{persistentToken}");
            }
            catch (Exception ex)
            {
                Logger.Log($"[Torii] Error generating hardware fingerprint v2: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
            }

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

        private static string? getPrimaryPhysicalMacAddress()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                if (interfaces == null || interfaces.Length == 0)
                    return null;

                foreach (var nic in interfaces)
                {
                    // Skip non-physical, loopback, or tunnels
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    // Only UP interfaces
                    if (nic.OperationalStatus != OperationalStatus.Up)
                        continue;

                    string name = (nic.Name + " " + nic.Description).ToLowerInvariant();
                    // Filter out known virtual, VPN, container and hypervisor adapters
                    if (name.Contains("virtual") ||
                        name.Contains("vmware") ||
                        name.Contains("hyper-v") ||
                        name.Contains("vethernet") ||
                        name.Contains("virtualbox") ||
                        name.Contains("hamachi") ||
                        name.Contains("radmin") ||
                        name.Contains("tap") ||
                        name.Contains("tun") ||
                        name.Contains("wireguard") ||
                        name.Contains("tailscale") ||
                        name.Contains("zerotier") ||
                        name.Contains("wsl"))
                    {
                        continue;
                    }

                    var address = nic.GetPhysicalAddress();
                    byte[] bytes = address.GetAddressBytes();
                    if (bytes != null && bytes.Length >= 6 && bytes.Any(b => b != 0))
                    {
                        return Convert.ToHexString(bytes).ToLowerInvariant();
                    }
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVolumeInformationW(
            string lpRootPathName,
            StringBuilder? lpVolumeNameBuffer,
            int nVolumeNameSize,
            out uint lpVolumeSerialNumber,
            out uint lpMaximumComponentLength,
            out uint lpFileSystemFlags,
            StringBuilder? lpFileSystemNameBuffer,
            int nFileSystemNameSize);

        private static string? getWindowsSystemDriveVolumeSerial()
        {
            if (!OperatingSystem.IsWindows())
                return null;

            try
            {
                string root = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
                if (GetVolumeInformationW(root, null, 0, out uint serialNumber, out _, out _, null, 0))
                {
                    return serialNumber.ToString("X8");
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }

        private static string? readWindowsRegistryString(string subKey, string valueName)
        {
            if (!OperatingSystem.IsWindows())
                return null;

            try
            {
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

        private static string? readWindowsRegistryCurrentUserString(string subKey, string valueName)
        {
            if (!OperatingSystem.IsWindows())
                return null;

            try
            {
                Type? registryType = Type.GetType("Microsoft.Win32.Registry, Microsoft.Win32.Registry")
                                     ?? Type.GetType("Microsoft.Win32.Registry, mscorlib");
                if (registryType == null)
                    return null;

                PropertyInfo? currentUserProp = registryType.GetProperty("CurrentUser", BindingFlags.Public | BindingFlags.Static);
                object? currentUser = currentUserProp?.GetValue(null);
                if (currentUser == null)
                    return null;

                MethodInfo? openSubKeyMethod = currentUser.GetType().GetMethod("OpenSubKey", new[] { typeof(string) });
                using var key = openSubKeyMethod?.Invoke(currentUser, new object[] { subKey }) as IDisposable;
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

        private static void writeWindowsRegistryCurrentUserString(string subKey, string valueName, string value)
        {
            if (!OperatingSystem.IsWindows())
                return;

            try
            {
                Type? registryType = Type.GetType("Microsoft.Win32.Registry, Microsoft.Win32.Registry")
                                     ?? Type.GetType("Microsoft.Win32.Registry, mscorlib");
                if (registryType == null)
                    return;

                PropertyInfo? currentUserProp = registryType.GetProperty("CurrentUser", BindingFlags.Public | BindingFlags.Static);
                object? currentUser = currentUserProp?.GetValue(null);
                if (currentUser == null)
                    return;

                MethodInfo? createSubKeyMethod = currentUser.GetType().GetMethod("CreateSubKey", new[] { typeof(string) });
                using var key = createSubKeyMethod?.Invoke(currentUser, new object[] { subKey }) as IDisposable;
                if (key == null)
                    return;

                MethodInfo? setValueMethod = key.GetType().GetMethod("SetValue", new[] { typeof(string), typeof(object) });
                setValueMethod?.Invoke(key, new object[] { valueName, value });
            }
            catch
            {
                // ignored
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

        private static bool isValidBreadcrumbToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            string trimmed = token.Trim();
            if (trimmed.Length < 16 || trimmed.Length > 64)
                return false;

            return trimmed.All(c => char.IsLetterOrDigit(c) || c == '-');
        }

        /// <summary>
        /// Reads or creates a persistent device breadcrumb ID across multiple locations
        /// (Roaming AppData, LocalAppData, Windows Registry, and user home hidden token).
        /// Performs automatic self-healing: if any location is wiped or missing, it is restored
        /// from the remaining locations.
        /// </summary>
        private static string getOrCreatePersistentDeviceId()
        {
            try
            {
                var candidateFilePaths = new List<string>();

                // 1. Roaming AppData (~/.config or %AppData%)
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrEmpty(appData))
                    candidateFilePaths.Add(Path.Combine(appData, "torii", "device.id"));

                // 2. Local AppData (~/.local/share or %LocalAppData%)
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                    candidateFilePaths.Add(Path.Combine(localAppData, "torii", "device.id"));

                // 3. User profile hidden token
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfile))
                {
                    candidateFilePaths.Add(Path.Combine(userProfile, ".torii_device_id"));

                    if (OperatingSystem.IsLinux())
                        candidateFilePaths.Add(Path.Combine(userProfile, ".config", "torii", "device.id"));
                }

                // Check file locations first
                string? existingToken = null;
                foreach (string filePath in candidateFilePaths)
                {
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            string content = File.ReadAllText(filePath).Trim();
                            if (isValidBreadcrumbToken(content))
                            {
                                existingToken = content;
                                break;
                            }
                        }
                    }
                    catch { }
                }

                // Check Windows Registry if not found in files
                if (existingToken == null && OperatingSystem.IsWindows())
                {
                    string? regToken = readWindowsRegistryCurrentUserString(@"Software\Torii", "DeviceToken");
                    if (isValidBreadcrumbToken(regToken))
                        existingToken = regToken!.Trim();
                }

                // If no valid token found anywhere, generate a fresh unique breadcrumb
                string activeToken = existingToken ?? Guid.NewGuid().ToString("N");

                // Self-healing: persist the active token to all candidate locations
                foreach (string filePath in candidateFilePaths)
                {
                    try
                    {
                        string? dir = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);

                        if (!File.Exists(filePath) || File.ReadAllText(filePath).Trim() != activeToken)
                            File.WriteAllText(filePath, activeToken);
                    }
                    catch { }
                }

                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        writeWindowsRegistryCurrentUserString(@"Software\Torii", "DeviceToken", activeToken);
                    }
                    catch { }
                }

                return activeToken;
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
