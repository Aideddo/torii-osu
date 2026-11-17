// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Login;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Configuration
{
    public partial class ProfileModPolicyController : Component
    {
        public readonly Bindable<ProfileModPolicy?> Current = new Bindable<ProfileModPolicy?>();
        private readonly Bindable<IReadOnlyList<Mod>> mods;
        private readonly Bindable<RulesetInfo> ruleset;
        private bool enforcing;
        private int requestVersion;
        private int? loadedForUser;
        private ModSettingChangeTracker? settingsTracker;
        private IBindable<APIUser>? localUser;
        private IBindable<APIState>? apiState;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        public ProfileModPolicyController(Bindable<IReadOnlyList<Mod>> mods, Bindable<RulesetInfo> ruleset)
        {
            this.mods = mods;
            this.ruleset = ruleset;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            mods.BindValueChanged(onModsChanged);
            ruleset.BindValueChanged(onRulesetChanged);
            Current.BindValueChanged(_ => onModsChanged(new ValueChangedEvent<IReadOnlyList<Mod>>(mods.Value, mods.Value)));
            localUser = api.LocalUser.GetBoundCopy();
            localUser.BindValueChanged(_ => reload(), true);
            apiState = api.State.GetBoundCopy();
            apiState.BindValueChanged(_ => Schedule(reload));
        }

        private void onRulesetChanged(ValueChangedEvent<RulesetInfo> _)
        {
            loadedForUser = null;
            reload();
            enforce();
        }

        private void onModsChanged(ValueChangedEvent<IReadOnlyList<Mod>> change)
        {
            settingsTracker?.Dispose();
            settingsTracker = null;
            if (Current.Value != null)
            {
                settingsTracker = new ModSettingChangeTracker(change.NewValue);
                settingsTracker.SettingChanged += _ => enforce();
            }
            enforce();
        }

        private void reload()
        {
#if DEBUG
            if (SubProfilePreview.Enabled)
            {
                Current.Value = SubProfilePreview.GetProfiles().Profiles.FirstOrDefault(p => p.IsActive)?.ModPolicy;
                return;
            }
#endif
            if (!api.IsLoggedIn)
            {
                ++requestVersion;
                loadedForUser = null;
                Current.Value = null;
                return;
            }
            int userId = api.LocalUser.Value.Id;
            if (loadedForUser == userId) return;
            loadedForUser = userId;
            int version = ++requestVersion;
            Current.Value = null;
            var request = new GetToriiSubProfilesRequest(ruleset.Value?.OnlineID ?? 0);
            request.Success += response => Schedule(() =>
            {
                if (version == requestVersion && api.LocalUser.Value.Id == userId)
                    Current.Value = response.Profiles.FirstOrDefault(p => p.ProfileUserId == userId)?.ModPolicy;
            });
            request.Failure += _ => Schedule(() =>
            {
                if (version == requestVersion) loadedForUser = null;
            });
            api.Queue(request);
        }

        private void enforce()
        {
            if (enforcing || Current.Value == null || ruleset.Value == null || mods.Disabled)
                return;
            // el perfil pertenece a un modo; no convertir sus reglas a otro ruleset.
            if (ruleset.Value.OnlineID != Current.Value.RulesetId)
                return;
            enforcing = true;
            try
            {
                var instance = ruleset.Value.CreateInstance();
                var corrected = Current.Value.Enforce(mods.Value, instance);
                if (!mods.Value.SequenceEqual(corrected))
                    mods.Value = corrected;
            }
            finally { enforcing = false; }
        }

        protected override void Dispose(bool isDisposing)
        {
            settingsTracker?.Dispose();
            localUser?.UnbindAll();
            apiState?.UnbindAll();
            ruleset.ValueChanged -= onRulesetChanged;
            mods.ValueChanged -= onModsChanged;
            base.Dispose(isDisposing);
        }
    }
}
