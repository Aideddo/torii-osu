// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Skinning
{
    /// <summary>
    /// Marker interface for Torii skin components which only make sense in VR.
    /// </summary>
    /// <remarks>
    /// The skin layout editor lists these in their own "VR Components" section above the "Torii Exclusive Components" one,
    /// and only when running in VR. Apply by inheriting on any component that also implements <see cref="ISerialisableDrawable"/>.
    /// </remarks>
    public interface IVRSkinComponent : IToriiSkinComponent
    {
    }
}
