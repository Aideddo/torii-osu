// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Overlays.Login
{
    public partial class CreateProfileDialog : PopupDialog
    {
        private readonly Action onProfileCreated;
        private readonly int nextSlotCost;
        private readonly Guid creationRequestId = Guid.NewGuid();
        private LabelledTextBox nameTextBox = null!;
        private LabelledTextBox tagTextBox = null!;
        private OsuCheckbox allowOtherMods = null!;
        private RoundedButton optionalButton = null!;
        private ProfileModSummary requiredSummary = null!;
        private ProfileModSummary optionalSummary = null!;
        private OsuTextFlowContainer errorText = null!;
        private ProfileHoldButton createButton = null!;
        private ProfileCancelButton cancelButton = null!;
        private OsuScrollContainer formScroll = null!;
        private OsuScrollContainer? reviewScroll;
        private OsuTextFlowContainer? reviewError;
        private ProfileModPolicy? reviewedPolicy;
        private string reviewedName = string.Empty;
        private string reviewedTag = string.Empty;
        private bool reviewing;
        private APIMod[] required = Array.Empty<APIMod>();
        private APIMod[] optional = Array.Empty<APIMod>();
        private bool creating;
        private bool picking;
        private int? policyRuleset;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        public CreateProfileDialog(Action onProfileCreated, int nextSlotCost = 0)
        {
            this.onProfileCreated = onProfileCreated;
            this.nextSlotCost = nextSlotCost;
            HeaderText = "New player profile";
            Icon = FontAwesome.Solid.UserPlus;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            MainContent.AutoSizeAxes = Axes.None;
            MainContent.Height = 280;
            MainContent.Add(formScroll = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new ReverseChildIDFillFlowContainer<Drawable>
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = 20 },
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 10),
                    Children = new Drawable[]
                    {
                        nameTextBox = new LabelledTextBox { Label = "Profile name", PlaceholderText = "e.g. CS9 practice", TabbableContentContainer = this },
                        tagTextBox = new LabelledTextBox { Label = "Profile tag", PlaceholderText = "2-8 letters or numbers", TabbableContentContainer = this },
                        button("Choose required mods", () => openPicker(false)),
                        requiredSummary = new ProfileModSummary(),
                        allowOtherMods = new OsuCheckbox { LabelText = "Allow all compatible extra mods", Current = { Value = false } },
                        optionalButton = button("Choose optional mods", () => openPicker(true)),
                        optionalSummary = new ProfileModSummary(),
                        new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 13))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Text = "Required mods and their settings cannot be changed while using this profile. All other mods are blocked unless allowed above. Profile rules cannot be edited after creation.",
                        },
                        errorText = summary(),
                    },
                },
            });
            setFormButtons();
            nameTextBox.Current.BindValueChanged(_ => validate());
            tagTextBox.Current.BindValueChanged(_ => validate());
            allowOtherMods.Current.BindValueChanged(_ => updateSummary(), true);
            validate();
        }

        private RoundedButton button(string text, Action action) => new RoundedButton
        {
            RelativeSizeAxes = Axes.X,
            Height = 36,
            Text = text,
            Action = action,
        };

        private OsuTextFlowContainer summary() => new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Colour = Colour4.FromHex("8fcaff"),
        };

        private void openPicker(bool chooseOptional)
        {
            if (picking || game is not OsuGame client || ruleset.Value == null) return;
            if (policyRuleset.HasValue && policyRuleset != ruleset.Value.OnlineID)
            {
                errorText.Text = "Return to the game mode used for these profile rules.";
                return;
            }
            policyRuleset = ruleset.Value.OnlineID;
            var instance = ruleset.Value.CreateInstance();
            var picker = new ProfileModPicker(selected =>
            {
                picking = false;
                if (chooseOptional) optional = selected;
                else
                {
                    required = selected;
                    var currentRuleset = ruleset.Value.CreateInstance();
                    var policy = makePolicy();
                    policy.AllowOtherMods = true;
                    optional = optional.Where(m => policy.AllowsOptional(m.ToMod(currentRuleset), currentRuleset)).ToArray();
                }
                updateSummary();
                validate();
            }, chooseOptional)
            {
                Ruleset = { Value = ruleset.Value },
                SelectionPolicy = chooseOptional ? new ProfileModPolicy { RulesetId = policyRuleset.Value, Required = required, AllowOtherMods = true } : null,
            };
            picker.SelectedMods.Value = (chooseOptional ? required.Concat(optional) : required).Select(m => m.ToMod(instance)).ToArray();
            picking = true;
            validate();
            client.OpenProfileModPicker(picker);
        }

        private ProfileModPolicy makePolicy() => new ProfileModPolicy
        {
            RulesetId = policyRuleset ?? ruleset.Value?.OnlineID ?? 0,
            Required = required,
            Optional = optional.Select(m => m.Acronym).ToArray(),
            AllowOtherMods = allowOtherMods.Current.Value,
        };

        private void updateSummary()
        {
            requiredSummary.SetMods(required, ruleset.Value, true, "None");
            optionalSummary.SetMods(allowOtherMods.Current.Value ? Array.Empty<APIMod>() : optional, ruleset.Value, false,
                allowOtherMods.Current.Value ? "All compatible mods" : "None - only required mods can be used");
            optionalButton.Enabled.Value = !allowOtherMods.Current.Value;
        }

        private void setFormButtons()
        {
            Buttons = new PopupDialogButton[]
            {
                createButton = new ProfileHoldButton
                {
                    Text = nextSlotCost == 0 ? "Hold to create profile (free)" : $"Hold to create - {nextSlotCost:N0} coins",
                    HideDialogOnAction = false,
                    Action = showReview,
                },
                cancelButton = new ProfileCancelButton { Text = "Cancel" },
            };
        }

        private void showReview()
        {
            if (reviewing || creating || !createButton.Enabled.Value) return;
            reviewedPolicy = JsonConvert.DeserializeObject<ProfileModPolicy>(JsonConvert.SerializeObject(makePolicy()))!;
            reviewedName = nameTextBox.Current.Value.Trim();
            reviewedTag = tagTextBox.Current.Value.Trim();
            reviewing = true;
            HeaderText = "Confirm player profile";
            formScroll.Hide();
            var reviewRequired = new ProfileModSummary();
            var reviewOptional = new ProfileModSummary();
            reviewRequired.SetMods(reviewedPolicy.Required, ruleset.Value, true, "None");
            reviewOptional.SetMods(reviewedPolicy.AllowOtherMods ? Array.Empty<APIMod>() : optional, ruleset.Value, false,
                reviewedPolicy.AllowOtherMods ? "All compatible mods" : "None");
            MainContent.Add(reviewScroll = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Horizontal = 20 },
                    Spacing = new Vector2(0, 12),
                    Children = new Drawable[]
                    {
                        new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 22, weight: FontWeight.SemiBold))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Text = $"{reviewedName} [{reviewedTag}]",
                        },
                        new OsuSpriteText
                        {
                            Text = nextSlotCost == 0 ? "Free profile slot" : $"One-time cost: {nextSlotCost:N0} coins",
                            Font = OsuFont.GetFont(size: 17, weight: FontWeight.SemiBold),
                            Colour = Colour4.FromHex("8fcaff"),
                        },
                        reviewRequired,
                        reviewOptional,
                        new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Text = "These rules are permanent. Hold the button below to confirm.",
                        },
                        reviewError = summary(),
                    },
                },
            });
            Buttons = new PopupDialogButton[]
            {
                createButton = new ProfileHoldButton
                {
                    Text = nextSlotCost == 0 ? "Hold to confirm (free)" : $"Hold to confirm - {nextSlotCost:N0} coins",
                    HideDialogOnAction = false,
                    Action = performCreate,
                },
                cancelButton = new ProfileCancelButton { Text = "Back", HideDialogOnAction = false, Action = returnToForm },
            };
        }

        private void returnToForm()
        {
            if (creating) return;
            reviewing = false;
            reviewedPolicy = null;
            HeaderText = "New player profile";
            if (reviewScroll != null) MainContent.Remove(reviewScroll, disposeImmediately: true);
            reviewScroll = null;
            formScroll.Show();
            setFormButtons();
            validate();
        }

        private void validate()
        {
            string name = nameTextBox.Current.Value.Trim();
            string tag = tagTextBox.Current.Value.Trim();
            createButton.Enabled.Value = !creating && !picking && name.Length is >= 2 and <= 32 && tag.Length is >= 2 and <= 8
                && tag.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9');
        }

        private void performCreate()
        {
            if (creating || !reviewing || reviewedPolicy == null || !createButton.Enabled.Value) return;
            if (game is OsuGame client && !client.CanSwitchPlayerProfile)
            {
                if (reviewError != null) reviewError.Text = "Return to the main menu or song select and leave any multiplayer room before creating a profile.";
                return;
            }
            var policy = reviewedPolicy;
            string name = reviewedName;
            string tag = reviewedTag;
#if DEBUG
            if (SubProfilePreview.Enabled)
            {
                SubProfilePreview.Create(name, tag, "custom", policy);
                Hide();
                onProfileCreated();
                return;
            }
#endif
            creating = true;
            cancelButton.Enabled.Value = false;
            validate();
            errorText.Text = string.Empty;
            var request = new CreateToriiSubProfileRequest(name, tag, "custom", modPolicy: policy, expectedCost: nextSlotCost, requestId: creationRequestId);
            request.Success += response => Schedule(() =>
            {
                api.SwitchSubProfile(response, () => Schedule(() =>
                {
                    Hide();
                    onProfileCreated();
                }), error => Schedule(() =>
                {
                    creating = false;
                    createButton.Enabled.Value = false;
                    cancelButton.Enabled.Value = true;
                    if (reviewError != null) reviewError.Text = "Profile created, but switching failed. Sign in again before playing. " + error.Message;
                }));
            });
            request.Failure += error => Schedule(() =>
            {
                creating = false;
                cancelButton.Enabled.Value = true;
                string message = "Could not create profile. " + error.Message;
                errorText.Text = message;
                if (reviewError != null) reviewError.Text = message;
                validate();
            });
            api.Queue(request);
        }

        protected override void Update()
        {
            base.Update();
            var overlay = this.FindClosestParent<DialogOverlay>();
            if (overlay != null) MainContent.Height = FormHeight(overlay.DrawHeight, DrawHeight - MainContent.DrawHeight);
        }

        public static float FormHeight(float viewportHeight, float chromeHeight) => Math.Clamp(viewportHeight - chromeHeight - 32, 0, 280);
    }
}
