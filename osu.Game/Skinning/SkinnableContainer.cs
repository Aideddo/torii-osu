// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Screens.Play.HUD;
using osu.Framework.Allocation;
using osuTK;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A container which holds many skinnable components, with functionality to add, remove and reload layouts.
    /// Used to allow user customisation of skin layouts.
    /// </summary>
    /// <remarks>
    /// This is currently used as a means of serialising skin layouts to files.
    /// Currently, one json file in a skin will represent one <see cref="SkinnableContainer"/>, containing
    /// the output of <see cref="ISerialisableDrawableContainer.CreateSerialisedInfo"/>.
    /// </remarks>
    public partial class SkinnableContainer : SkinReloadableDrawable, ISerialisableDrawableContainer
    {
        /// <summary>
        /// Invoked when the skinnable components of this container finish loading.
        /// </summary>
        public event Action<Drawable>? OnComponentsLoaded;

        private Container? content;

        /// <summary>
        /// The lookup criteria which will be used to retrieve components from the active skin.
        /// </summary>
        public GlobalSkinnableContainerLookup Lookup { get; }

        public IBindableList<ISerialisableDrawable> Components => components;

        private readonly BindableList<ISerialisableDrawable> components = new BindableList<ISerialisableDrawable>();

        public override bool IsPresent => base.IsPresent || Scheduler.HasPendingTasks; // ensure that components are loaded even if the target container is hidden (ie. due to user toggle).

        public bool ComponentsLoaded { get; private set; }

        private CancellationTokenSource? cancellationSource;

        public SkinnableContainer(GlobalSkinnableContainerLookup lookup)
        {
            Lookup = lookup;
        }

        public void Reload() => Reload((
                CurrentSkin.GetDrawableComponent(new UserSkinComponentLookup(Lookup))
                ?? CurrentSkin.GetDrawableComponent(Lookup))
            as Container);

        public void Reload(Container? componentsContainer)
        {
            ClearInternal();
            components.Clear();
            ComponentsLoaded = false;

            content = componentsContainer ?? new Container
            {
                RelativeSizeAxes = Axes.Both
            };

            cancellationSource?.Cancel();
            cancellationSource = null;

            LoadComponentAsync(content, wrapper =>
            {
                if (Lookup.Lookup == GlobalSkinnableContainers.MainHUDComponents
                    && Dependencies.Get<OsuConfigManager>() is OsuConfigManager config
                    && config.Get<UIThemeOption>(OsuSetting.UITheme) == UIThemeOption.StableUI
                    && ToriiStableUI.GameplayLeaderboard(config))
                    ApplyClassicLeaderboard(wrapper, Lookup.Ruleset == null);
                AddInternal(wrapper);
                components.AddRange(wrapper.Children.OfType<ISerialisableDrawable>());
                ComponentsLoaded = true;
                OnComponentsLoaded?.Invoke(this);
            }, (cancellationSource = new CancellationTokenSource()).Token);
        }

        internal static void ApplyClassicLeaderboard(Container container, bool includeClassic = true)
        {
            foreach (var modern in container.Children.OfType<DrawableGameplayLeaderboard>().ToArray())
                container.Remove(modern, true);

            if (!includeClassic)
            {
                foreach (var classic in container.Children.OfType<LegacyGameplayLeaderboard>().ToArray())
                    container.Remove(classic, true);
                return;
            }

            if (container.Children.OfType<LegacyGameplayLeaderboard>().Any())
                return;

            container.Add(new LegacyGameplayLeaderboard
            {
                Position = new Vector2(5.704895f, 45.772842f),
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                UsesFixedAnchor = false,
            });
        }

        /// <inheritdoc cref="ISerialisableDrawableContainer"/>
        /// <exception cref="NotSupportedException">Thrown when attempting to add an element to a target which is not supported by the current skin.</exception>
        /// <exception cref="ArgumentException">Thrown if the provided instance is not a <see cref="Drawable"/>.</exception>
        public void Add(ISerialisableDrawable component)
        {
            if (content == null)
                throw new NotSupportedException("Attempting to add a new component to a target container which is not supported by the current skin.");

            if (!(component is Drawable drawable))
                throw new ArgumentException($"Provided argument must be of type {nameof(Drawable)}.", nameof(component));

            content.Add(drawable);
            components.Add(component);
        }

        /// <inheritdoc cref="ISerialisableDrawableContainer"/>
        /// <exception cref="NotSupportedException">Thrown when attempting to add an element to a target which is not supported by the current skin.</exception>
        /// <exception cref="ArgumentException">Thrown if the provided instance is not a <see cref="Drawable"/>.</exception>
        public void Remove(ISerialisableDrawable component, bool disposeImmediately)
        {
            if (content == null)
                throw new NotSupportedException("Attempting to remove a new component from a target container which is not supported by the current skin.");

            if (!(component is Drawable drawable))
                throw new ArgumentException($"Provided argument must be of type {nameof(Drawable)}.", nameof(component));

            content.Remove(drawable, disposeImmediately);
            components.Remove(component);
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);

            Reload();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            OnComponentsLoaded = null;
        }
    }
}
