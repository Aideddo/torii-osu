# Local Classic UI candidate - 2026-11-14

- Worktree: .tmp/stable-ui-complete, branch port/complete-stable-ui.
- Full visual port conflicts are resolved; changes remain uncommitted.
- Local resources: .tmp/stable-complete-resources. Added PM-RabbitOk2405
  menu-button-background.png and @2x from PopCat19's Project Minimalist.
  Included upstream Zero-Clause BSD notice in resources and desktop LICENSES.
- Existing custom skin textures take precedence over the bundled fallback.
- Main menu uses original code-drawn Torii fallback buttons when skin textures
  are absent. No osu!ui.dll textures were imported.
- Classic component toggles, gameplay options, input/audio dropdown, aura picker,
  server pulse and shared theme picker now use the selected settings renderer.
- Remaining polish: Torii menu hue/NSFW controls, specialised potato/restart
  widgets and action buttons still use their existing controls. Do not claim
  that all Torii settings have been converted.
- Desktop Debug build passed (0 errors); NUnitLite harness passed 36/36 tests,
  including embedded PM textures and attribution. Not full gameplay E2E.
- Opened isolated portable Debug profile with audio muted, PID 25024.
  MainMenu reached without startup exception. Personal installed Torii untouched.
- No release, production deployment, resources push or client commit in this step.
  Release resources pins still need updating before publishing this candidate.
- Subsequent user test found a results-screen crash: LegacyRankingOverlay.load
  constructed the sizing container without assigning panelSpace. rebuildPanel
  dereferenced it during initial score load (runtime log 1794650099, line 398).
  Restored the missing assignment; rebuilt Desktop Debug successfully and
  reopened PID 23508. Results navigation still needs user re-test; earlier
  36/36 nonvisual checks did not cover this visual loading path.
- Follow-up: copied PM PNG UI families (pause, ranking, selection, score digits,
  mode icons, back button, hit judgements, arrows and songselect chrome) into
  bundled Legacy resources. No Rafis or DLL extraction and no PM audio imported.
  Existing bundled resources outside these families remain: this is not a
  statement that the entire application is free of third-party assets.
- Classic results entrance now requires IsLocalPlay, not merely a solo results
  screen. Existing leaderboard scores open without the count-up animation.
  Added four cases for local-play/auto-show combinations plus UI asset checks.
- Pause buttons and old-style back now explicitly fall back to bundled textures.
  Results rays are only used from a non-default user skin, not the stock bundle.
- Verification: final Desktop Debug build succeeded; 48/48 NUnitLite tests
  passed. Reopened isolated muted candidate, PID 1772. No publishing/deploy.
- Next user test exposed oversized PM mode artwork: mode-osu-small is 2192x1776
  and selection-mode is 1142x944. These are full-screen decorations, not icons.
  LegacyFooterUser was auto-sized by its ruleset sprite, expanding the profile
  hit region over song select. Fixed card size (340x86), clipping, bounded icon;
  default decorations are no longer mounted as full-screen skin chrome.
- LegacyTopDecoration now opts out of both input subtrees. Footer/ruleset
  fallback rejects oversized textures even when the default is also oversized;
  a code-drawn mode glyph stays available. Added two nonvisual regression tests.
- Slate prototype: source cloned to .tmp/slate-ui-reference at
  f29d938d6a6cbf4a1b5d82f7767f91a312076302, CC BY 4.0 (XetThe).
  .tmp/slate-export-tools/export.cjs extracts raster layers with ag-psd and
  @napi-rs/canvas; ignores old smart-object metadata, does not alter source PSDs.
  Exported 35 images: ranking Top panel, labels, grades, graph, replay/retry,
  carousel plate, footer buttons and score digits. Footer idle uses hover art.
  PSD full-screen decorations and below-fold baked stats are deliberately excluded.
  Pause actions, hit judgements, back button and other unconverted elements
  still use PM; this is a local comparison, not a finished single-source theme.
  SLATE-ASSETS.json records PSD layer provenance; credits and full licence
  included both in resources and Desktop output.
- Slate prototype build: Desktop Debug passed, NUnitLite 52/52 passed.
  Visual test remains manual; no release or server changes. Source PSDs unchanged.

## Classic UI follow-up

### Transparent Skin Backing

- User screenshot confirmed that transparent stable artwork expects dark backing.
  Restore an opaque black top backing below skin/decorative textures and a black
  footer backing that remains visible even when a skin provides transparent art.
- User card has its own black backing; move the whole card down 5 logical UI px
  (Y 3.2 -> 8.2). Keep its bounded hit area and cookie/carousel layers unchanged.
- This supersedes the earlier no-backing transparency policy; supplied transparent
  artwork remains untouched, with the backing drawn independently underneath.

### Saved HUD Override And Logo Layer

- Supersedes the prior preserve-custom-HUD policy: when StableUI and the gameplay
  leaderboard toggle are active, replace modern leaderboards in saved layouts at
  runtime too. Keep one classic leaderboard in the global main HUD, remove the
  duplicates from ruleset HUDs. Do not rewrite the source skin JSON.
