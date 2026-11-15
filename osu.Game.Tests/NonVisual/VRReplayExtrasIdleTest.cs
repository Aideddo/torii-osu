// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Numerics;
using NUnit.Framework;
using osu.Game.VR;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class VRReplayExtrasIdleTest
    {
        [Test]
        public void TestHandLeftAloneIsIdle()
        {
            var extras = new VRReplayExtras();

            // The right hand plays and moves about; the left one rests, until it's picked up at 20 s.
            for (int i = 0; i <= 300; i++)
            {
                double time = i * 100;

                extras.Frames.Add(new VRReplayFrame
                {
                    Time = time,
                    ActiveHand = IVRControllers.RIGHT,
                    Left = new VRReplayHand { Grip = new VRPose(new Vector3(time >= 20000 ? 0.2f : -0.3f, 1, -0.3f), Quaternion.Identity) },
                    Right = new VRReplayHand { Grip = new VRPose(new Vector3(0.3f + (i % 2) * 0.05f, 1, -0.3f), Quaternion.Identity) },
                });
            }

            Assert.That(extras.IsHandIdleAt(IVRControllers.LEFT, 4000), Is.False);
            Assert.That(extras.IsHandIdleAt(IVRControllers.LEFT, 10000), Is.True);
            Assert.That(extras.IsHandIdleAt(IVRControllers.LEFT, 21000), Is.False, "picked up again");
            Assert.That(extras.IsHandIdleAt(IVRControllers.RIGHT, 10000), Is.False);

            // Asking out of order (seeking) gives the same answers.
            Assert.That(extras.IsHandIdleAt(IVRControllers.LEFT, 10000), Is.True);
        }
    }
}
