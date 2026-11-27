// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Localisation;
using Realms;

namespace osu.Game.Overlays.FirstRunSetup
{
    [LocalisableDescription(typeof(FirstRunSetupBeatmapScreenStrings), nameof(FirstRunSetupBeatmapScreenStrings.Header))]
    public partial class ScreenBeatmaps : WizardScreen
    {
        private OsuTextFlowContainer currentlyLoadedBeatmaps = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        private IDisposable? beatmapSubscription;

        [BackgroundDependencyLoader]
        private void load()
        {
            Content.Children = new Drawable[]
            {
                new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Colour = OverlayColourProvider.Content1,
                    Text = FirstRunSetupBeatmapScreenStrings.Description,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 30,
                    Children = new Drawable[]
                    {
                        currentlyLoadedBeatmaps = new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: HEADER_FONT_SIZE, weight: FontWeight.SemiBold))
                        {
                            Colour = OverlayColourProvider.Content2,
                            TextAnchor = Anchor.Centre,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            AutoSizeAxes = Axes.Both,
                        },
                    }
                },
                new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Colour = OverlayColourProvider.Content1,
                    Text = FirstRunSetupBeatmapScreenStrings.ObtainMoreBeatmaps,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatmapSubscription = realmAccess.RegisterForNotifications(r => r.All<BeatmapSetInfo>().Where(s => !s.DeletePending && !s.Protected), beatmapsChanged);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            beatmapSubscription?.Dispose();
        }

        private void beatmapsChanged(IRealmCollection<BeatmapSetInfo> sender, ChangeSet? changes) => Schedule(() =>
        {
            currentlyLoadedBeatmaps.Text = FirstRunSetupBeatmapScreenStrings.CurrentlyLoadedBeatmaps(sender.Count);

            if (sender.Count == 0)
            {
                currentlyLoadedBeatmaps.FadeColour(colours.Red1, 500, Easing.OutQuint);
            }
            else if (changes != null && (changes.DeletedIndices.Any() || changes.InsertedIndices.Any()))
            {
                currentlyLoadedBeatmaps.FadeColour(colours.Yellow)
                                       .FadeColour(OverlayColourProvider.Content2, 1500, Easing.OutQuint);

                currentlyLoadedBeatmaps.ScaleTo(1.1f)
                                       .ScaleTo(1, 1500, Easing.OutQuint);
            }
        });
    }
}
