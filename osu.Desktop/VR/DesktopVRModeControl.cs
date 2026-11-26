// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game;
using osu.Game.VR;

namespace osu.Desktop.VR
{
    /// <summary>
    /// Torii: switches the desktop game in and out of VR mode by restarting it with or without <see cref="VR_ARGUMENT"/>.
    /// VR mode is available while SteamVR is running.
    /// </summary>
    public class DesktopVRModeControl : IVRModeControl
    {
        /// <summary>
        /// Starts the game in VR mode.
        /// </summary>
        public const string VR_ARGUMENT = "--vr";

        /// <summary>
        /// Starts the game without offering VR mode: it was just switched off.
        /// </summary>
        public const string NO_OFFER_ARGUMENT = "--no-vr-offer";

        /// <summary>
        /// Starts the game saying that VR mode ended because the headset's runtime was closed.
        /// </summary>
        public const string ENDED_ARGUMENT = "--vr-ended";

        /// <summary>
        /// Starts the game saying that VR mode couldn't be started.
        /// </summary>
        public const string FAILED_ARGUMENT = "--vr-failed";

        /// <summary>
        /// Makes the game wait for the process with the given id to exit before starting (the instance it replaces).
        /// </summary>
        public const string WAIT_ARGUMENT = "--wait-pid=";

        /// <summary>
        /// Set (to 1) when SDL3 was forced for VR mode rather than chosen by the user, so that leaving VR mode can undo it.
        /// </summary>
        public const string FORCED_SDL3_VARIABLE = "TORII_VR_FORCED_SDL3";

        private static readonly string[] mode_arguments = { VR_ARGUMENT, NO_OFFER_ARGUMENT, ENDED_ARGUMENT, FAILED_ARGUMENT };

        public bool Enabled => VRRuntime.IsVR;

        public IBindable<bool> Available => available;

        public bool OfferOnStartup { get; }

        public string? StartupMessage { get; }

        private readonly BindableBool available = new BindableBool();
        private readonly OsuGameBase game;

        private long lastRestartTimestamp;

        public DesktopVRModeControl(OsuGameBase game, string[] args)
        {
            this.game = game;

            OfferOnStartup = !Enabled && !mode_arguments.Any(args.Contains);

            if (args.Contains(FAILED_ARGUMENT))
                StartupMessage = "VR mode couldn't be started. Check that your headset is connected to SteamVR.";
            else if (args.Contains(ENDED_ARGUMENT))
                StartupMessage = "VR mode ended, as SteamVR was closed.";

            Refresh();
        }

        public void Refresh() => available.Value = IsRuntimeRunning();

        public void Enable() => restart(VR_ARGUMENT);

        public void Disable() => restart(NO_OFFER_ARGUMENT);

        /// <summary>
        /// Leaves VR mode because the headset's runtime ended the session (it was closed).
        /// </summary>
        public void LeaveAsEnded() => restart(ENDED_ARGUMENT, immediately: true);

        /// <summary>
        /// Leaves VR mode because it couldn't be started (or stopped working).
        /// </summary>
        public void LeaveAsFailed() => restart(FAILED_ARGUMENT, immediately: true);

        /// <summary>
        /// Whether the headset's runtime (SteamVR) is running. A headset isn't necessarily connected to it.
        /// </summary>
        public static bool IsRuntimeRunning()
        {
            // For developing against other runtimes (e.g. Monado).
            if (Environment.GetEnvironmentVariable("TORII_VR_AVAILABLE") == "1")
                return true;

            try
            {
                // SteamVR's server process, on Windows and Linux alike. It isn't started just to find out: that would open SteamVR.
                var processes = Process.GetProcessesByName("vrserver");

                foreach (var process in processes)
                    process.Dispose();

                return processes.Length > 0;
            }
            catch (Exception e)
            {
                Logger.Log($"Could not check whether SteamVR is running: {e.Message}");
                return false;
            }
        }

        /// <param name="modeArgument">The argument to restart with.</param>
        /// <param name="immediately">
        /// Whether to exit right away rather than through the main menu, which the game may never get to with nothing being displayed.
        /// </param>
        private void restart(string modeArgument, bool immediately = false)
        {
            // Asked for more than once (e.g. the session both ended and failed). Not for good though: the exit may yet be cancelled
            // (see MainMenu's confirmation while operations are ongoing).
            if (lastRestartTimestamp != 0 && Stopwatch.GetElapsedTime(lastRestartTimestamp).TotalSeconds < 5)
                return;

            string? processPath = Environment.ProcessPath;

            if (string.IsNullOrEmpty(processPath))
            {
                Logger.Log("Can't switch VR mode: the game's executable is unknown.", level: LogLevel.Important);
                return;
            }

            lastRestartTimestamp = Stopwatch.GetTimestamp();

            var arguments = new List<string>();

            // Started through the dotnet host rather than the game's own executable.
            string[] commandLine = Environment.GetCommandLineArgs();
            if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase) && commandLine.Length > 0)
                arguments.Add(commandLine[0]);

            // Whatever else the game was started with still applies, except files it was asked to open, which it has by now.
            arguments.AddRange(commandLine.Skip(1).Where(a => a.StartsWith('-') && !mode_arguments.Contains(a) && !a.StartsWith(WAIT_ARGUMENT, StringComparison.Ordinal)));
            arguments.Add(modeArgument);
            arguments.Add($"{WAIT_ARGUMENT}{Environment.ProcessId}");

            Logger.Log($"Restarting the game with {modeArgument}.");

            game.RestartOnExitAction = () =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = processPath,
                        UseShellExecute = false,
                        WorkingDirectory = Environment.CurrentDirectory,
                    };

                    foreach (string argument in arguments)
                        startInfo.ArgumentList.Add(argument);

                    if (Environment.GetEnvironmentVariable(FORCED_SDL3_VARIABLE) == "1")
                    {
                        startInfo.Environment.Remove("OSU_SDL3");
                        startInfo.Environment.Remove(FORCED_SDL3_VARIABLE);
                    }

                    Process.Start(startInfo)?.Dispose();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to restart the game to switch VR mode.");
                }
            };

            if (immediately)
            {
                game.Exit();
                return;
            }

            // Switching was the user's choice already: no "are you sure?".
            game.ExitConfirmed = true;
            game.AttemptExit();
        }
    }
}
