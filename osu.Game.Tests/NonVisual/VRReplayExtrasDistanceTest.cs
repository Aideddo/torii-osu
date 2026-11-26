// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Numerics;
using NUnit.Framework;
using osu.Game.VR;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class VRReplayExtrasDistanceTest
    {
        [Test]
        public void TestDistanceOverThePlay()
        {
            // The left hand moves a centimetre every 100 ms; the right one isn't tracked. Measured, it came to twice the path through
            // the frames.
            var extras = new VRReplayExtras { LeftDistance = 2 };

            for (int i = 0; i <= 100; i++)
                extras.Frames.Add(frame(1000 + i * 100, i * 0.01f));

            Assert.That(extras.GetDistanceAt(0), Is.Zero, "before the recording");
            Assert.That(extras.GetDistanceAt(1000), Is.Zero);
            Assert.That(extras.GetDistanceAt(6000), Is.EqualTo(1).Within(0.001));
            Assert.That(extras.GetDistanceAt(6050), Is.EqualTo(1.01).Within(0.001), "between frames");
            Assert.That(extras.GetDistanceAt(11000), Is.EqualTo(2).Within(0.001));
            Assert.That(extras.GetDistanceAt(50000), Is.EqualTo(2).Within(0.001), "after the recording");

            // Asking out of order (seeking) gives the same answers.
            Assert.That(extras.GetDistanceAt(6000), Is.EqualTo(1).Within(0.001));
        }

        [Test]
        public void TestMovementDuringAPauseIsNotCounted()
        {
            var extras = new VRReplayExtras { LeftDistance = 0.2 };

            for (int i = 0; i <= 10; i++)
                extras.Frames.Add(frame(i * 100, i * 0.01f));

            // Paused for a while, with the hand ending up elsewhere.
            for (int i = 0; i <= 10; i++)
                extras.Frames.Add(frame(5000 + i * 100, 3 + i * 0.01f));

            Assert.That(extras.GetDistanceAt(1000), Is.EqualTo(0.1).Within(0.001));
            Assert.That(extras.GetDistanceAt(5000), Is.EqualTo(0.1).Within(0.001));
            Assert.That(extras.GetDistanceAt(6000), Is.EqualTo(0.2).Within(0.001));
        }

        [Test]
        public void TestNoFrames()
        {
            Assert.That(new VRReplayExtras { LeftDistance = 5 }.GetDistanceAt(1000), Is.Zero);
        }

        private static VRReplayFrame frame(double time, float x) => new VRReplayFrame
        {
            Time = time,
            Left = new VRReplayHand { Grip = new VRPose(new Vector3(x, 1, -0.3f), Quaternion.Identity) },
        };
    }
}
