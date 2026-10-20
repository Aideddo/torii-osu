// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Mapperatorinator
{
    internal static class MapperatorinatorSetup
    {
        public const string INCOMPLETE_MARKER = @".torii-setup-incomplete";

        public static string WritableRoot(string preferred, string fallback, Action<string> log, string? childDirectory = null)
        {
            bool writable(string path)
            {
                if (!CanWriteInside(path))
                    return false;
                if (childDirectory == null)
                    return true;

                string checkout = Path.Combine(path, childDirectory);
                if (!CanWriteInside(checkout))
                    return false;

                // el checkout puede ser nuestro y el venv o cache venir de un intento como admin.
                foreach (string relative in new[] { @"tmp", @"pip-cache", Path.Combine(childDirectory, @".venv"), Path.Combine(childDirectory, @"venv") })
                {
                    string existing = Path.Combine(path, relative);
                    if ((Directory.Exists(existing) || File.Exists(existing)) && !CanWriteInside(existing))
                        return false;
                }

                return true;
            }

            if (writable(preferred))
                return preferred;

            if (!string.Equals(Path.GetFullPath(preferred), Path.GetFullPath(fallback), StringComparison.OrdinalIgnoreCase)
                && writable(fallback))
            {
                log($"Can't write to {preferred}; using your user folder: {fallback}");
                return fallback;
            }

            throw new IOException($"Torii can't write to {preferred} or {fallback}. Choose a writable folder; running the game as administrator is not required.");
        }

        public static bool CanWriteInside(string root)
        {
            string probe = Path.Combine(root, $".torii-write-test-{Guid.NewGuid():N}");
            string file = Path.Combine(probe, @"probe");
            try
            {
                Directory.CreateDirectory(probe);
                File.WriteAllText(file, @"ok");
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                try
                {
                    File.Delete(file);
                    if (Directory.Exists(probe))
                        Directory.Delete(probe);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public static async Task DownloadAsync(Uri uri, string destination, CancellationToken cancellation)
        {
            using var http = new HttpClient();
            await DownloadAsync(http, uri, destination, cancellation).ConfigureAwait(false);
        }

        internal static async Task DownloadAsync(HttpClient http, Uri uri, string destination, CancellationToken cancellation)
        {
            string partial = destination + $".{Guid.NewGuid():N}.download";
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                timeout.CancelAfter(TimeSpan.FromMinutes(30));
                using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using (var file = File.Create(partial))
                    await response.Content.CopyToAsync(file, timeout.Token).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                File.Move(partial, destination, true);
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                throw new TimeoutException($"The download from {uri.Host} timed out. Check your connection and retry setup.");
            }
            finally
            {
                try { File.Delete(partial); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public static async Task ExtractSourceAsync(string archive, string destination, CancellationToken cancellation)
        {
            await using var file = File.OpenRead(archive);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            await TarFile.ExtractToDirectoryAsync(gzip, destination, overwriteFiles: true, cancellationToken: cancellation).ConfigureAwait(false);
        }
    }
}
