// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Scoring;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Torii: the tagged file of extras kept next to a replay, and the store of those files.
    /// </summary>
    [TestFixture]
    public class ReplayExtrasFileTest
    {
        // A kind from a newer build.
        private const ReplayExtrasType unknown_type = (ReplayExtrasType)12345;

        [Test]
        public void TestRoundTrip()
        {
            var file = new ReplayExtrasFile();
            file.Set(ReplayExtrasType.VRTracking, new byte[] { 1, 2, 3 });
            file.Set(unknown_type, new byte[] { 9, 8 });
            file.Set(ReplayExtrasType.VRTracking, new byte[] { 4, 5, 6, 7 });

            byte[] data = encode(file);

            var decoded = ReplayExtrasFile.Decode(new MemoryStream(data));

            Assert.That(decoded.Sections, Has.Count.EqualTo(2));
            Assert.That(decoded.Get(ReplayExtrasType.VRTracking), Is.EqualTo(new byte[] { 4, 5, 6, 7 }));
            Assert.That(decoded.Get(unknown_type), Is.EqualTo(new byte[] { 9, 8 }));

            Assert.That(ReplayExtrasFile.DecodeTypes(new MemoryStream(data)), Is.EqualTo(new[] { ReplayExtrasType.VRTracking, unknown_type }));
        }

        [Test]
        public void TestEmpty()
        {
            byte[] data = encode(new ReplayExtrasFile());

            Assert.That(ReplayExtrasFile.Decode(new MemoryStream(data)).Sections, Is.Empty);
            Assert.That(ReplayExtrasFile.DecodeTypes(new MemoryStream(data)), Is.Empty);
            Assert.That(ReplayExtrasFile.Decode(new MemoryStream(data)).Get(ReplayExtrasType.VRTracking), Is.Null);
        }

        [Test]
        public void TestDamagedFiles()
        {
            var file = new ReplayExtrasFile();
            file.Set(ReplayExtrasType.VRTracking, new byte[100]);
            byte[] data = encode(file);

            foreach (int length in new[] { 0, 3, 4, 9, 12, data.Length - 1 })
            {
                Assert.Throws<InvalidDataException>(() => ReplayExtrasFile.Decode(new MemoryStream(data, 0, length)), $"cut to {length} bytes");
                Assert.Throws<InvalidDataException>(() => ReplayExtrasFile.DecodeTypes(new MemoryStream(data, 0, length)), $"cut to {length} bytes");
            }

            byte[] notExtras = (byte[])data.Clone();
            notExtras[0] = (byte)'X';
            Assert.Throws<InvalidDataException>(() => ReplayExtrasFile.Decode(new MemoryStream(notExtras)));

            byte[] newer = (byte[])data.Clone();
            newer[4]++;
            Assert.Throws<InvalidDataException>(() => ReplayExtrasFile.DecodeTypes(new MemoryStream(newer)));
        }

        [Test]
        public void TestStore()
        {
            using (var storage = new TemporaryNativeStorage("replay-extras-test"))
            {
                const string hash = "0123abcd";
                var store = new ReplayExtrasStore(storage);

                Assert.That(store.GetTypes(hash), Is.Empty);
                Assert.That(store.Has(hash, ReplayExtrasType.VRTracking), Is.False);
                Assert.That(store.Read(hash, ReplayExtrasType.VRTracking), Is.Null);

                // Not the name of a replay: nothing to find, and nothing is written outside the folder.
                Assert.That(store.GetTypes("../escape"), Is.Empty);
                Assert.That(store.GetTypes((string?)null), Is.Empty);
                Assert.That(() => store.Write("../escape", ReplayExtrasType.VRTracking, new byte[1]), Throws.ArgumentException);

                store.Write(hash, unknown_type, new byte[] { 9 });
                store.Write(hash, ReplayExtrasType.VRTracking, new byte[] { 1, 2, 3 });
                store.Write(hash, ReplayExtrasType.VRTracking, new byte[] { 4 });

                Assert.That(store.GetTypes(hash), Is.EquivalentTo(new[] { unknown_type, ReplayExtrasType.VRTracking }));
                Assert.That(store.GetTypes(new ScoreInfo { Hash = hash }), Has.Count.EqualTo(2));
                Assert.That(store.Has(new ScoreInfo { Hash = hash }, ReplayExtrasType.VRTracking));
                Assert.That(store.Read(hash, ReplayExtrasType.VRTracking), Is.EqualTo(new byte[] { 4 }));
                Assert.That(store.Read(hash, unknown_type), Is.EqualTo(new byte[] { 9 }));

                Assert.That(store.GetTypes("ffff"), Is.Empty);
            }
        }

        [Test]
        public void TestStoreExportAndImport()
        {
            using (var storage = new TemporaryNativeStorage("replay-extras-export-test"))
            using (var otherStorage = new TemporaryNativeStorage("replay-extras-import-test"))
            {
                const string hash = "0123abcd";
                var store = new ReplayExtrasStore(storage);
                var other = new ReplayExtrasStore(otherStorage);

                Assert.That(store.Exists(hash), Is.False);

                var nothing = new MemoryStream();
                store.CopyTo(hash, nothing);
                Assert.That(nothing.Length, Is.Zero);

                store.Write(hash, ReplayExtrasType.VRTracking, new byte[] { 1, 2, 3 });
                store.Write(hash, unknown_type, new byte[] { 9 });
                Assert.That(store.Exists(hash));

                var exported = new MemoryStream();
                store.CopyTo(hash, exported);

                // The other install has extras of its own for the replay already: of one kind which is in the file, and one which isn't.
                var otherKind = (ReplayExtrasType)777;
                other.Write(hash, ReplayExtrasType.VRTracking, new byte[] { 5 });
                other.Write(hash, otherKind, new byte[] { 6 });

                exported.Position = 0;
                Assert.That(other.Import(hash, exported));

                Assert.That(other.Read(hash, ReplayExtrasType.VRTracking), Is.EqualTo(new byte[] { 1, 2, 3 }));
                Assert.That(other.Read(hash, unknown_type), Is.EqualTo(new byte[] { 9 }));
                Assert.That(other.Read(hash, otherKind), Is.EqualTo(new byte[] { 6 }));

                // Not an extras file, or one with nothing in it: nothing changes.
                Assert.That(other.Import(hash, new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6 })), Is.False);
                Assert.That(other.Import("ffff", new MemoryStream(encode(new ReplayExtrasFile()))), Is.False);
                Assert.That(other.Exists("ffff"), Is.False);
                Assert.That(other.Read(hash, ReplayExtrasType.VRTracking), Is.EqualTo(new byte[] { 1, 2, 3 }));
            }
        }

        private static byte[] encode(ReplayExtrasFile file)
        {
            var stream = new MemoryStream();
            file.Encode(stream);
            return stream.ToArray();
        }
    }
}
