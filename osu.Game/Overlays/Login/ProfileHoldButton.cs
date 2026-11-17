// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays.Dialog;
using osuTK.Input;

namespace osu.Game.Overlays.Login
{
    public partial class ProfileHoldButton : PopupDialogButton
    {
        private ConfirmHold hold = null!;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            ButtonColour = colours.Blue;
            var progress = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Width = 0,
                Alpha = 0.35f,
                Blending = BlendingParameters.Additive,
            };
            ColourContainer.Add(progress);
            AddInternal(hold = new ConfirmHold(() => Enabled.Value && IsPresent
                && this.FindClosestParent<PopupDialog>()?.State.Value == Visibility.Visible, () => Action?.Invoke())
            {
                RelativeSizeAxes = Axes.Both,
            });
            hold.Progress.BindValueChanged(e => progress.Width = (float)e.NewValue, true);
            Enabled.BindValueChanged(e => { if (!e.NewValue) hold.Cancel(); });
        }

        // ni click, doble click o atajo numerico pueden saltear el hold.
        protected override bool OnClick(ClickEvent e) => true;

        private partial class ConfirmHold : HoldToConfirmContainer
        {
            private readonly Func<bool> canConfirm;
            private readonly Action confirmed;
            protected override bool AllowMultipleFires => true;

            public ConfirmHold(Func<bool> canConfirm, Action confirmed)
                : base(isDangerousAction: true)
            {
                this.canConfirm = canConfirm;
                this.confirmed = confirmed;
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                if (e.Button != MouseButton.Left || !canConfirm()) return false;
                BeginConfirm();
                return true;
            }

            protected override void OnMouseUp(MouseUpEvent e)
            {
                if (e.Button == MouseButton.Left) Cancel();
                base.OnMouseUp(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                Cancel();
                base.OnHoverLost(e);
            }

            protected override void Update()
            {
                base.Update();
                if (!canConfirm()) Cancel();
            }

            public void Cancel()
            {
                AbortConfirm();
                ClearTransforms();
                ((Bindable<double>)Progress).Value = 0;
            }

            protected override void Confirm()
            {
                if (canConfirm()) confirmed();
                base.Confirm();
            }
        }
    }

    public partial class ProfileCancelButton : PopupDialogCancelButton
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();
            ButtonColour = Colour4.Transparent;
            ColourContainer.Alpha = 0;
            Height = 36;
        }
    }
}
