// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Stable-style piece ordering. Original drawables retain input, transforms and pooling ownership.
    /// The mode is selected when the playfield loads, before any proxy is created.
    /// </summary>
    public partial class OsuVisualOrdering : CompositeDrawable
    {
        private readonly OrderedProxyContainer ordered = new OrderedProxyContainer { RelativeSizeAxes = Axes.Both };
        private readonly List<VisualEntry> entries = new List<VisualEntry>();
        private readonly Dictionary<Drawable, VisualEntry> sources = new Dictionary<Drawable, VisualEntry>();
        private readonly Dictionary<HitObject, int> declarationOrder = new Dictionary<HitObject, int>();
        private readonly Bindable<bool> showHiddenSliders = new Bindable<bool>();
        private ConcurrentQueue<Drawable>? removals = new ConcurrentQueue<Drawable>();
        private IBeatmap? beatmap;
        private bool orderDirty;

        // desactivado hasta corregir los ghosts con notas reutilizadas.
        public bool IsActive => false;
        public bool IsStableRendering => IsActive;

        public OsuVisualOrdering()
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
            InternalChild = ordered;
        }

        [BackgroundDependencyLoader]
        private void load(OsuRulesetConfigManager? config, IBeatmap? beatmap)
        {
            config?.BindWith(OsuRulesetSetting.ShowHiddenSliders, showHiddenSliders);
            this.beatmap = beatmap;
            RefreshBeatmap();
        }

        public void RefreshBeatmap()
        {
            declarationOrder.Clear();
            if (beatmap != null)
            {
                for (int i = 0; i < beatmap.HitObjects.Count; i++)
                    declarationOrder[beatmap.HitObjects[i]] = i;
            }
            orderDirty = true;
        }

        public void Unregister(Drawable source) => Volatile.Read(ref removals)?.Enqueue(source);

        public void Register(Drawable source, DrawableOsuHitObject owner, OsuVisualKind kind, Func<Container?>? fallback = null,
                             float localDepth = 0, bool overlayAboveNumber = true, Drawable? existingProxy = null,
                             Action<Drawable>? addFallback = null, Action<Drawable>? removeFallback = null,
                             Action<Drawable, float>? changeFallbackDepth = null)
        {
            if (!IsActive)
                return;
            ObjectDisposedException.ThrowIf(Volatile.Read(ref removals) == null, this);
            ObjectDisposedException.ThrowIf(owner.IsVisualSourceDisposed, owner);
            if (sources.ContainsKey(source))
                return;

            var entry = new VisualEntry(source, owner, kind, fallback, overlayAboveNumber, addFallback, removeFallback, changeFallbackDepth)
            {
                Gate = new VisualProxy
                {
                    RelativeSizeAxes = Axes.Both,
                    Depth = localDepth,
                    Child = existingProxy ?? source.CreateProxy(),
                },
            };
            entries.Add(entry);
            sources.Add(source, entry);
            refresh(entry);
            orderDirty = true;
        }

        protected override void Update()
        {
            base.Update();
            if (!IsActive)
                return;

            drainRemovals();
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry.Owner.IsVisualSourceDisposed || entry.Gate.IsProxyDisposed)
                    remove(entry);
                else
                    refresh(entry);
            }
            if (orderDirty)
                ordered.InvalidateOrder();
            ordered.RefreshOrder();
            orderDirty = false;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            drainRemovals();
        }

        private void drainRemovals()
        {
            var queue = Volatile.Read(ref removals);
            while (queue != null && queue.TryDequeue(out var source))
            {
                if (sources.TryGetValue(source, out var entry))
                    remove(entry);
            }
        }

        private void remove(VisualEntry entry)
        {
            detach(entry);
            entry.Gate.Dispose();
            sources.Remove(entry.Source);
            entries.Remove(entry);
        }

        private void refresh(VisualEntry entry)
        {
            var owner = entry.Owner;
            var parent = owner.VisualParentHitObject ?? owner;
            var model = owner.HitObject;
            var parentModel = parent.HitObject;
            bool applied = owner.Entry != null && model != null && parentModel != null;
            bool hidden = parentModel is Slider slider && slider.Path.LegacyTrimRemovesAllSegments;
            bool repeatFinished = owner is DrawableSliderRepeat repeat && repeat.AllJudged
                                  && Time.Current >= repeat.HitStateUpdateTime + Math.Min(300, repeat.HitObject.SpanDuration);
            bool approachFinished = entry.Kind == OsuVisualKind.Approach && (owner.Judged || (model != null && Time.Current > model.StartTime + 50));

            // Nested objects stop updating when the parent leaves the hitobject container.
            entry.Gate.Alpha = applied && owner.IsAlive && (ReferenceEquals(parent, owner) || parent.IsAlive)
                               && (showHiddenSliders.Value || !hidden) && !repeatFinished && !approachFinished ? 1 : 0;
            if (!applied)
            {
                entry.Model = null;
                entry.ParentModel = null;
                return;
            }

            int declaration = declarationOrder.GetValueOrDefault(parentModel!);
            if (entry.Kind == OsuVisualKind.Approach && entry.Gate.Depth != declaration)
            {
                if (entry.Gate.Parent == null)
                    entry.Gate.Depth = declaration;
                else if (entry.ChangeFallbackDepth != null && entry.InFallback)
                    entry.ChangeFallbackDepth(entry.Gate, declaration);
                else if (entry.Gate.Parent is Container container)
                    container.ChangeChildDepth(entry.Gate, declaration);
            }

            bool tail = owner is DrawableSliderTail;
            int role = owner switch
            {
                DrawableSliderTail => 0,
                DrawableSliderHead => 2,
                DrawableSliderRepeat => 3,
                _ => 1,
            };
            double time = entry.Kind == OsuVisualKind.Body || tail ? parentModel!.GetEndTime() : model!.StartTime;
            int phase = entry.Kind switch
            {
                OsuVisualKind.Object => -2,
                OsuVisualKind.CircleGroup or OsuVisualKind.CircleBase => 1,
                OsuVisualKind.Number => entry.OverlayAboveNumber ? 2 : 3,
                OsuVisualKind.Overlay => entry.OverlayAboveNumber ? 3 : 2,
                OsuVisualKind.Arrow or OsuVisualKind.Flash => 4,
                OsuVisualKind.Tick => 5,
                _ => 0,
            };

            if (!ReferenceEquals(entry.Model, model) || !ReferenceEquals(entry.ParentModel, parentModel))
            {
                entry.Model = model;
                entry.ParentModel = parentModel;
                entry.EventOrdinal = 0;
                if (!ReferenceEquals(parent, owner))
                {
                    foreach (var nested in parentModel!.NestedHitObjects)
                    {
                        if (ReferenceEquals(nested, model))
                            break;
                        entry.EventOrdinal++;
                    }
                }
            }

            var key = new VisualKey(entry.Kind == OsuVisualKind.Ball ? 1 : 0, double.IsFinite(time) ? time : 0,
                phase, tail ? -declaration : declaration, role, entry.EventOrdinal, (int)entry.Kind);
            if (!key.Equals(entry.Gate.Key))
            {
                entry.Gate.Key = key;
                ordered.InvalidateOrder();
            }

            if (entry.Kind == OsuVisualKind.Approach && entry.AddFallback != null)
            {
                if (!entry.InFallback)
                {
                    detach(entry);
                    entry.AddFallback(entry.Gate);
                    entry.InFallback = true;
                }
            }
            else
            {
                Container? target = entry.Kind == OsuVisualKind.Approach ? entry.Fallback?.Invoke() : ordered;
                if (!ReferenceEquals(entry.Gate.Parent, target))
                {
                    detach(entry);
                    target?.Add(entry.Gate);
                }
            }
        }

        private void detach(VisualEntry entry)
        {
            if (ReferenceEquals(entry.Gate.Parent, ordered))
                ordered.Remove(entry.Gate, false);
            else if (entry.RemoveFallback != null && entry.InFallback)
            {
                entry.RemoveFallback(entry.Gate);
                entry.InFallback = false;
            }
            else if (entry.Gate.Parent is Container container)
                container.Remove(entry.Gate, false);
        }

        protected override void Dispose(bool isDisposing)
        {
            // Unregister may arrive from asynchronous skin disposal after this drawable is gone.
            Interlocked.Exchange(ref removals, null);
            foreach (var entry in entries)
            {
                if (entry.Gate.Parent == null)
                    entry.Gate.Dispose();
            }
            entries.Clear();
            sources.Clear();
            declarationOrder.Clear();
            beatmap = null;
            base.Dispose(isDisposing);
        }

        private sealed class VisualEntry(Drawable source, DrawableOsuHitObject owner, OsuVisualKind kind, Func<Container?>? fallback,
                                         bool overlayAboveNumber, Action<Drawable>? addFallback, Action<Drawable>? removeFallback,
                                         Action<Drawable, float>? changeFallbackDepth)
        {
            public readonly Drawable Source = source;
            public readonly DrawableOsuHitObject Owner = owner;
            public readonly OsuVisualKind Kind = kind;
            public readonly Func<Container?>? Fallback = fallback;
            public readonly bool OverlayAboveNumber = overlayAboveNumber;
            public readonly Action<Drawable>? AddFallback = addFallback;
            public readonly Action<Drawable>? RemoveFallback = removeFallback;
            public readonly Action<Drawable, float>? ChangeFallbackDepth = changeFallbackDepth;
            public VisualProxy Gate = null!;
            public bool InFallback;
            public HitObject? Model;
            public HitObject? ParentModel;
            public int EventOrdinal;
        }

        // Public so headless tests exercise the comparator actually used by the proxy container.
        public readonly record struct VisualKey(int Group, double Time, int Phase, int Declaration, int Role, int EventOrdinal, int Piece) : IComparable<VisualKey>
        {
            public int CompareTo(VisualKey other)
            {
                int result = Group.CompareTo(other.Group);
                if (result == 0) result = other.Time.CompareTo(Time);
                if (result == 0) result = Phase.CompareTo(other.Phase);
                if (result == 0) result = Declaration.CompareTo(other.Declaration);
                if (result == 0) result = Role.CompareTo(other.Role);
                if (result == 0) result = EventOrdinal.CompareTo(other.EventOrdinal);
                if (result == 0) result = Piece.CompareTo(other.Piece);
                return result;
            }
        }

        private partial class VisualProxy : Container
        {
            public VisualKey Key;
            public override bool RemoveWhenNotAlive => false;
            public bool IsProxyDisposed => IsDisposed;

            protected override bool CheckChildrenLife()
            {
                if (Child.IsAlive || Child.LoadState < LoadState.Ready)
                    return false;
                MakeChildAlive(Child);
                return true;
            }
        }

        private partial class OrderedProxyContainer : Container
        {
            private bool orderInvalid;
            public void InvalidateOrder() => orderInvalid = true;

            public void RefreshOrder()
            {
                if (!orderInvalid)
                    return;
                SortInternal();
                orderInvalid = false;
            }

            protected override void AddInternal(Drawable drawable)
            {
                RefreshOrder();
                base.AddInternal(drawable);
            }

            protected override bool RemoveInternal(Drawable drawable, bool disposeImmediately)
            {
                // Container removals use binary search, including after pooled keys change.
                RefreshOrder();
                return base.RemoveInternal(drawable, disposeImmediately);
            }

            protected override int Compare(Drawable x, Drawable y)
            {
                int result = ((VisualProxy)x).Key.CompareTo(((VisualProxy)y).Key);
                return result != 0 ? result : CompareReverseChildID(x, y);
            }
        }
    }
}
