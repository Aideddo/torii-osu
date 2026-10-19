// Copyright (c) GooGuTeam & Torii. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Leaderboards;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// Bundled-font rank lettering and accent strokes for the score overview.
    /// Uses high-resolution skinnable rank textures with fallback to font lettering.
    /// </summary>
    public partial class V2RankEmblem : CompositeDrawable
    {
        public bool AutoAppear { get; init; }

        private readonly ScoreRank rank;
        private readonly OsuSpriteText rankText;
        private readonly Sprite rankSprite;
        private readonly Container letterContainer;
        private readonly BufferedContainer glowFlare;
        private readonly Sprite? ghostSprite;
        private readonly Drawable? ghostLetter;
        private readonly Streak[] streaks;

        private bool hasAppeared;

        private readonly Colour4 accent;
        private readonly Colour4 flareColour;

        public V2RankEmblem(ScoreRank rank)
        {
            this.rank = rank;
            Size = new Vector2(284, 271);

            bool isS = rank >= ScoreRank.S;

            switch (rank)
            {
                case ScoreRank.X:
                    accent = Colour4.FromHex("#FFCC22");
                    flareColour = Colour4.FromHex("#FFB800");
                    break;

                case ScoreRank.XH:
                    accent = Colour4.FromHex("#99EEFF");
                    flareColour = Colour4.FromHex("#66DDFF");
                    break;

                case ScoreRank.S:
                    accent = Colour4.FromHex("#FFCC22");
                    flareColour = Colour4.FromHex("#FFB800");
                    break;

                case ScoreRank.SH:
                    accent = Colour4.FromHex("#99EEFF");
                    flareColour = Colour4.FromHex("#66DDFF");
                    break;

                case ScoreRank.A:
                    accent = Colour4.FromHex("#88DA20");
                    flareColour = Colour4.FromHex("#88DA20");
                    break;

                case ScoreRank.B:
                    accent = Colour4.FromHex("#29A6FF");
                    flareColour = Colour4.FromHex("#29A6FF");
                    break;

                case ScoreRank.C:
                    accent = Colour4.FromHex("#FF8E5D");
                    flareColour = Colour4.FromHex("#FF8E5D");
                    break;

                case ScoreRank.D:
                    accent = Colour4.FromHex("#FF5A5A");
                    flareColour = Colour4.FromHex("#FF5A5A");
                    break;

                default:
                    accent = Colour4.FromHex("#888888");
                    flareColour = Colour4.White;
                    break;
            }

            AddInternal(glowFlare = new BufferedContainer(cachedFrameBuffer: true)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                BlurSigma = new Vector2(40),
                Size = new Vector2(400),
                Blending = BlendingParameters.Additive,
                Alpha = 0,
                RedrawOnScale = false,
                Children = new Drawable[]
                {
                    new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(160),
                        Colour = flareColour,
                    },
                    new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(70),
                        Colour = Colour4.White,
                    },
                },
            });

            streaks = new[]
            {
                new Streak(new Vector2(73, 44), 47, 9, accent, true, isS),
                new Streak(new Vector2(90, 88), 77, 21, accent, true, isS),
                new Streak(new Vector2(187, 209), 70, 14, Colour4.White, false, isS),
                new Streak(new Vector2(142, 227), 40, 8, Colour4.White, false, isS),
            };

            AddInternal(streaks[0]);
            AddInternal(streaks[1]);

            letterContainer = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(240, 180),
                Children = new Drawable[]
                {
                    rankSprite = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fit,
                    },
                    rankText = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = DrawableRank.GetRankLetter(rank),
                        Font = OsuFont.Default.With(size: 180, weight: FontWeight.Bold),
                        Colour = ColourInfo.GradientVertical(Colour4.White, accent),
                        Shadow = false,
                        UseFullGlyphHeight = false,
                        Alpha = 0,
                    },
                },
            };

            AddInternal(letterContainer);

            AddInternal(streaks[2]);
            AddInternal(streaks[3]);

            if (isS)
            {
                ghostSprite = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(240, 180),
                    FillMode = FillMode.Fit,
                    Blending = BlendingParameters.Additive,
                    Colour = accent,
                    Alpha = 0,
                };
                ghostLetter = ghostSprite;
                AddInternal(ghostLetter);
            }
        }

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(TextureStore textures, ISkinSource skin)
        {
            string rankLetter = DrawableRank.GetRankLetter(rank);
            var texture = textures.Get($"V2/Ranks/rank-{rankLetter}")
                          ?? textures.Get($"Textures/V2/Ranks/rank-{rankLetter}")
                          ?? textures.Get($"Torii/Ranks/rank-{rankLetter}")
                          ?? skin.GetTexture($"ranking-{rankLetter}")
                          ?? skins.DefaultClassicSkin.GetTexture($"ranking-{rankLetter}");

            if (texture != null)
            {
                rankSprite.Texture = texture;
                rankSprite.Colour = ColourInfo.GradientVertical(Colour4.White, accent);
                if (ghostSprite != null)
                    ghostSprite.Texture = texture;
                rankText.Alpha = 0;
            }
            else
            {
                rankSprite.Alpha = 0;
                rankText.Colour = ColourInfo.GradientVertical(Colour4.White, accent);
                rankText.Alpha = 1;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (AutoAppear && !hasAppeared)
                Appear();
        }

        public void Appear()
        {
            if (hasAppeared)
                return;

            hasAppeared = true;

            double impactTime = rank < ScoreRank.A
                ? (rank <= ScoreRank.D ? 350 : 450)
                : 0;

            if (rank >= ScoreRank.X)
            {
                letterContainer.ScaleTo(1.4f).ScaleTo(1.0f, 600, Easing.OutQuint);
                letterContainer.FadeInFromZero(100, Easing.OutQuint);

                glowFlare.ScaleTo(0.85f).ScaleTo(1.3f, 700, Easing.OutQuint);
                glowFlare.FadeTo(1.0f).FadeOut(700, Easing.OutQuint);

                ghostLetter?.ScaleTo(1.0f).ScaleTo(1.25f, 1000, Easing.OutQuint);
                ghostLetter?.FadeTo(0.9f).FadeOut(1000, Easing.OutQuint);
            }
            else if (rank >= ScoreRank.S)
            {
                letterContainer.ScaleTo(1.35f).ScaleTo(1.0f, 500, Easing.OutQuint);
                letterContainer.FadeInFromZero(100, Easing.OutQuint);

                glowFlare.ScaleTo(0.85f).ScaleTo(1.2f, 550, Easing.OutQuint);
                glowFlare.FadeTo(0.9f).FadeOut(550, Easing.OutQuint);

                ghostLetter?.ScaleTo(1.0f).ScaleTo(1.18f, 800, Easing.OutQuint);
                ghostLetter?.FadeTo(0.75f).FadeOut(800, Easing.OutQuint);
            }
            else if (rank == ScoreRank.A)
            {
                letterContainer.ScaleTo(1.25f).ScaleTo(1.0f, 400, Easing.OutBack);
                letterContainer.FadeInFromZero(80, Easing.OutQuint);

                glowFlare.ScaleTo(0.9f).ScaleTo(1.15f, 400, Easing.OutQuint);
                glowFlare.FadeTo(0.8f).FadeOut(400, Easing.OutQuint);
            }
            else if (rank >= ScoreRank.C)
            {
                letterContainer.MoveToY(-35).MoveToY(0, 450, Easing.OutBounce);
                letterContainer.FadeInFromZero(80, Easing.OutQuint);

                glowFlare.Delay(impactTime).FadeTo(0.45f).FadeOut(350, Easing.OutQuint);
                glowFlare.Delay(impactTime).ScaleTo(0.9f).ScaleTo(1.1f, 350, Easing.OutQuint);
            }
            else
            {
                letterContainer.MoveToY(-40).MoveToY(0, 350, Easing.OutBounce);
                letterContainer.FadeInFromZero(80, Easing.OutQuint);

                letterContainer.Delay(350 + 500)
                               .RotateTo(4.5f, 250, Easing.InOutQuad)
                               .MoveToY(4f, 250, Easing.InOutQuad);
            }

            foreach (var s in streaks)
                s.AnimateIn(impactTime);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            rankText.Scale = new Vector2(Math.Min(1, 180 / Math.Max(1, rankText.DrawWidth)));
        }

        private partial class Streak : CompositeDrawable
        {
            private readonly Vector2 restingPosition;
            private readonly float restingLength;
            private readonly float restingWidth;
            private readonly bool isTopLeft;
            private readonly bool hasSheen;
            private readonly Box sheenBox;

            public Streak(Vector2 position, float length, float width, Colour4 colour, bool isTopLeft, bool hasSheen)
            {
                restingPosition = position;
                restingLength = length;
                restingWidth = width;
                this.isTopLeft = isTopLeft;
                this.hasSheen = hasSheen;

                Position = position;
                Origin = Anchor.Centre;
                Size = new Vector2(length, width);
                Rotation = 45;
                CornerRadius = width / 2;
                Masking = true;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colour,
                    },
                    sheenBox = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.White,
                        Blending = BlendingParameters.Additive,
                        Alpha = 0,
                    },
                };
            }

            public void AnimateIn(double impactTime)
            {
                Vector2 offset = isTopLeft ? new Vector2(-30, -30) : new Vector2(30, 30);
                double delay = impactTime + (isTopLeft ? 50 : 90);

                Position = restingPosition + offset;
                Size = new Vector2(restingLength * 0.25f, restingWidth * 0.4f);
                Alpha = 0;

                using (BeginDelayedSequence(delay))
                {
                    this.FadeIn(60);
                    this.MoveTo(restingPosition, 250, Easing.OutQuint);
                    this.ResizeTo(new Vector2(restingLength, restingWidth), 250, Easing.OutQuint);

                    if (hasSheen)
                    {
                        sheenBox.FadeTo(0.7f)
                                .FadeOut(450, Easing.OutQuint);
                    }
                }
            }
        }
    }
}
