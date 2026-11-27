// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Game.Configuration;

namespace osu.Game.Online
{
    public sealed class TrustedDomainOnlineStore : OnlineStore
    {
        protected override string GetLookupUrl(string url)
        {
            if (IsAllowedUrl(url))
                return url;

            Logger.Log($@"Blocking resource lookup from external website: {url}", LoggingTarget.Network, LogLevel.Verbose);
            return string.Empty;
        }

        /// <summary>
        /// Whether <paramref name="url"/> may be retrieved as an online asset.
        /// </summary>
        /// <param name="url">The absolute URL to check.</param>
        /// <param name="_">Optional config manager (unused in Torii; trusted hosts are strictly pinned).</param>
        public static bool IsAllowedUrl(string? url, OsuConfigManager? _ = null)
        {
            // Trust the Torii server's file host (avatars, team flags, proxied covers/previews, etc.).
            // torii.local es el host del stack de dev; solo resuelve a 127.0.0.1 via el hosts, asi
            // que confiarlo es inofensivo en prod (no resuelve en una maquina normal).
            return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                   && (uri.Host.EndsWith(@".shikkesora.com", StringComparison.OrdinalIgnoreCase)
                       || uri.Host.Equals(@"torii.local", StringComparison.OrdinalIgnoreCase)
                       || uri.Host.EndsWith(@".torii.local", StringComparison.OrdinalIgnoreCase));
        }
    }
}
