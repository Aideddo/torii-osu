// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.IO;
using osu.Game.Localisation;
using osuTK;
using FontAwesome = osu.Framework.Graphics.Sprites.FontAwesome;

namespace osu.Game.Overlays.FirstRunSetup
{
    [LocalisableDescription(typeof(ToriiSettingsStrings), nameof(ToriiSettingsStrings.FirstRunHeader))]
    public partial class ScreenToriiStorage : WizardScreen
    {
        // torii: las opciones de esta pantalla son sus propios botones. El del pie seria una salida
        // mas, la mas grande de todas y ademas atada a Enter, y encima terminaba el first-run entero
        // salteandose los pasos clasicos, porque en ese momento es el unico paso de la lista.
        public override bool HideNextButton => true;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private FirstRunSetupOverlay? firstRunOverlay { get; set; }

        private OsuTextFlowContainer helperText = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            var intro = new List<Drawable>
            {
                new CircularContainer
                {
                    Size = new Vector2(72),
                    Masking = true,
                    CornerRadius = 18,
                    BorderThickness = 3,
                    BorderColour = OverlayColourProvider.Colour2,
                    Child = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Icon = FontAwesome.Solid.FolderOpen,
                        Size = new Vector2(30),
                        Colour = OverlayColourProvider.Light1,
                    }
                },
                new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Text = ToriiSettingsStrings.NoDetectedLazerFolder,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
            };

            var content = new List<Drawable>
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(12),
                    ChildrenEnumerable = intro,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(10),
                    Children = new Drawable[]
                    {
                        new RoundedButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 55,
                            Text = ToriiSettingsStrings.ChooseLazerFolderManually,
                            Action = showFolderPicker,
                        },
                        new RoundedButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 55,
                            Text = ToriiSettingsStrings.KeepPortable,
                            Action = stayFresh,
                        },
                    }
                },
            };

            content.Add(helperText = new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: 13))
            {
                Text = ToriiSettingsStrings.ChangeLaterInSettings,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Colour = OverlayColourProvider.Content2,
            });

            Content.AddRange(content);
        }

        private void showFolderPicker()
        {
            firstRunOverlay?.AppendFolderPickerStep();
            advanceToNextStep();
        }

        private void stayFresh()
        {
            firstRunOverlay?.AppendClassicSteps();
            advanceToNextStep();
        }

        // torii: el wizard sólo avanza por el botón del footer, y los pasos se leen recién al avanzar,
        // así que lo dejamos scheduleado para que el push del próximo screen no pase adentro del click.
        // torii: el guard no es paranoia. El boton de antes se auto-deshabilitaba al primer click, y al
        // cambiarlo por uno comun eso se perdio: la pantalla saliente sigue recibiendo input mientras se
        // desvanece, asi que dos clicks seguidos avanzaban DOS pasos y se salteaban una pantalla.
        private bool avanzando;

        private void advanceToNextStep()
        {
            if (avanzando)
                return;

            avanzando = true;
            Schedule(() => firstRunOverlay?.AdvanceStep());
        }
    }
}
