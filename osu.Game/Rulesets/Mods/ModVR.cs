// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Torii: marks a play as made with VR controllers. Not chosen by the player: applied to the score once they use a controller
    /// during the play (see <see cref="Screens.Play.PlayerVRInputDetector"/>), the way <see cref="ModTouchDevice"/> is for touch.
    /// </summary>
    public class ModVR : Mod, IApplicableMod, IToriiExclusiveMod
    {
        public sealed override string Name => "VR";
        public sealed override string Acronym => "VR";
        public sealed override IconUsage? Icon => OsuIcon.ModVR;
        public sealed override LocalisableString Description => "Automatically applied to plays made with VR controllers.";
        public sealed override ModType Type => ModType.System;
        public sealed override double ScoreMultiplier => 1;
        public sealed override bool ValidForMultiplayer => false;
        public sealed override bool ValidForMultiplayerAsFreeMod => false;
        public sealed override bool AlwaysValidForSubmission => true;

        // Says how the play was made, not how hard it was.
        public override bool Ranked => true;

        public override Type[] IncompatibleMods => new[] { typeof(ICreateReplayData) };
    }
}
