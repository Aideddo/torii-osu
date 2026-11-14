// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using osu.Game.VR;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Torii: the file format of what VR plays save along with their replays.
    /// </summary>
    [TestFixture]
    public class VRReplayExtrasEncoderTest
    {
        [Test]
        public void TestRoundTrip()
        {
            var extras = createExtras(5000);

            var decoded = roundTrip(extras, out long size);

            Assert.That(decoded.ScoreHash, Is.EqualTo(extras.ScoreHash));
            Assert.That(decoded.RecordedAt, Is.EqualTo(extras.RecordedAt));
            Assert.That(decoded.Runtime, Is.EqualTo(extras.Runtime));
            Assert.That(decoded.ControllerModel, Is.EqualTo(VRControllerModel.Pen));
            Assert.That(decoded.PenTilt, Is.EqualTo(40));
            Assert.That(decoded.PenOffsetForward, Is.EqualTo(5.5f));
            Assert.That(decoded.PenOffsetUp, Is.EqualTo(-1));
            Assert.That(decoded.LeftDistance, Is.EqualTo(extras.LeftDistance));
            Assert.That(decoded.RightDistance, Is.EqualTo(extras.RightDistance));
            Assert.That(decoded.LeftPointer, Is.EqualTo(extras.LeftPointer));
            Assert.That(decoded.RightPointer, Is.Null);

            Assert.That(decoded.Panels, Has.Count.EqualTo(2));
            Assert.That(decoded.Panels[1].Time, Is.EqualTo(extras.Panels[1].Time));
            Assert.That(decoded.Panels[1].Geometry, Is.EqualTo(extras.Panels[1].Geometry));

            Assert.That(decoded.Frames, Has.Count.EqualTo(extras.Frames.Count));

            for (int i = 0; i < extras.Frames.Count; i++)
            {
                var expected = extras.Frames[i];
                var actual = decoded.Frames[i];

                Assert.That(actual.Time, Is.EqualTo(expected.Time).Within(VRReplayExtrasEncoder.TIME_UNIT));
                Assert.That(actual.ActiveHand, Is.EqualTo(expected.ActiveHand));

                assertPose(actual.Head, expected.Head, i);
                assertPose(actual.Left.Grip, expected.Left.Grip, i);
                assertPose(actual.Right.Grip, expected.Right.Grip, i);

                Assert.That(actual.Left.Trigger, Is.EqualTo(expected.Left.Trigger).Within(1 / 255f));
                Assert.That(actual.Right.Squeeze, Is.EqualTo(expected.Right.Squeeze).Within(1 / 255f));
            }

            TestContext.WriteLine($"{extras.Frames.Count} frames in {size} bytes ({(double)size / extras.Frames.Count:N1} per frame)");
            Assert.That((double)size / extras.Frames.Count, Is.LessThan(12));
        }

        [Test]
        public void TestJitterIsNotStored()
        {
            var random = new Random(3);
            float jitter() => ((float)random.NextDouble() - 0.5f) * VRReplayExtrasEncoder.POSITION_UNIT * 0.5f;

            // A hand at rest, right on the edge between two stored values, shaking by less than a unit; then moved away.
            var rest = new Vector3(0.10025f, 1.20025f, -0.40025f);
            var extras = new VRReplayExtras();

            for (int i = 0; i < 2000; i++)
            {
                var position = (i < 1000 ? rest : rest + new Vector3(0.05f, 0, 0)) + new Vector3(jitter(), jitter(), jitter());
                extras.Frames.Add(new VRReplayFrame { Time = i * 11, Left = new VRReplayHand { Grip = new VRPose(position, Quaternion.Identity) } });
            }

            var decoded = roundTrip(extras, out long size);

            for (int i = 1; i < 1000; i++)
                Assert.That(decoded.Frames[i].Left.Grip!.Value.Position, Is.EqualTo(decoded.Frames[0].Left.Grip!.Value.Position), $"frame {i}");

            Assert.That(decoded.Frames[1500].Left.Grip!.Value.Position.X, Is.EqualTo(rest.X + 0.05f).Within(VRReplayExtrasEncoder.POSITION_UNIT));

            // Next to nothing for a hand which hardly moves.
            TestContext.WriteLine($"{extras.Frames.Count} frames at rest in {size} bytes");
            Assert.That(size, Is.LessThan(600));
        }

        [Test]
        public void TestReadsWhatANewerBuildAddedTo()
        {
            var extras = createExtras(300);

            // A newer build stores more: a kind of value this one doesn't know, and one it knows twice over.
            var stream = new MemoryStream();
            VRReplayExtrasEncoder.Encode(extras, stream, new[]
            {
                (9999, new byte[] { 1, 2, 3, 4, 5 }),
                ((int)VRReplayExtrasEncoder.Channel.LeftTrigger, new byte[] { 1 }),
            });

            byte[] data = stream.ToArray();

            // And says more about the play: fields this one doesn't know, a controller model it has never heard of.
            int descriptionLength = BitConverter.ToInt32(data, 1);
            string description = System.Text.Encoding.UTF8.GetString(data, 5, descriptionLength);
            string newer = "{\"eye_colour\":\"green\",\"finger_tracking\":{\"joints\":26}," + description[1..].Replace("\"controller_model\":1", "\"controller_model\":57");
            Assert.That(newer, Does.Contain("\"controller_model\":57"));

            byte[] newerDescription = System.Text.Encoding.UTF8.GetBytes(newer);
            var rewritten = new MemoryStream();
            rewritten.WriteByte(data[0]);
            rewritten.Write(BitConverter.GetBytes(newerDescription.Length));
            rewritten.Write(newerDescription);
            rewritten.Write(data, 5 + descriptionLength, data.Length - 5 - descriptionLength);
            rewritten.Position = 0;

            var decoded = VRReplayExtrasEncoder.Decode(rewritten);

            Assert.That(decoded.Frames, Has.Count.EqualTo(extras.Frames.Count));
            Assert.That(decoded.LeftDistance, Is.EqualTo(extras.LeftDistance));
            Assert.That((int)decoded.ControllerModel, Is.EqualTo(57));

            for (int i = 0; i < extras.Frames.Count; i++)
            {
                assertPose(decoded.Frames[i].Left.Grip, extras.Frames[i].Left.Grip, i);
                Assert.That(decoded.Frames[i].Left.Trigger, Is.EqualTo(extras.Frames[i].Left.Trigger).Within(1 / 255f));
            }
        }

        [Test]
        public void TestUnitsAreThoseOfTheFile()
        {
            var extras = createExtras(300);

            // Stored far coarser than this build would: a centimetre, and a tenth of a second.
            extras.PositionUnit = 0.01f;
            extras.TimeUnit = 100;

            var decoded = roundTrip(extras, out _);

            Assert.That(decoded.PositionUnit, Is.EqualTo(0.01f));
            Assert.That(decoded.RotationUnit, Is.EqualTo(VRReplayExtrasEncoder.ROTATION_UNIT));

            for (int i = 0; i < extras.Frames.Count; i++)
            {
                Assert.That(decoded.Frames[i].Time, Is.EqualTo(extras.Frames[i].Time).Within(50.001));

                if (extras.Frames[i].Left.Grip is VRPose expected)
                {
                    var actual = decoded.Frames[i].Left.Grip!.Value;

                    Assert.That(Vector3.Distance(actual.Position, expected.Position), Is.LessThan(0.01f * VRReplayExtrasEncoder.TOLERANCE * 1.8f), $"frame {i}");

                    // On the coarse grid, not this build's own.
                    Assert.That(actual.Position.X / 0.01f, Is.EqualTo(MathF.Round(actual.Position.X / 0.01f)).Within(0.001f), $"frame {i}");
                }
            }
        }

        [Test]
        public void TestDescriptionOnly()
        {
            var extras = createExtras(300);
            var stream = new MemoryStream();
            VRReplayExtrasEncoder.Encode(extras, stream);
            stream.Position = 0;

            var decoded = VRReplayExtrasEncoder.Decode(stream, false);

            Assert.That(decoded.Frames, Is.Empty);
            Assert.That(decoded.LeftDistance, Is.EqualTo(extras.LeftDistance));
            Assert.That(decoded.Panels, Has.Count.EqualTo(2));
        }

        [Test]
        public void TestFrameAtTime()
        {
            var extras = new VRReplayExtras();

            VRReplayFrame frame(double time, float x, bool rightTracked = true) => new VRReplayFrame
            {
                Time = time,
                Head = new VRPose(new Vector3(x, 0, 0), Quaternion.Identity),
                Left = new VRReplayHand { Grip = new VRPose(new Vector3(x, 1, 0), Quaternion.Identity), Trigger = x },
                Right = new VRReplayHand { Grip = rightTracked ? new VRPose(new Vector3(-x, 1, 0), Quaternion.Identity) : null },
                ActiveHand = x >= 1 ? IVRControllers.RIGHT : IVRControllers.LEFT,
            };

            Assert.That(extras.TryGetFrameAt(0, out _), Is.False);

            extras.Frames.Add(frame(100, 0));
            extras.Frames.Add(frame(200, 1));
            extras.Frames.Add(frame(300, 2, false));
            // A long gap: nothing to blend across.
            extras.Frames.Add(frame(5000, 10));

            Assert.That(extras.TryGetFrameAt(99, out _), Is.False);
            Assert.That(extras.TryGetFrameAt(5001, out _), Is.False);

            Assert.That(extras.TryGetFrameAt(100, out var f));
            Assert.That(f.Left.Grip!.Value.Position.X, Is.Zero);

            Assert.That(extras.TryGetFrameAt(150, out f));
            Assert.That(f.Head!.Value.Position.X, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(f.Left.Grip!.Value.Position.X, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(f.Right.Grip!.Value.Position.X, Is.EqualTo(-0.5f).Within(0.0001f));
            Assert.That(f.Left.Trigger, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(f.ActiveHand, Is.EqualTo(IVRControllers.LEFT));

            // Tracking lost by the next frame: stays where it was.
            Assert.That(extras.TryGetFrameAt(250, out f));
            Assert.That(f.Left.Grip!.Value.Position.X, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(f.Right.Grip!.Value.Position.X, Is.EqualTo(-1));
            Assert.That(f.ActiveHand, Is.EqualTo(IVRControllers.RIGHT));

            Assert.That(extras.TryGetFrameAt(2000, out f));
            Assert.That(f.Left.Grip!.Value.Position.X, Is.EqualTo(2));
            Assert.That(f.Right.Grip, Is.Null);

            Assert.That(extras.TryGetFrameAt(5000, out f));
            Assert.That(f.Left.Grip!.Value.Position.X, Is.EqualTo(10));
        }

        [Test]
        public void TestPlayerPose()
        {
            var extras = new VRReplayExtras();

            Assert.That(extras.GetPlayerPose(), Is.Null);

            // Looking a little left and down, then a little right and up, from two places; untracked in between.
            extras.Frames.Add(new VRReplayFrame { Head = new VRPose(new Vector3(0, 1.5f, 0), Quaternion.CreateFromYawPitchRoll(MathF.PI / 2 + 0.2f, -0.3f, 0)) });
            extras.Frames.Add(new VRReplayFrame());
            extras.Frames.Add(new VRReplayFrame { Head = new VRPose(new Vector3(1, 1.7f, 2), Quaternion.CreateFromYawPitchRoll(MathF.PI / 2 - 0.2f, 0.3f, 0)) });

            var pose = extras.GetPlayerPose()!.Value;

            Assert.That(Vector3.Distance(pose.Position, new Vector3(0.5f, 1.6f, 1)), Is.LessThan(0.0001f));

            // A quarter turn to the left faces -X, level.
            Assert.That(Vector3.Distance(Vector3.Transform(-Vector3.UnitZ, pose.Rotation), -Vector3.UnitX), Is.LessThan(0.0001f));

            var tilted = new VRPose(Vector3.One, Quaternion.CreateFromYawPitchRoll(MathF.PI / 2, 0.5f, 0.3f)).Levelled;
            Assert.That(tilted.Position, Is.EqualTo(Vector3.One));
            Assert.That(Vector3.Distance(Vector3.Transform(-Vector3.UnitZ, tilted.Rotation), -Vector3.UnitX), Is.LessThan(0.0001f));
        }

        [Test]
        public void TestPanelAtTime()
        {
            var extras = createExtras(1);

            Assert.That(new VRReplayExtras().GetPanelAt(0), Is.Null);

            // Before the first: the first, there being no other.
            Assert.That(extras.GetPanelAt(-5000), Is.EqualTo(extras.Panels[0].Geometry));
            Assert.That(extras.GetPanelAt(0), Is.EqualTo(extras.Panels[0].Geometry));
            Assert.That(extras.GetPanelAt(2000.5), Is.EqualTo(extras.Panels[1].Geometry));
            Assert.That(extras.GetPanelAt(99999), Is.EqualTo(extras.Panels[1].Geometry));
        }

        [Test]
        public void TestMappingBetweenPanels()
        {
            var recorded = new VRPanelGeometry(new VRPose(new Vector3(0, 1.5f, 0), Quaternion.Identity), 1.5f, 1, 16f / 9, true);

            // The same panel elsewhere, turned a quarter to the left (its middle is along -X).
            var moved = recorded with { Pose = new VRPose(new Vector3(3, 1, -2), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2)) };

            // A hand 40 cm in front of the middle of the panel, 10 cm to its right, pointing at it.
            var hand = new VRPose(new Vector3(0.1f, 1.5f, -1.1f), Quaternion.Identity);

            var mapped = moved.MapFrom(recorded, hand);

            Assert.That(Vector3.Distance(mapped.Position, new Vector3(3 - 1.1f, 1, -2 - 0.1f)), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(Vector3.Transform(-Vector3.UnitZ, mapped.Rotation), -Vector3.UnitX), Is.LessThan(0.0001f));

            // Twice the size at twice the distance: twice as far from the middle of the panel.
            var larger = recorded with { Radius = 3 };
            mapped = larger.MapFrom(recorded, hand);

            Assert.That(Vector3.Distance(mapped.Position, new Vector3(0.2f, 1.5f, -3 + 0.8f)), Is.LessThan(0.0001f));
            Assert.That(recorded.MapFrom(recorded, hand).Position.X, Is.EqualTo(hand.Position.X).Within(0.0001f));
        }

        [Test]
        public void TestNoFrames()
        {
            var decoded = roundTrip(new VRReplayExtras(), out _);

            Assert.That(decoded.Frames, Is.Empty);
            Assert.That(decoded.Panels, Is.Empty);
            Assert.That(decoded.LeftPointer, Is.Null);
        }

        [Test]
        public void TestUnusableValuesAreSaved()
        {
            var extras = new VRReplayExtras();
            extras.Frames.Add(new VRReplayFrame
            {
                Head = new VRPose(new Vector3(float.NaN, float.PositiveInfinity, 1e30f), default),
                Left = new VRReplayHand { Trigger = float.NaN, Squeeze = 7 },
            });

            var frame = roundTrip(extras, out _).Frames[0];

            Assert.That(float.IsFinite(frame.Head!.Value.Position.Length()));
            Assert.That(frame.Head!.Value.Rotation, Is.EqualTo(Quaternion.Identity));
            Assert.That(frame.Left.Trigger, Is.Zero);
            Assert.That(frame.Left.Squeeze, Is.EqualTo(1));
        }

        [Test]
        public void TestDamagedFiles()
        {
            var stream = new MemoryStream();
            VRReplayExtrasEncoder.Encode(createExtras(200), stream);
            byte[] data = stream.ToArray();

            Assert.Throws<InvalidDataException>(() => VRReplayExtrasEncoder.Decode(new MemoryStream(new byte[] { 99, 2, 3, 4, 5, 6, 7, 8 })));
            Assert.Throws<InvalidDataException>(() => VRReplayExtrasEncoder.Decode(new MemoryStream(data, 0, 40)));
            Assert.Throws<InvalidDataException>(() => VRReplayExtrasEncoder.Decode(new MemoryStream(data, 0, data.Length - 100)));

            byte[] newer = (byte[])data.Clone();
            newer[0]++;
            Assert.Throws<InvalidDataException>(() => VRReplayExtrasEncoder.Decode(new MemoryStream(newer)));
        }

        private static VRReplayExtras roundTrip(VRReplayExtras extras, out long size)
        {
            var stream = new MemoryStream();
            VRReplayExtrasEncoder.Encode(extras, stream);
            size = stream.Length;

            stream.Position = 0;
            return VRReplayExtrasEncoder.Decode(stream);
        }

        private static void assertPose(VRPose? actual, VRPose? expected, int frame)
        {
            Assert.That(actual.HasValue, Is.EqualTo(expected.HasValue), $"frame {frame}");

            if (actual is not VRPose a || expected is not VRPose e)
                return;

            // (Within the tolerance on each axis.)
            Assert.That(Vector3.Distance(a.Position, e.Position), Is.LessThan(VRReplayExtrasEncoder.POSITION_UNIT * VRReplayExtrasEncoder.TOLERANCE * 1.8f), $"frame {frame}");

            // The same rotation, whatever the signs: within a tenth of a degree.
            float dot = MathF.Min(MathF.Abs(Quaternion.Dot(a.Rotation, Quaternion.Normalize(e.Rotation))), 1);
            Assert.That(2 * MathF.Acos(dot) * 180 / MathF.PI, Is.LessThan(0.1f), $"frame {frame}");
        }

        /// <summary>
        /// A play at 90 frames per second: the head sways, the hands swing about and shake a little, as tracking does.
        /// </summary>
        private static VRReplayExtras createExtras(int frames)
        {
            var random = new Random(1);
            float noise(float amount) => ((float)random.NextDouble() - 0.5f) * amount;

            var extras = new VRReplayExtras
            {
                ScoreHash = "0123abcd",
                RecordedAt = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.FromHours(2)),
                Runtime = "Test runtime",
                ControllerModel = VRControllerModel.Pen,
                PenTilt = 40,
                PenOffsetForward = 5.5f,
                PenOffsetUp = -1,
                LeftDistance = 12.5,
                RightDistance = 31.25,
                LeftPointer = new VRPose(new Vector3(0, -0.02f, -0.05f), Quaternion.CreateFromYawPitchRoll(0, -0.7f, 0)),
            };

            extras.Panels.Add(new VRReplayPanel { Time = -1000, Geometry = new VRPanelGeometry(new VRPose(new Vector3(0, 1.5f, 0), Quaternion.Identity), 1.5f, 1.05f, 16f / 9, true) });
            extras.Panels.Add(new VRReplayPanel { Time = 2000.5, Geometry = new VRPanelGeometry(new VRPose(new Vector3(0.1f, 1.4f, 0.2f), Quaternion.CreateFromYawPitchRoll(0.2f, 0.1f, 0)), 2, 0.9f, 16f / 9, false) });

            for (int i = 0; i < frames; i++)
            {
                float t = i / 90f;

                VRPose hand(float side, float speed) => new VRPose(
                    new Vector3(side * 0.25f + MathF.Sin(t * speed) * 0.2f + noise(0.0004f), 1.2f + MathF.Cos(t * speed * 1.3f) * 0.15f + noise(0.0004f), -0.4f + MathF.Sin(t * 0.7f) * 0.05f + noise(0.0004f)),
                    // Turns right round over time, so the rotation's sign has to be followed.
                    Quaternion.CreateFromYawPitchRoll(MathF.Sin(t * speed) * 0.5f + noise(0.001f), MathF.Cos(t * speed) * 0.4f, t * 0.8f));

                extras.Frames.Add(new VRReplayFrame
                {
                    Time = -1000 + i * (1000.0 / 90) + noise(0.5f),
                    Head = i % 500 == 499 ? null : new VRPose(new Vector3(MathF.Sin(t * 0.5f) * 0.05f, 1.6f + noise(0.0002f), noise(0.0002f)), Quaternion.CreateFromYawPitchRoll(MathF.Sin(t * 0.3f) * 0.2f, -0.1f, 0)),
                    Left = new VRReplayHand { Grip = hand(-1, 5), Trigger = MathF.Abs(MathF.Sin(t * 8)), Squeeze = 0 },
                    // Loses tracking now and then.
                    Right = new VRReplayHand { Grip = i / 300 % 4 == 3 ? null : hand(1, 9), Trigger = i % 20 < 10 ? 1 : 0, Squeeze = 0.5f },
                    ActiveHand = i / 100 % 2,
                });
            }

            return extras;
        }
    }
}
