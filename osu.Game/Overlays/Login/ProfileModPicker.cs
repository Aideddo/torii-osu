// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Online.API;
using osu.Game.Overlays.Mods;

namespace osu.Game.Overlays.Login
{
    public partial class ProfileModPicker : UserModSelectOverlay
    {
        private readonly Action<APIMod[]> onDone;
        private readonly bool optional;
        private bool shown;
        protected override bool AllowCustomisation => !optional;

        public ProfileModPicker(Action<APIMod[]> onDone, bool optional)
        {
            this.onDone = onDone;
            this.optional = optional;
            UseProfilePolicy = false;
            IsValidMod = mod => mod.UserPlayable;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Header.Title = optional ? "Optional mods" : "Required mods";
            Header.Description = optional ? "Choose which extra mods this profile can use." : "Selected mods and their settings will be locked to this profile.";
            AddInternal(new RoundedButton
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Margin = new MarginPadding(25),
                Width = 180,
                Height = 45,
                Text = "Done",
                Depth = float.MinValue,
                Action = Hide,
            });
        }

        protected override void PopIn()
        {
            base.PopIn();
            shown = true;
        }

        protected override void PopOut()
        {
            base.PopOut();
            if (!shown) return;
            shown = false;
            onDone(SelectedMods.Value.Where(m => !optional || SelectionPolicy?.IsRequired(m) != true).Select(m => new APIMod(m)).ToArray());
            this.Delay(500).Expire();
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == GlobalAction.Back && !e.Repeat)
            {
                Hide();
                return true;
            }
            return base.OnPressed(e);
        }
    }
}
