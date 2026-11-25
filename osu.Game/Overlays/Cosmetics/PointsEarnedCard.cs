// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Backdrops;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.ToriiBriefing;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Cosmetics
{
    internal partial class PointsEarnedCard : CompositeDrawable
    {
        public readonly record struct Line(int Amount, string Reason, string Ref);

        private const float expanded_width = 340;
        private const float compact_width = 196;
        private const double dock_duration = 360;
        private readonly List<Line> lines;
        private readonly int total;
        private readonly int balanceAfter;
        private readonly bool reducedMotion;
        private CompositeDrawable expandedContent;
        private Container compactContent;
        private Container surface;
        private OsuSpriteText totalText;
        private Sample appearSample;
        private float expandedHeight;
        private double remaining = 6000;
        private double countElapsed;
        private int displayed = -1;
        private Vector2 lastParentSize;
        private bool layoutPending = true;
        private bool hasPosition;

        internal bool IsCompact { get; private set; }
        internal bool IsExiting { get; private set; }

        public PointsEarnedCard(IEnumerable<Line> lines, int balanceAfter, bool reducedMotion)
        {
            this.lines = lines.ToList();
            this.balanceAfter = balanceAfter;
            this.reducedMotion = reducedMotion;
            total = this.lines.Sum(l => l.Amount);
            Size = new Vector2(expanded_width, 120);
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            appearSample = audio.Samples.Get(@"SongSelect/confirm-selection");
            var rows = summarise().ToList();
            var contents = new List<Drawable> { header() };
            contents.AddRange(rows.Take(4).Select(row => breakdownRow(row.label, row.amount, row.icon, row.accent)));
            if (rows.Count > 4)
                contents.Add(breakdownRow($"{rows.Count - 4} more rewards", rows.Skip(4).Sum(r => r.amount), FontAwesome.Solid.Plus, Color4.White.Opacity(0.7f)));
            if (lines.Any(l => l.Reason == "top_play" && parseTagInt(l.Ref, "capped:") == 1))
                contents.Add(note("Daily limit reached: pp bonus only"));
            foreach (int percent in lines.Where(l => l.Reason == "top_play").Select(l => parseTagInt(l.Ref, "alt:")).Where(p => p > 0).Distinct())
                contents.Add(note($"Other ruleset: reward at {percent}%"));
            foreach (int percent in lines.Where(l => l.Reason == "top_play").Select(l => parseTagInt(l.Ref, "rx:")).Where(p => p > 0).Distinct())
                contents.Add(note($"Relax / Autopilot: reward at {percent}%"));
            contents.Add(new Container
            {
                RelativeSizeAxes = Axes.X, Height = 27, Margin = new MarginPadding { Top = 7 },
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.X, Height = 1, Colour = Color4.White.Opacity(0.08f) },
                    new OsuSpriteText { Y = 10, Text = $"Balance  {balanceAfter:N0}", Font = OsuFont.Torus.With(size: 12), Colour = Color4.White.Opacity(0.6f) },
                },
            });
            expandedHeight = 32 + contents.Sum(d => d.Height + d.Margin.TotalVertical);
            Size = IsCompact ? new Vector2(compact_width, 44) : new Vector2(expanded_width, expandedHeight);
            InternalChild = surface = new Container
            {
                RelativeSizeAxes = Axes.Both, Anchor = Anchor.Centre, Origin = Anchor.Centre, Masking = true, CornerRadius = 14,
                BorderThickness = 1, BorderColour = Color4.White.Opacity(0.14f),
                EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Shadow, Colour = Color4.Black.Opacity(0.32f), Radius = 16, Offset = new Vector2(0, 5) },
                Children = new Drawable[]
                {
                    new GlassBackdrop { TintColour = new Color4(12, 14, 22, 210), BlurSigma = 3.5f },
                    expandedContent = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Padding = new MarginPadding(16),
                        Direction = FillDirection.Vertical, Alpha = IsCompact ? 0 : 1, Children = contents.ToArray(),
                    },
                    compactContent = new Container
                    {
                        RelativeSizeAxes = Axes.Both, Alpha = IsCompact ? 1 : 0,
                        Children = new Drawable[]
                        {
                            new SpriteIcon { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, X = 14, Size = new Vector2(16), Icon = FontAwesome.Solid.Coins, Colour = BriefingTheme.AccentAmber },
                            new TruncatingSpriteText { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, X = 40, Text = $"+{total:N0} points", Font = OsuFont.Torus.With(size: 17, weight: FontWeight.SemiBold), MaxWidth = compact_width - 76 },
                        },
                    },
                    new IconButton
                    {
                        Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Position = new Vector2(-6, 6), Size = new Vector2(30),
                        Icon = FontAwesome.Solid.Times, IconScale = new Vector2(0.65f), IconColour = Color4.White.Opacity(0.6f),
                        IconHoverColour = Color4.White, TooltipText = "Dismiss", Action = Dismiss,
                    },
                },
            };
        }

        private Drawable header() => new Container
        {
            RelativeSizeAxes = Axes.X, Height = 48,
            Children = new Drawable[]
            {
                new OsuSpriteText { Text = "Points earned", Font = OsuFont.Torus.With(size: 12), Colour = Color4.White.Opacity(0.6f) },
                new SpriteIcon { Y = 21, Icon = FontAwesome.Solid.Coins, Size = new Vector2(21), Colour = BriefingTheme.AccentAmber },
                totalText = new OsuSpriteText { Position = new Vector2(31, 15), Text = $"+{(reducedMotion ? total : 0):N0}", Font = OsuFont.Torus.With(size: 27, weight: FontWeight.Bold), Colour = BriefingTheme.AccentAmber },
            },
        };

        private static Drawable breakdownRow(string label, int amount, IconUsage icon, Color4 accent) => new Container
        {
            RelativeSizeAxes = Axes.X, Height = 24,
            Children = new Drawable[]
            {
                new SpriteIcon { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Icon = icon, Size = new Vector2(13), Colour = accent },
                new TruncatingSpriteText { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, X = 22, Text = label, Font = OsuFont.Torus.With(size: 14), MaxWidth = 210 },
                new OsuSpriteText { Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, Text = $"+{amount:N0}", Font = OsuFont.Torus.With(size: 14, weight: FontWeight.SemiBold), Colour = accent },
            },
        };

        private static Drawable note(string text) => new Container
        {
            RelativeSizeAxes = Axes.X, Height = 20,
            Child = new TruncatingSpriteText { Text = text, Font = OsuFont.Torus.With(size: 11), Colour = BriefingTheme.AccentAmber.Opacity(0.85f), MaxWidth = 300 },
        };

        private IEnumerable<(string label, int amount, IconUsage icon, Color4 accent)> summarise()
        {
            foreach (var line in lines.Where(l => l.Reason != "medal"))
            {
                var info = PointsReasonInfo.Resolve(line.Reason, line.Ref);
                yield return (labelFor(line), line.Amount, info.Icon, info.Accent);
            }
            var medals = lines.Where(l => l.Reason == "medal").ToList();
            if (medals.Count > 0)
            {
                var info = PointsReasonInfo.Resolve("medal", null);
                yield return (medals.Count == 1 ? "Medal" : $"{medals.Count} medals", medals.Sum(m => m.Amount), info.Icon, info.Accent);
            }
        }

        private static string labelFor(Line line) => line.Reason switch
        {
            "top_play" => parseTagInt(line.Ref, "rank:") is > 0 and var rank ? $"Top play #{rank}" : "Top play",
            "daily_play" => parseTagInt(line.Ref, "streak:") is >= 2 and var streak ? $"{streak} day streak" : "First play today",
            "milestone" => parseTagInt(line.Ref, "pp:") is > 0 and var pp ? $"{pp:N0}pp milestone" : "pp milestone",
            _ => string.IsNullOrEmpty(line.Reason) ? "Points" : char.ToUpperInvariant(line.Reason[0]) + line.Reason.Substring(1).Replace('_', ' '),
        };

        private static int parseTagInt(string reasonRef, string tag)
        {
            foreach (string part in (reasonRef ?? string.Empty).Split('|'))
                if (part.StartsWith(tag, StringComparison.Ordinal) && int.TryParse(part.Substring(tag.Length), out int value))
                    return value;
            return 0;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            appearSample?.Play();
            this.FadeInFromZero(reducedMotion ? 100 : 200, Easing.OutQuint);
            if (!reducedMotion && !IsCompact)
                surface.ScaleTo(0.96f).ScaleTo(1, 380, Easing.OutBack);
            if (IsCompact)
                remaining = 1600;
        }

        public void SetGameplay(bool gameplay)
        {
            // Una vez resumido no vuelve a tapar la pantalla al salir del mapa.
            if (!gameplay || IsCompact || IsExiting)
                return;
            IsCompact = true;
            remaining = 1600;
            layoutPending = true;
            if (IsLoaded)
            {
                surface.ScaleTo(1, reducedMotion ? 0 : 100);
                expandedContent.FadeOut(reducedMotion ? 80 : 120);
                compactContent.Delay(reducedMotion ? 0 : 140).FadeIn(reducedMotion ? 80 : 160);
            }
        }

        public void Dismiss()
        {
            if (IsExiting)
                return;
            IsExiting = true;
            this.ClearTransforms();
            this.FadeOut(reducedMotion ? 80 : 160, Easing.OutQuad).Expire();
        }

        protected override bool OnHover(HoverEvent e) => false;

        protected override void Update()
        {
            base.Update();
            if (IsExiting)
                return;
            if (Parent != null && (layoutPending || Parent.DrawSize != lastParentSize))
            {
                lastParentSize = Parent.DrawSize;
                float width = IsCompact ? compact_width : expanded_width;
                float height = IsCompact ? 44 : expandedHeight;
                float scale = Math.Min(1, Math.Max(0.1f, (lastParentSize.X - 24) / width));
                float x = IsCompact ? lastParentSize.X - width * scale - 16 : (lastParentSize.X - width * scale) / 2;
                float y = Math.Min(64, Math.Max(8, lastParentSize.Y - height * scale - 12));
                double duration = hasPosition && layoutPending && IsCompact && !reducedMotion ? dock_duration : 0;
                this.MoveTo(new Vector2(Math.Max(8, x), y), duration, Easing.OutExpo);
                this.ResizeTo(new Vector2(width, height), duration, Easing.OutExpo);
                this.ScaleTo(scale, duration, Easing.OutExpo);
                hasPosition = true;
                layoutPending = false;
            }
            if (!IsHovered || IsCompact)
                remaining -= Time.Elapsed;
            if (remaining <= 0)
                Dismiss();
            if (!reducedMotion && !IsCompact)
            {
                countElapsed += Time.Elapsed;
                double progress = Math.Clamp(countElapsed / 650, 0, 1);
                int value = (int)Math.Round(total * (1 - Math.Pow(1 - progress, 3)));
                if (value != displayed)
                {
                    displayed = value;
                    totalText.Text = $"+{value:N0}";
                }
            }
        }
    }
}
