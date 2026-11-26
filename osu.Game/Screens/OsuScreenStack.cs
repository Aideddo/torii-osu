// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Graphics.Containers;
using osu.Game.VR;

namespace osu.Game.Screens
{
    public partial class OsuScreenStack : ScreenStack
    {
        [Cached]
        private BackgroundScreenStack backgroundScreenStack;

        private readonly ParallaxContainer parallaxContainer;

        protected float ParallaxAmount => parallaxContainer.ParallaxAmount;

        public new MarginPadding Padding
        {
            get => base.Padding;
            set => base.Padding = value;
        }

        private float backgroundTopExtension;

        /// <summary>
        /// Cuanto se estira el FONDO por encima del borde de arriba del stack, sin mover el
        /// contenido.
        /// </summary>
        /// <remarks>
        /// Es para la toolbar. Sin auto-hide, OsuGame le mete padding al ScreenOffsetContainer
        /// para que la barra no tape nada, y eso empuja la escena ENTERA hacia abajo, fondo
        /// incluido: detras de la barra no queda un solo pixel dibujado. Un frost ahi no tiene
        /// nada que desenfocar y sale negro.
        ///
        /// Con esto el fondo llega hasta arriba de todo y el contenido conserva su inset, que
        /// es como se comportan las barras translucidas del sistema operativo. Nada de UI se
        /// tapa y el frost siempre tiene material.
        /// </remarks>
        public float BackgroundTopExtension
        {
            get => backgroundTopExtension;
            set => backgroundTopExtension = value;
        }

        private readonly RenderTargetRedirectContainer backgroundRedirect;

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            if (VRRuntime.IsVR)
                backgroundRedirect.Target = host.GetExternalRenderTarget(VRLayers.BACKGROUND);
        }

        public OsuScreenStack()
        {
            // Torii: in VR, backgrounds are drawn into their own compositor layer placed far behind the game panel.
            InternalChild = backgroundRedirect = new RenderTargetRedirectContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = parallaxContainer = new ParallaxContainer
                {
                    // El alto lo maneja Update para poder sumarle BackgroundTopExtension.
                    // Con la extension en 0 esto es igual que RelativeSizeAxes.Both.
                    RelativeSizeAxes = Axes.X,
                    Child = backgroundScreenStack = new BackgroundScreenStack { RelativeSizeAxes = Axes.Both },
                },
            };

            ScreenPushed += screenPushed;
            ScreenExited += ScreenChanged;
        }

        protected override void Update()
        {
            base.Update();

            parallaxContainer.Y = -backgroundTopExtension;
            parallaxContainer.Height = DrawHeight + backgroundTopExtension;
        }

        public void PushSynchronously(OsuScreen screen)
        {
            LoadComponent(screen);

            Push(screen);
        }

        private void screenPushed(IScreen prev, IScreen next)
        {
            if (LoadState < LoadState.Ready)
            {
                // dependencies must be present to stay in a sane state.
                // this is generally only ever hit by test scenes.
                Schedule(() => screenPushed(prev, next));
                return;
            }

            // create dependencies synchronously to ensure leases are in a sane state.
            ((OsuScreen)next).CreateLeasedDependencies((prev as OsuScreen)?.Dependencies ?? Dependencies);

            ScreenChanged(prev, next);
        }

        protected virtual void ScreenChanged(IScreen prev, IScreen? next)
        {
            setParallax(next);
        }

        private void setParallax(IScreen? next) =>
            // Torii: in VR the background moving with the pointer causes motion sickness; real depth from head movement replaces it.
            parallaxContainer.ParallaxAmount = VRRuntime.IsVR ? 0 : ParallaxContainer.DEFAULT_PARALLAX_AMOUNT * ((next as IOsuScreen)?.BackgroundParallaxAmount ?? 1.0f);
    }
}
