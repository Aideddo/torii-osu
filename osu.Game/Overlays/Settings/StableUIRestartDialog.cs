// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings
{
    internal partial class StableUIRestartDialog : PopupDialog
    {
        public StableUIRestartDialog(OsuGame? game)
        {
            HeaderText = "Restart to apply changes";
            BodyText = "Your Torii Classic settings have been saved. Restart the game to apply the new interface.";
            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = "Restart now",
                    Action = () =>
                    {
                        // sin instalador puede no haber relaunch; el cierre igual es limpio.
                        game?.RestartAppWhenExited();
                        game?.AttemptExit();
                    },
                },
                new PopupDialogCancelButton { Text = "Later" },
            };
        }
    }
}