- Default placement matches requested CentreLeft anchor/origin, position
  (5.704895, 45.772842), scale 1, size (144, 345.6), UsesFixedAnchor false.
- Removed size-driven cookie Y placement. Restored Y=-36 and keep the logo above
  the song-select front layer; carousel and footer proxy depths are unchanged.
- Custom texture lookup now unwraps nested ISkinSource providers before deciding
  whether an image is bundled. Added nested-source regression tests.
- Silent focused suite passed 81/81; in-game visual verification remains pending.

### Skin Chrome And Cookie

- The song-select top no longer stacks the bundled opaque bar below a custom
  (possibly transparent) songselect-top. Uses ISkinSource rather than bypassing
  lookup rules through CurrentSkin. Full-screen selection-mode artwork can
  supply the chrome without a default backing bar.
- Footer top-level images now listen for SourceChanged and release the handler
  on disposal. Custom bottom and large selection-mode decoration take priority;
  missing artwork still falls back. Transparent supplied PNGs are retained.
- Song-select score rows use a skin's menu-button-background if present, with a
  neutral charcoal fallback, and refresh on SourceChanged.
- Cookie facade is raised above the measured footer (with 8px spacing), accounting
  for cookie size. Carousel/footer proxy depths are unchanged.
- Desktop Debug compiled; silent focused suite passed 80/80. Retained the selected
  skin in the debug profile. Visual Retome verification is still manual.

### Results Wrapper Regression

- SkinProvidingContainer.FindProvider evaluates the predicate with a
  DisableableSkinSource wrapper, then returns the original provider. Filtering
  DefaultLegacySkin inside that predicate was ineffective: the bundled Slate
  ranking panel was incorrectly treated as custom, hiding the native labels and
  keeping its baked, misaligned rank plate. Check the returned provider instead.
- Shared FindCustomStableTextureSource fixes this for results, carousel colours,
  footer/top decoration and gameplay leaderboard background selection.
- Results texture lookup now locks to the first provider with a plain image or
  first animation frame, rather than allowing a default plain image to hide a
  user's first frame. Custom complete ranking panels remain supported.
- Default results title/buttons/graph frame no longer use the old mismatched
  Slate placeholders. Missing mod textures retain a text acronym; missing button
  textures retain visible text and a fixed hit area. No fake health graph when
  hit events are absent.
- Song-select leaderboard rows use a neutral charcoal box, not the blue PM PNG.
- Regression tests exercise wrapped default/custom providers and frame-0 lookup
  with DummyRenderer (no audio), plus missing-texture button dimensions/content.
- Desktop Debug build succeeded; focused suite including texture fallback tests
  passed 77/77. Relaunched muted without changing the selected skin. Visual
  confirmation of the user's reported score remains pending.

- Kept the modern main menu unconditionally for now, including when old saved
  preferences request the classic menu. Hid its toggle until artwork is ready.
- Argon, Triangles and legacy default HUDs now share the classic leaderboard
  factory. Custom saved HUD layouts are not rewritten.
- PM's squarer background is a separate torii-leaderboard-background asset for
  classic gameplay and song-select scores. Twotoned carousel remains unchanged.
- Bundled results now render hit labels, score/counts, combo and accuracy with
  Inter instead of relying on transparent judgement sprites and mismatched baked
  panel labels. Removed the bundled baked plate and constrained the rank size.
  Skins providing a ranking panel retain their original texture-based layout.
- Classic UI uses Inter (SIL OFL); no longer registers or embeds Aller. The saved
  Typeface.Aller identifier maps to Inter for compatibility. Inter licence is in
  LICENSES/Inter.txt. This does not audit every historical raster asset's font.
- Verification: Desktop Debug build succeeded; focused NUnitLite suite 61/61.
  Visual validation is pending. Relaunch uses the bundled skin in this debug
  profile; prior selected skin e7b611ca-59b7-4879-bff0-ae3e994cd21c remains installed.

## Earlier Twotoned carousel trial

- Follow-up: the debug profile had imported skin
  dfce1bd3-5fcd-46a4-b35d-610f8b5802f7 selected, overriding the bundled fallback.
  Selected CLASSIC_SKIN (81f02cd3-eec6-4865-ac23-fae26a386187) in this isolated
  profile only. The imported skin remains installed and can be reselected.
- Added Twotoned songselect-top/bottom and selection-tab at both resolutions.
  Prior trial changed only the carousel plate, not the surrounding chrome.

- Replaced the shared menu-button-background at both resolutions with Twotoned
  revision ed203b11f344f730e1c0152106e64ecbca375b79. This shared texture also
  supplies the classic leaderboard fallback. Kept the existing separate buttons;
  did not import Twotoned's key-labelled buttons or selection-mode decoration.
- Default carousel text uses Twotoned's light grey colours; custom skin panels
  retain their configured text colours.
- Footer background now has its own front-layer proxy, below footer controls,
  rather than drawing under the carousel. Needs manual interaction validation.
- Only two background PNGs imported. WTFPL declaration and exclusions recorded
  in TWOTONED-CREDITS.txt. No audio or external slider artwork imported.
- This is still a mixed local prototype. Results alignment/labels are not fixed
  by replacing a carousel texture. No publishing or production changes.
