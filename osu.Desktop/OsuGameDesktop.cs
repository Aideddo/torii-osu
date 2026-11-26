// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;
using osu.Desktop.IPC;
using osu.Desktop.Security;
using osu.Framework.Platform;
using osu.Game;
using osu.Desktop.Updater;
using osu.Framework;
using osu.Framework.Logging;
using osu.Game.Updater;
using osu.Desktop.MacOS;
using osu.Desktop.Windows;
using osu.Framework.Allocation;
using osu.Game.Configuration;
using osu.Game.IO;
using osu.Game.IPC;
using osu.Game.Performance;
using osu.Game.Utils;
using osu.Game.VR;
using osu.Desktop.VR;
using osu.Framework.Platform.OpenXR;

namespace osu.Desktop
{
    internal partial class OsuGameDesktop : OsuGame
    {
        private OsuSchemeLinkIPCChannel? osuSchemeLinkIPCChannel;
        private ArchiveImportIPCChannel? archiveImportIPCChannel;

        [Cached(typeof(IHighPerformanceSessionManager))]
        private readonly HighPerformanceSessionManager highPerformanceSessionManager = new HighPerformanceSessionManager();

        public bool IsFirstRun { get; init; }

        public bool EnableWebSocketServer { get; init; }

        private readonly string[] args;

        public OsuGameDesktop(string[]? args = null)
            : base(args)
        {
            this.args = args ?? Array.Empty<string>();
        }

        private DesktopVRModeControl? vrModeControl;

        // Torii: VR mode can be switched on and off on Windows and Linux (the platforms with a host presenting through OpenXR).
        protected override IVRModeControl? CreateVRModeControl()
            => RuntimeInfo.OS is RuntimeInfo.Platform.Windows or RuntimeInfo.Platform.Linux ? vrModeControl = new DesktopVRModeControl(this, args) : null;

        public override StableStorage? GetStorageForStableInstall()
        {
            try
            {
                if (Host is DesktopGameHost desktopHost)
                {
                    string? stablePath = getStableInstallPath();
                    if (!string.IsNullOrEmpty(stablePath))
                        return new StableStorage(stablePath, desktopHost);
                }
            }
            catch (Exception)
            {
                Logger.Log("Could not find a stable install", LoggingTarget.Runtime, LogLevel.Important);
            }

            return null;
        }

        private string? getStableInstallPath()
        {
            static bool checkExists(string p) => Directory.Exists(Path.Combine(p, "Songs")) || File.Exists(Path.Combine(p, "osu!.cfg"));

            string? stableInstallPath;

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    stableInstallPath = getStableInstallPathFromRegistry("osustable.File.osz");

                    if (!string.IsNullOrEmpty(stableInstallPath) && checkExists(stableInstallPath))
                        return stableInstallPath;

                    stableInstallPath = getStableInstallPathFromRegistry("osu!");

                    if (!string.IsNullOrEmpty(stableInstallPath) && checkExists(stableInstallPath))
                        return stableInstallPath;
                }
                catch
                {
                }
            }

            stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"osu!");
            if (checkExists(stableInstallPath))
                return stableInstallPath;

            stableInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".osu");
            if (checkExists(stableInstallPath))
                return stableInstallPath;

            return null;
        }

        [SupportedOSPlatform("windows")]
        private string? getStableInstallPathFromRegistry(string progId)
        {
            using (RegistryKey? key = Registry.ClassesRoot.OpenSubKey(progId))
                return key?.OpenSubKey(WindowsAssociationManager.SHELL_OPEN_COMMAND)?.GetValue(string.Empty)?.ToString()?.Split('"')[1].Replace("osu!.exe", "");
        }

        public static bool IsPackageManaged => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OSU_EXTERNAL_UPDATE_PROVIDER"));

        protected override UpdateManager CreateUpdateManager()
        {
            // If this is the first time we've run the game, ie it is being installed,
            // reset the user's release stream specified by the installation target.
            //
            // This ensures that if a user is trying to recover from a failed startup, it will keep them
            // on the stream which is imminently being reinstalled.
            if (IsFirstRun)
                LocalConfig.SetValue(OsuSetting.ReleaseStream, ReleaseStream.Torii);

            if (IsPackageManaged)
                return new NoActionUpdateManager();

            return new VelopackUpdateManager();
        }

        public override bool RestartAppWhenExited()
        {
            if (IsPackageManaged)
                return false;

            if (!IsDeployedBuild)
            {
                if (!osu.Framework.Development.DebugUtils.IsDebugBuild)
                    return false;

                string? executable = Environment.ProcessPath;
                if (string.IsNullOrEmpty(executable))
                    return false;

                var start = new ProcessStartInfo
                {
                    FileName = executable,
                    WorkingDirectory = AppContext.BaseDirectory,
                    UseShellExecute = true,
                };
                string[] arguments = Environment.GetCommandLineArgs();
                int firstArgument = Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                for (int i = firstArgument; i < arguments.Length; i++)
                    start.ArgumentList.Add(arguments[i]);

                RestartOnExitAction = () =>
                {
                    try
                    {
                        Process.Start(start);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Failed to restart development build.");
                    }
                };
                return true;
            }

            RestartOnExitAction = () =>
            {
                try
                {
                    Velopack.UpdateExe.Start(waitPid: (uint)Environment.ProcessId);
                }
                catch
                {
                    try
                    {
                        string? processPath = Environment.ProcessPath;
                        if (!string.IsNullOrEmpty(processPath))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = processPath,
                                UseShellExecute = true,
                                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Failed to restart application on exit.");
                    }
                }
            };
            return true;
        }

        protected override void LoadComplete()
        {
            // this is done before `base.LoadComplete` so that the game can immediately register data sources.
            if (EnableWebSocketServer)
            {
                var provider = new OsuWebSocketProvider();
                Add(provider);
                Dependencies.CacheAs<IWebSocketProvider>(provider);
            }

            base.LoadComplete();

            // Torii: when the headset's runtime is closed (or VR couldn't be started at all), carry on outside VR mode rather than exit
            // or sit there showing nothing.
            if (Host is IOpenXRHost xrHost && vrModeControl is DesktopVRModeControl control)
            {
                xrHost.OpenXR.ExitWhenSessionEnds = false;
                xrHost.OpenXR.ExitRequested += () => Schedule(control.LeaveAsEnded);
                // Failing after the session had started is the runtime going away without notice.
                xrHost.OpenXR.SessionFailed += () => Schedule(() =>
                {
                    if (xrHost.OpenXR.RuntimeName.Length > 0)
                        control.LeaveAsEnded();
                    else
                        control.LeaveAsFailed();
                });

                if (xrHost.OpenXR.Failed)
                    Schedule(control.LeaveAsFailed);
            }

            LoadComponentAsync(new DiscordRichPresence(), Add);

            switch (RuntimeInfo.OS)
            {
                case RuntimeInfo.Platform.Windows:
                    LoadComponentAsync(new GameplayWinKeyBlocker(), Add);
                    break;

                case RuntimeInfo.Platform.macOS when !IsPackageManaged && IsDeployedBuild:
                    if (!IsPackageManaged && IsDeployedBuild)
                        LoadComponentAsync(new MacOSAppLocationChecker(), Add);
                    break;
            }

            LoadComponentAsync(new ElevatedPrivilegesChecker(), Add);

            osuSchemeLinkIPCChannel = new OsuSchemeLinkIPCChannel(Host, this);
            archiveImportIPCChannel = new ArchiveImportIPCChannel(Host, this);
        }

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);

            // Apple operating systems use a better icon provided via external assets.
            if (!RuntimeInfo.IsApple)
            {
                var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetType(), "torii.ico");
                if (iconStream != null)
                    host.Window.SetIconFromStream(iconStream);
            }

            host.Window.Title = Name;
        }

        protected override BatteryInfo CreateBatteryInfo() => FrameworkEnvironment.UseSDL3 ? new SDL3BatteryInfo() : new SDL2BatteryInfo();

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            osuSchemeLinkIPCChannel?.Dispose();
            archiveImportIPCChannel?.Dispose();
        }
    }
}
