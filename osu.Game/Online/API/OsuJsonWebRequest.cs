// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.IO.Network;

namespace osu.Game.Online.API
{
    public class OsuJsonWebRequest<T> : JsonWebRequest<T>
    {
        public OsuJsonWebRequest(string uri)
            : base(uri)
        {
        }

        public OsuJsonWebRequest()
        {
        }

        protected override string UserAgent => "Torii";

        protected override void PrePerform()
        {
            AddHeader(@"X-Torii-HWID", HardwareFingerprint.Get());
            AddHeader(@"X-Torii-HWID-V2", HardwareFingerprint.GetV2());
            AddHeader(@"X-Torii-Device-Token", HardwareFingerprint.GetDeviceToken());

            string? env = EnvironmentTelemetry.Get();
            if (!string.IsNullOrEmpty(env))
                AddHeader(@"X-Torii-Env", env);

            base.PrePerform();
        }
    }
}
