// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Mapperatorinator;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MapperatorinatorSetupTest
    {
        private string root = null!;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), $"torii setup tests {Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(root, true);

        [Test]
        public void TestFallbackWhenPreferredRootCannotBeCreated()
        {
            string blocked = Path.Combine(root, @"blocked");
            File.WriteAllText(blocked, @"keep");
            string fallback = Path.Combine(root, @"user folder");
            var logs = new List<string>();
            Assert.That(MapperatorinatorSetup.WritableRoot(blocked, fallback, logs.Add), Is.EqualTo(fallback));
            Assert.That(File.ReadAllText(blocked), Is.EqualTo(@"keep"));
            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(Directory.EnumerateFileSystemEntries(fallback), Is.Empty);
        }

        [Test]
        public void TestFallbackWhenExistingCheckoutIsBlocked()
        {
            string preferred = Path.Combine(root, @"preferred");
            Directory.CreateDirectory(preferred);
            File.WriteAllText(Path.Combine(preferred, @"Mapperatorinator-main"), @"keep");
            string fallback = Path.Combine(root, @"fallback");
            Assert.That(MapperatorinatorSetup.WritableRoot(preferred, fallback, _ => { }, @"Mapperatorinator-main"), Is.EqualTo(fallback));
            Assert.That(File.ReadAllText(Path.Combine(preferred, @"Mapperatorinator-main")), Is.EqualTo(@"keep"));
        }

        [Test]
        [TestCase(@"tmp")]
        [TestCase(@"pip-cache")]
        [TestCase(@"Mapperatorinator-main/.venv")]
        public void TestExistingSetupDirectoryCannotBeWritten(string relative)
        {
            string preferred = Path.Combine(root, @"preferred");
            string blocked = Path.Combine(preferred, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(blocked)!);
            File.WriteAllText(blocked, @"keep");
            string fallback = Path.Combine(root, @"fallback");
            Assert.That(MapperatorinatorSetup.WritableRoot(preferred, fallback, _ => { }, @"Mapperatorinator-main"), Is.EqualTo(fallback));
            Assert.That(File.ReadAllText(blocked), Is.EqualTo(@"keep"));
        }

        [Test]
        public void TestBothRootsBlocked()
        {
            string blocked = Path.Combine(root, @"blocked");
            File.WriteAllText(blocked, @"keep");
            Assert.Throws<IOException>(() => MapperatorinatorSetup.WritableRoot(blocked, blocked, _ => { }));
        }

        [Test]
        public void TestWriteProbeDoesNotDeleteExistingFiles()
        {
            string oldProbe = Path.Combine(root, @".torii-write-test");
            Directory.CreateDirectory(oldProbe);
            File.WriteAllText(Path.Combine(oldProbe, @"existing"), @"keep");
            Assert.That(MapperatorinatorSetup.CanWriteInside(root), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(oldProbe, @"existing")), Is.EqualTo(@"keep"));
            Assert.That(Directory.EnumerateDirectories(root), Has.Exactly(1).Items);
        }

        [TestCase(@".venv")]
        [TestCase(@"venv")]
        public void TestIncompleteEnvironmentIsNotReady(string environment)
        {
            var runner = new MapperatorinatorRunner(root);
            runner.Config.InstallPath = root;
            File.WriteAllText(Path.Combine(root, @"inference.py"), string.Empty);
            Assert.That(runner.VenvPython, Is.Null);
            string scripts = Path.Combine(root, environment, @"Scripts");
            Directory.CreateDirectory(scripts);
            File.WriteAllText(Path.Combine(scripts, @"python.exe"), string.Empty);
            Assert.That(runner.InstallLooksValid, Is.True);
            Assert.That(runner.PythonExecutable, Is.EqualTo(runner.VenvPython));
            Assert.That(runner.CheckEnvironment(), Is.Not.Null);
            File.WriteAllText(Path.Combine(root, MapperatorinatorSetup.INCOMPLETE_MARKER), @"pending");
            Assert.That(runner.InstallLooksValid, Is.False);
        }

        [Test]
        public void TestLegacyEnvironmentKeepsPriority()
        {
            foreach (string name in new[] { @".venv", @"venv" })
            {
                string scripts = Path.Combine(root, name, @"Scripts");
                Directory.CreateDirectory(scripts);
                File.WriteAllText(Path.Combine(scripts, @"python.exe"), string.Empty);
            }

            var runner = new MapperatorinatorRunner(root);
            runner.Config.InstallPath = root;
            Assert.That(runner.PythonExecutable, Is.EqualTo(Path.Combine(root, @"venv", @"Scripts", @"python.exe")));
            Assert.That(runner.VenvPython, Is.EqualTo(runner.PythonExecutable));
        }

        [Test]
        public async Task TestRealPythonEnvironmentWithoutSystemChanges()
        {
            string? python = MapperatorinatorRunner.FindPython310();
            if (python == null)
                Assert.Ignore(@"64-bit Python 3.10 is not installed on this test machine.");

            string? originalPath = Environment.GetEnvironmentVariable(@"PATH");
            var runner = new MapperatorinatorRunner(root);
            runner.Config.InstallPath = root;
            File.WriteAllText(Path.Combine(root, @"inference.py"), string.Empty);
            await runner.RunSetupStepAsync(python!, new[] { @"-m", @"venv", @".venv" }, root, _ => { }, CancellationToken.None);
            Assert.That(runner.InstallLooksValid, Is.True);
            Assert.That(MapperatorinatorRunner.TryRunProbe(runner.PythonExecutable,
                new[] { @"-c", @"import sys,struct; print(str(sys.version_info.major)+'.'+str(sys.version_info.minor)+':'+str(8*struct.calcsize('P')))" }, root, out string version), Is.True);
            Assert.That(version.Trim(), Is.EqualTo(@"3.10:64"));
            Assert.That(runner.CheckEnvironment(), Does.Contain(@"Missing Python packages").And.Contain(@"torch"));
            Assert.That(Environment.GetEnvironmentVariable(@"PATH"), Is.EqualTo(originalPath));
        }

        [Test]
        public void TestCancelledSetupStepDoesNotStartProcess()
        {
            Assert.CatchAsync<OperationCanceledException>(() => new MapperatorinatorRunner(root).RunSetupStepAsync(
                @"this-executable-does-not-exist", Array.Empty<string>(), root, _ => { }, new CancellationToken(true)));
        }

        [Test]
        [Explicit(@"Downloads the upstream source archive.")]
        public async Task TestRealSourceDownloadAndExtraction()
        {
            string archive = Path.Combine(root, @"source.tar.gz");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            await MapperatorinatorSetup.DownloadAsync(new Uri(@"https://github.com/OliBomby/Mapperatorinator/archive/refs/heads/main.tar.gz"), archive, cancellation.Token);
            await MapperatorinatorSetup.ExtractSourceAsync(archive, root, cancellation.Token);
            Assert.That(File.Exists(Path.Combine(root, @"Mapperatorinator-main", @"inference.py")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, @"Mapperatorinator-main", @"requirements.txt")), Is.True);
        }

        [Test]
        public async Task TestManagedTarExtraction()
        {
            string archive = createArchive(@"Mapperatorinator-main/inference.py");
            string destination = Path.Combine(root, @"extracted");
            Directory.CreateDirectory(destination);
            await MapperatorinatorSetup.ExtractSourceAsync(archive, destination, CancellationToken.None);
            Assert.That(File.ReadAllText(Path.Combine(destination, @"Mapperatorinator-main", @"inference.py")), Is.EqualTo(@"test source"));
        }

        [Test]
        public void TestArchiveCannotEscapeDestination()
        {
            string archive = createArchive(@"../escaped.py");
            string destination = Path.Combine(root, @"extracted");
            Directory.CreateDirectory(destination);
            Assert.ThrowsAsync<IOException>(() => MapperatorinatorSetup.ExtractSourceAsync(archive, destination, CancellationToken.None));
            Assert.That(File.Exists(Path.Combine(root, @"escaped.py")), Is.False);
        }

        [Test]
        public void TestCancelledExtraction()
        {
            string archive = createArchive(@"Mapperatorinator-main/inference.py");
            Assert.CatchAsync<OperationCanceledException>(() => MapperatorinatorSetup.ExtractSourceAsync(archive, root, new CancellationToken(true)));
        }

        [Test]
        public async Task TestSuccessfulDownload()
        {
            using var http = new HttpClient(new ResponseHandler(HttpStatusCode.OK, new StringContent(@"complete")));
            string destination = Path.Combine(root, @"download.zip");
            await MapperatorinatorSetup.DownloadAsync(http, new Uri(@"https://example.test/archive"), destination, CancellationToken.None);
            Assert.That(File.ReadAllText(destination), Is.EqualTo(@"complete"));
            Assert.That(Directory.GetFiles(root, @"*.download"), Is.Empty);
        }

        [Test]
        public void TestFailedDownloadPreservesPreviousFile()
        {
            using var http = new HttpClient(new ResponseHandler(HttpStatusCode.NotFound, new StringContent(@"missing")));
            string destination = Path.Combine(root, @"download.zip");
            File.WriteAllText(destination, @"previous");
            Assert.ThrowsAsync<HttpRequestException>(() => MapperatorinatorSetup.DownloadAsync(http, new Uri(@"https://example.test/archive"), destination, CancellationToken.None));
            Assert.That(File.ReadAllText(destination), Is.EqualTo(@"previous"));
            Assert.That(Directory.GetFiles(root, @"*.download"), Is.Empty);
        }

        [Test]
        public void TestInterruptedDownloadCleansPartialFile()
        {
            using var http = new HttpClient(new ResponseHandler(HttpStatusCode.OK, new InterruptedContent()));
            string destination = Path.Combine(root, @"download.zip");
            File.WriteAllText(destination, @"previous");
            Assert.ThrowsAsync<HttpRequestException>(() => MapperatorinatorSetup.DownloadAsync(http, new Uri(@"https://example.test/archive"), destination, CancellationToken.None));
            Assert.That(File.ReadAllText(destination), Is.EqualTo(@"previous"));
            Assert.That(Directory.GetFiles(root, @"*.download"), Is.Empty);
        }

        [Test]
        public void TestCancelledDownload()
        {
            using var http = new HttpClient(new ResponseHandler(HttpStatusCode.OK, new StringContent(@"complete")));
            Assert.ThrowsAsync<TaskCanceledException>(() => MapperatorinatorSetup.DownloadAsync(http, new Uri(@"https://example.test/archive"), Path.Combine(root, @"download.zip"), new CancellationToken(true)));
            Assert.That(Directory.GetFiles(root), Is.Empty);
        }

        [Test, Platform(@"Win")]
        public void TestProbeDrainsBothPipes()
        {
            Assert.That(MapperatorinatorRunner.TryRunProbe(Environment.GetEnvironmentVariable(@"ComSpec")!,
                new[] { @"/d", @"/c", @"for /L %i in (1,1,5000) do @echo stderr-line 1>&2 & echo complete" }, root, out string output), Is.True);
            Assert.That(output, Does.Contain(@"complete").And.Contain(@"stderr-line"));
        }

        [Test, Platform(@"Win")]
        public void TestProbeTimeout()
        {
            Assert.That(MapperatorinatorRunner.TryRunProbe(Environment.GetEnvironmentVariable(@"ComSpec")!,
                new[] { @"/d", @"/c", @"ping -n 30 127.0.0.1 >nul" }, root, out string output, 200), Is.False);
            Assert.That(output, Does.Contain(@"did not respond"));
        }

        [Test, Platform(@"Win")]
        public void TestFailureIncludesLastOutput()
        {
            var runner = new MapperatorinatorRunner(root);
            var error = Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunSetupStepAsync(Environment.GetEnvironmentVariable(@"ComSpec")!,
                new[] { @"/d", @"/c", @"(for /L %i in (1,1,300) do @echo prefix) & echo final-pip-error & exit /b 1" }, root, _ => { }, CancellationToken.None));
            Assert.That(error!.Message, Does.Contain(@"final-pip-error"));
        }

        [Test, Platform(@"Win")]
        public async Task TestBatchLauncherWithSpaces()
        {
            string batch = Path.Combine(root, @"custom python.cmd");
            File.WriteAllText(batch, "@echo off\r\necho batch-ok\r\nexit /b 0\r\n");
            var lines = new List<string>();
            await new MapperatorinatorRunner(root).RunSetupStepAsync(batch, Array.Empty<string>(), root, lines.Add, CancellationToken.None);
            Assert.That(lines, Does.Contain(@"batch-ok"));
        }

        private string createArchive(string entryName)
        {
            string path = Path.Combine(root, @"source.tar.gz");
            using var file = File.Create(path);
            using var gzip = new GZipStream(file, CompressionLevel.Fastest);
            using var writer = new TarWriter(gzip);
            using var data = new MemoryStream(Encoding.UTF8.GetBytes(@"test source"));
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, entryName) { DataStream = data });
            return path;
        }

        private class ResponseHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode status;
            private readonly HttpContent content;

            public ResponseHandler(HttpStatusCode status, HttpContent content)
            {
                this.status = status;
                this.content = content;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }

        private class InterruptedContent : HttpContent
        {
            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(@"partial"));
                throw new IOException(@"connection interrupted");
            }

            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return false;
            }
        }
    }
}
