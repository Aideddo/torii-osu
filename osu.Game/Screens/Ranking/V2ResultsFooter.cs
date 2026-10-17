// Copyright (c) GooGuTeam & Torii. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.ReplayRender;
using osu.Game.Overlays.ScoreNotes;
using osu.Game.Scoring;
using osu.Game.Screens.Footer;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    public partial class V2ResultsFooter : CompositeDrawable
    {
        public const float DESIGN_HEIGHT = ScreenFooter.HEIGHT;

        public Action? BackAction { get; init; }
        public Action? RankingAction { get; init; }
        public Action? DetailsAction { get; init; }
        public Action? RetryAction { get; init; }
        public ScoreInfo? Score { get; init; }
        public bool AllowWatchingReplay { get; init; }

        public readonly BindableBool DetailsVisible = new BindableBool();
        public readonly BindableBool LeaderboardVisible = new BindableBool();

        public ScreenBackButton BackButton { get; private set; } = null!;
        public ScreenFooterButton ExportButton { get; private set; } = null!;
        public ScreenFooterButton RankingButton { get; private set; } = null!;
        public ScreenFooterButton DetailsButton { get; private set; } = null!;
        public ScreenFooterButton ReplayButton { get; private set; } = null!;
        public ScreenFooterButton RetryButton { get; private set; } = null!;

        protected override void Update()
        {
            base.Update();
        }

        private const int padding = 60;
        private const float delay_per_button = 30;

        // ���� ScreenFooter���ر����ֲü�����Ϊ��ťԲ�ǺͶ����ᳬ�� 50px �ĵ����߶�
        public override bool UpdateSubTreeMasking() => false;

        private FillFlowContainer<ScreenFooterButton> buttonsFlow = null!;
        private Box background = null!;
        private ScreenFooterButton screenshotButton = null!;
        private bool screenshotUploadMode;

        private readonly Bindable<DownloadState> replayState = new Bindable<DownloadState>();
        private bool exportWhenDownloaded;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private ScoreModelDownloader downloader { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private OverlayColourProvider? colourProvider { get; set; }

        [Resolved(canBeNull: true)]
        private ReplayRenderOverlay? renderOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private ScoreNoteOverlay? noteOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private IAPIProvider? api { get; set; }

        public V2ResultsFooter()
        {
            RelativeSizeAxes = Axes.X;
            Height = ScreenFooter.HEIGHT;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, ScreenshotManager? screenshots)
        {
            ScreenFooterButton[] buttons =
            {
                ExportButton = new ScreenFooterButton
                {
                    Text = GlobalActionKeyBindingStrings.SaveReplay,
                    Icon = FontAwesome.Solid.Download,
                    AccentColour = colours.Blue2,
                    Hotkey = GlobalAction.ExportReplay,
                    Action = exportReplay,
                },
                screenshotButton = new ScreenFooterButton
                {
                    Text = ResultsScreenStrings.Screenshot,
                    Icon = FontAwesome.Solid.Camera,
                    AccentColour = colours.Pink1,
                    Action = () => _ = screenshots?.TakeScreenshotAsync(),
                    Enabled = { Value = screenshots != null },
                    TooltipText = GlobalActionKeyBindingStrings.TakeScreenshot,
                },
                RankingButton = new ScreenFooterButton
                {
                    Text = SongSelectStrings.Ranking,
                    Icon = FontAwesome.Solid.Sitemap,
                    AccentColour = colours.Purple1,
                    Action = () => RankingAction?.Invoke(),
                    Enabled = { Value = RankingAction != null },
                    TooltipText = SongSelectStrings.Ranking,
                },
                DetailsButton = new ScreenFooterButton
                {
                    Text = ResultsScreenStrings.MoreInfo,
                    Icon = FontAwesome.Solid.InfoCircle,
                    AccentColour = colours.Purple2,
                    Action = () => DetailsAction?.Invoke(),
                    Enabled = { Value = DetailsAction != null },
                    TooltipText = ResultsScreenStrings.MoreInfo,
                },
                ReplayButton = new ReplayOptionsButton(
                    () => Score,
                    watchReplay,
                    () => replayState.Value,
                    renderOverlay,
                    noteOverlay,
                    api)
                {
                    Text = "Replay options",
                    Icon = FontAwesome.Solid.Film,
                    AccentColour = colours.Yellow,
                    Hotkey = GlobalAction.SaveReplay,
                    TooltipText = "Replay options",
                },
                RetryButton = new ScreenFooterButton
                {
                    Text = ResultsScreenStrings.PlayAgain,
                    Icon = FontAwesome.Solid.Redo,
                    AccentColour = colours.Green1,
                    Hotkey = GlobalAction.QuickRetry,
                    Action = () => RetryAction?.Invoke(),
                    Enabled = { Value = RetryAction != null },
                    TooltipText = ResultsScreenStrings.PlayAgain,
                },
            };

            InternalChildren = new Drawable[]
            {
                // 1. ������ɫ�ĵף����� ScreenFooter��
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider?.Background5 ?? Colour4.FromHex("#1C2125"),
                },
                // 2. ��ť��������λ�á������ȫ���� ScreenFooter��
                new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Padding = new MarginPadding { Left = OsuGame.SCREEN_EDGE_MARGIN + ScreenBackButton.BUTTON_WIDTH + padding },
                    AutoSizeAxes = Axes.Both,
                    Child = buttonsFlow = new FillFlowContainer<ScreenFooterButton>
                    {
                        Name = "Visible footer buttons",
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Y = ScreenFooterButton.CORNER_RADIUS,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(7, 0),
                        AutoSizeAxes = Axes.Both,
                    }
                },
                // 3. ���ذ�ť����ȫ���� ScreenFooter��
                BackButton = new ScreenBackButton
                {
                    Margin = new MarginPadding { Bottom = OsuGame.SCREEN_EDGE_MARGIN, Left = OsuGame.SCREEN_EDGE_MARGIN },
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Action = () => BackAction?.Invoke(),
                },
                // 4. ȫ�� ESC ���ؼ�����������ȫ���� ScreenFooter��
                new ScreenFooter.BackReceptor
                {
                    OnBackPressed = () => BackButton.TriggerClick(),
                }
            };

            // ���Ӱ�ť������ ScreenFooter һģһ������������볡����
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                // AppearFromBottom fades the button back in, so alpha alone cannot hide unavailable actions.
                if (button == RetryButton && RetryAction == null)
                    continue;

                buttonsFlow.Add(button);

                int index = i;
                button.OnLoadComplete += _ => button.AppearFromBottom(index * delay_per_button);
            }

            if (Score != null && AllowWatchingReplay)
                AddInternal(new ScoreDownloadTracker(Score) { State = { BindTarget = replayState } });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            replayState.BindValueChanged(_ => updateReplayState(), true);
            DetailsVisible.BindValueChanged(visible =>
            {
                DetailsButton.Text = visible.NewValue ? ResultsScreenStrings.ScoreOverview : ResultsScreenStrings.MoreInfo;
                DetailsButton.TooltipText = visible.NewValue ? ResultsScreenStrings.ScoreOverview : ResultsScreenStrings.MoreInfo;
                DetailsButton.OverlayState.Value = visible.NewValue ? Visibility.Visible : Visibility.Hidden;
            }, true);
            LeaderboardVisible.BindValueChanged(visible =>
            {
                RankingButton.OverlayState.Value = visible.NewValue ? Visibility.Visible : Visibility.Hidden;
            }, true);
        }

        private void updateReplayState()
        {
            bool local = replayState.Value == DownloadState.LocallyAvailable;
            bool busy = replayState.Value is DownloadState.Downloading or DownloadState.Importing;
            bool available = AllowWatchingReplay && Score != null && (local || Score.HasOnlineReplay);

            if (replayState.Value == DownloadState.NotDownloaded)
                exportWhenDownloaded = false;

            ExportButton.Enabled.Value = available && !busy;
            ReplayButton.Enabled.Value = !busy && (available || Score?.OnlineID > 0);
            ReplayButton.TooltipText = busy ? ResultsScreenStrings.DownloadingReplay : "Replay options";
            ExportButton.TooltipText = busy ? ResultsScreenStrings.DownloadingReplay : available ? GlobalActionKeyBindingStrings.ExportReplay : ResultsScreenStrings.ReplayUnavailable;

            if (local && exportWhenDownloaded)
            {
                exportWhenDownloaded = false;
                _ = scoreManager.Export(Score!);
            }
        }

        private void watchReplay()
        {
            if (Score == null)
                return;

            if (replayState.Value == DownloadState.LocallyAvailable)
                game?.PresentScore(Score, ScorePresentType.Gameplay);
            else
                downloader.Download(Score);
        }

        private void exportReplay()
        {
            if (!ExportButton.Enabled.Value || Score == null)
                return;

            if (replayState.Value == DownloadState.LocallyAvailable)
                _ = scoreManager.Export(Score);
            else
            {
                exportWhenDownloaded = true;
                downloader.Download(Score);
            }
        }

        public partial class ReplayOptionsButton : ScreenFooterButton, IHasPopover
        {
            private readonly Func<ScoreInfo?> getScore;
            private readonly Action watchReplayAction;
            private readonly Func<DownloadState> getReplayState;
            private readonly ReplayRenderOverlay? renderOverlay;
            private readonly ScoreNoteOverlay? noteOverlay;
            private readonly IAPIProvider? api;

            public ReplayOptionsButton(
                Func<ScoreInfo?> getScore,
                Action watchReplayAction,
                Func<DownloadState> getReplayState,
                ReplayRenderOverlay? renderOverlay,
                ScoreNoteOverlay? noteOverlay,
                IAPIProvider? api)
            {
                this.getScore = getScore;
                this.watchReplayAction = watchReplayAction;
                this.getReplayState = getReplayState;
                this.renderOverlay = renderOverlay;
                this.noteOverlay = noteOverlay;
                this.api = api;

                Action = this.ShowPopover;
            }

            public Popover GetPopover() => new ReplayOptionsPopover(this, getScore(), watchReplayAction, getReplayState(), renderOverlay, noteOverlay, api);
        }

        public partial class ReplayOptionsPopover : OsuPopover
        {
            private readonly ReplayOptionsButton button;

            public ReplayOptionsPopover(
                ReplayOptionsButton button,
                ScoreInfo? score,
                Action watchReplayAction,
                DownloadState replayState,
                ReplayRenderOverlay? renderOverlay,
                ScoreNoteOverlay? noteOverlay,
                IAPIProvider? api)
            {
                this.button = button;

                bool isOwnScore = score?.OnlineID > 0 && score.UserID == api?.LocalUser.Value?.Id;
                bool isRenderable = score?.OnlineID > 0;
                bool canWatch = score != null && (replayState == DownloadState.LocallyAvailable || score.HasOnlineReplay);

                Content.Padding = new MarginPadding(6);

                Child = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(4),
                    Children = new Drawable[]
                    {
                        new ReplayOptionButton("Watch replay", FontAwesome.Solid.Film, Color4Extensions.FromHex("#FFD966"))
                        {
                            Action = () =>
                            {
                                this.HidePopover();
                                watchReplayAction();
                            },
                            Enabled = { Value = canWatch },
                            TooltipText = canWatch ? "Watch this replay" : ResultsScreenStrings.ReplayUnavailable,
                        },
                        new ReplayOptionButton("Render video (o!rdr)", FontAwesome.Solid.Video, Color4Extensions.FromHex("#EB5068"))
                        {
                            Action = () =>
                            {
                                this.HidePopover();
                                if (isRenderable && score != null)
                                    renderOverlay?.ShowFor(score);
                            },
                            Enabled = { Value = isRenderable },
                            TooltipText = isRenderable ? "Render replay to video via o!rdr" : "Replay must be submitted online to render",
                        },
                        new ReplayOptionButton("Add note / comment", FontAwesome.Solid.CommentDots, Color4Extensions.FromHex("#5AA0F5"))
                        {
                            Action = () =>
                            {
                                this.HidePopover();
                                if (score != null)
                                    noteOverlay?.ShowFor(score);
                            },
                            Enabled = { Value = isOwnScore },
                            TooltipText = isOwnScore ? "Add a personal note or comment to this play" : "Notes are only available for your own online scores",
                        },
                    }
                };
            }

            protected override void UpdateState(ValueChangedEvent<Visibility> state)
            {
                base.UpdateState(state);
                button.OverlayState.Value = state.NewValue;
            }
        }

        private partial class ReplayOptionButton : OsuButton, IHasTooltip
        {
            public IconUsage Icon { get; }
            public Color4 Accent { get; }
            public LocalisableString TooltipText { get; set; }

            public ReplayOptionButton(LocalisableString text, IconUsage icon, Color4 accent)
            {
                Text = text;
                Icon = icon;
                Accent = accent;
                Size = new Vector2(230, 42);
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider? colourProvider)
            {
                BackgroundColour = colourProvider?.Background3 ?? Color4Extensions.FromHex("#22282C");
                Content.CornerRadius = 8;
                Content.Masking = true;

                SpriteText.Colour = Color4.White;
                SpriteText.Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold);

                Add(new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(16),
                    X = 14,
                    Icon = Icon,
                    Colour = Accent,
                });
            }

            protected override SpriteText CreateText() => new OsuSpriteText
            {
                Depth = -1,
                Origin = Anchor.CentreLeft,
                Anchor = Anchor.CentreLeft,
                X = 38,
            };
        }
    }
}
