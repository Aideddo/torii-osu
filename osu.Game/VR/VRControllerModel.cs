// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using System.IO;
using System.Numerics;
using osu.Framework.Platform.OpenXR;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: what is shown in your hands in VR.
    /// </summary>
    public enum VRControllerModel
    {
        /// <summary>
        /// The controllers themselves, with the pointer coming out of their front.
        /// </summary>
        [Description("Controllers")]
        Controller,

        /// <summary>
        /// A tablet pen held in each hand, with the pointer coming out of its tip.
        /// </summary>
        [Description("Pen")]
        Pen,
    }

    public static class VRModels
    {
        private static byte[]? pen;

        /// <summary>
        /// The pen model (binary glTF): its origin is the tip, it points along -Z and its side button faces +Y.
        /// </summary>
        /// <remarks>
        /// "Graphic tablet" by RoughnessPub (https://sketchfab.com/3d-models/graphic-tablet-6b30ea789a0e4a36a8a300e020aece2c), CC BY 4.0;
        /// pen only, re-centred and simplified. See THIRD-PARTY-NOTICES.txt.
        /// </remarks>
        public static byte[] Pen => pen ??= load("osu.Game.VR.Models.pen.glb");

        private static XrPlacedModel? controllerLeft;
        private static XrPlacedModel? controllerRight;

        // Where the controller models' origin and axes are in a controller's grip pose: found by lining their buttons, trigger and
        // handle up with a model the Quest gives of its own controllers. The same for both, which mirror each other.
        private static readonly Vector3 controller_position = new Vector3(0, 0, -0.02f);
        private static readonly Quaternion controller_rotation = Quaternion.Normalize(new Quaternion(0, 0.9135f, -0.4067f, 0));

        /// <summary>
        /// A controller for the left hand, drawn where a controller is and there's no model of the actual one: the hands of the player
        /// of a replay, and the user's own on runtimes which have no model of their controllers.
        /// </summary>
        /// <remarks>
        /// "Meta Quest Pro Left Controller" by mreimer (https://sketchfab.com/3d-models/meta-quest-pro-left-controller-b65700705dec4df9a12dd9e31933fb62),
        /// CC BY-ND 4.0: the file is the author's, unchanged; it's only placed when drawn. See THIRD-PARTY-NOTICES.txt.
        /// </remarks>
        public static XrPlacedModel ControllerLeft
            => controllerLeft ??= new XrPlacedModel(load("osu.Game.VR.Models.meta_quest_pro_left_controller.glb"), controller_position, controller_rotation);

        /// <summary>
        /// A controller for the right hand. See <see cref="ControllerLeft"/>.
        /// </summary>
        /// <remarks>
        /// "Meta Quest Pro Right Controller" by mreimer (https://sketchfab.com/3d-models/meta-quest-pro-right-controller-76a1fbb0530842eea882019bcfe51b79),
        /// CC BY-ND 4.0, unchanged.
        /// </remarks>
        public static XrPlacedModel ControllerRight
            => controllerRight ??= new XrPlacedModel(load("osu.Game.VR.Models.meta_quest_pro_right_controller.glb"), controller_position, controller_rotation);

        private static byte[]? headset;

        /// <summary>
        /// A VR headset (binary glTF), in metres: its origin is the point between the wearer's eyes, it faces -Z and +Y is up.
        /// Shown where the player's head was when a replay of a VR play is watched as a scene.
        /// </summary>
        /// <remarks>
        /// "VR Headset Free Model" by Vitamin (https://sketchfab.com/3d-models/vr-headset-free-model-51b8dbff65e247979f068914f6197909),
        /// CC BY 4.0; scaled, re-centred and turned, with its base colour texture only. See THIRD-PARTY-NOTICES.txt.
        /// </remarks>
        public static byte[] Headset => headset ??= load("osu.Game.VR.Models.headset.glb");

        private static byte[] load(string name)
        {
            using var stream = typeof(VRModels).Assembly.GetManifestResourceStream(name)
                               ?? throw new FileNotFoundException($"Embedded VR model {name} is missing.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }
}
