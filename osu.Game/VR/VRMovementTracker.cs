// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Screens.Play;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: tracks how far the VR controllers are moved over a game session (one run of the game), and persists it as a
    /// <see cref="VRSession"/> (see <see cref="VRSessionStore"/>). Cached by VR builds only.
    /// </summary>
    public partial class VRMovementTracker : Component
    {
        /// <summary>
        /// How often changed statistics are written to disk, in milliseconds. A headset can kill the game without notice,
        /// so nothing more than this is ever lost.
        /// </summary>
        public double SaveInterval { get; init; } = 5000;

        /// <summary>
        /// Whether the local user is in gameplay, to count movement during gameplay separately.
        /// </summary>
        public readonly IBindable<LocalUserPlayingState> PlayingState = new Bindable<LocalUserPlayingState>();

        /// <summary>
        /// How far both controllers have moved this session, in metres.
        /// </summary>
        public double SessionDistance => distance[IVRControllers.LEFT] + distance[IVRControllers.RIGHT];

        /// <summary>
        /// How far both controllers have moved today (local time), in metres: in this session since midnight, and in earlier sessions
        /// started today. (A session's distance isn't split by day, so one that ran past midnight counts towards the day it started on.)
        /// </summary>
        public double TodayDistance { get; private set; }

        private DateTime today = DateTime.Now.Date;

        /// <summary>
        /// How far both controllers have moved over all sessions including this one, in metres.
        /// </summary>
        public double AllTimeDistance => previousSessionsDistance + SessionDistance;

        [Resolved]
        private IVRControllers controllers { get; set; } = null!;

        private VRSessionStore store = null!;

        // The sessions before this one, as loaded: saved again along with this one.
        private List<VRSession> previousSessions = new List<VRSession>();

        // Saves run in the background, one after another so that an older one never lands on top of a newer one.
        private Task saveTask = Task.CompletedTask;

        private readonly Guid sessionId = Guid.NewGuid();
        private readonly DateTimeOffset startTime = DateTimeOffset.Now;

        private readonly double[] lastReading = new double[2];
        private readonly double[] distance = new double[2];
        private readonly double[] gameplayDistance = new double[2];

        private double previousSessionsDistance;

        private bool changed;
        private double lastSaveTime;

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            store = new VRSessionStore(storage);
            previousSessions = store.Load();
            previousSessionsDistance = previousSessions.Sum(s => s.LeftDistance + s.RightDistance);
            TodayDistance = previousSessions.Where(s => s.StartTime.LocalDateTime.Date == today).Sum(s => s.LeftDistance + s.RightDistance);

            Logger.Log($"VR controllers have moved {previousSessionsDistance:N1} m over {previousSessions.Count} previous session(s).");

            for (int hand = 0; hand < 2; hand++)
                lastReading[hand] = controllers.GetDistanceMoved(hand);
        }

        protected override void Update()
        {
            base.Update();

            bool playing = PlayingState.Value != LocalUserPlayingState.NotPlaying;

            // Still running at midnight: a new day starts counting from nothing.
            DateTime date = DateTime.Now.Date;

            if (date != today)
            {
                today = date;
                TodayDistance = 0;
            }

            for (int hand = 0; hand < 2; hand++)
            {
                double reading = controllers.GetDistanceMoved(hand);
                double moved = reading - lastReading[hand];
                lastReading[hand] = reading;

                if (moved <= 0)
                    continue;

                distance[hand] += moved;
                TodayDistance += moved;
                if (playing)
                    gameplayDistance[hand] += moved;

                changed = true;
            }

            if (changed && Time.Current - lastSaveTime >= SaveInterval)
            {
                lastSaveTime = Time.Current;
                changed = false;
                var sessions = createSave();

                saveTask = saveTask.ContinueWith(_ =>
                {
                    try
                    {
                        store.Save(sessions);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Failed to save VR session statistics.");
                    }
                }, TaskScheduler.Default);
            }
        }

        /// <summary>
        /// Creates what's to be saved: every session, with the current statistics of this one. Values are captured now, so the
        /// write can run later on another thread.
        /// </summary>
        private List<VRSession> createSave() => new List<VRSession>(previousSessions)
        {
            new VRSession(sessionId, startTime)
            {
                LastUpdated = DateTimeOffset.Now,
                LeftDistance = distance[IVRControllers.LEFT],
                RightDistance = distance[IVRControllers.RIGHT],
                LeftGameplayDistance = gameplayDistance[IVRControllers.LEFT],
                RightGameplayDistance = gameplayDistance[IVRControllers.RIGHT],
            }
        };

        protected override void Dispose(bool isDisposing)
        {
            if (SessionDistance > 0)
            {
                Logger.Log($"VR session: controllers moved {SessionDistance:N1} m ({gameplayDistance[IVRControllers.LEFT] + gameplayDistance[IVRControllers.RIGHT]:N1} m during gameplay), "
                           + $"left {distance[IVRControllers.LEFT]:N1} m, right {distance[IVRControllers.RIGHT]:N1} m.");
            }

            if (changed)
            {
                changed = false;

                try
                {
                    // After any save still under way, so that this one is the last.
                    saveTask.Wait(2000);
                    store.Save(createSave());
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to save VR session statistics on exit.");
                }
            }

            base.Dispose(isDisposing);
        }
    }
}
