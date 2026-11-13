// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings
{
    internal partial class ClassicThemeRequiredDialog : PopupDialog
    {
        public ClassicThemeRequiredDialog()
        {
            HeaderText = "Torii Classic (Stable) required";
            BodyText = "These options only apply to Torii Classic (Stable). Choose your components, then select Enable Torii Classic (Stable) and restart the game.";
            Buttons = new PopupDialogButton[] { new PopupDialogOkButton { Text = "Got it" } };
        }
    }
}
