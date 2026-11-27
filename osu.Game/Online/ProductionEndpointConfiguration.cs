// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Online
{
    public class ProductionEndpointConfiguration : EndpointConfiguration
    {
        public ProductionEndpointConfiguration()
        {
            WebsiteUrl = APIUrl = @"https://lazer.shikkesora.com";
            APIClientSecret = @"FGc9GAtyHzeQDshWP5Ah7dega8hJACAJpQtw6OXk";
            APIClientID = "5";
            SpectatorUrl = "https://lazer-api.shikkesora.com/spectator";
            MultiplayerUrl = "https://lazer-api.shikkesora.com/multiplayer";
            MetadataUrl = "https://lazer-api.shikkesora.com/metadata";
            BeatmapSubmissionServiceUrl = "https://lazer-api.shikkesora.com";
        }
    }
}
