# Stable UI and 2B/Aspire port

## Scope and attribution

Target: private `ShikkesoraSIM/torii-hiding`, based on `066f117510`.
Local branch: `port/stable-ui-aspire-private`.
Source: telecomadm1145 / GooGuTeam, `g0v0-pr20` at
`9572d6b0aeee68cc1449c1982e3e2beb891a599c` (including the UI work at
`63c767c845` and 2B rendering work). Integration permission supplied by the
Torii maintainer. Source copyright headers remain in imported files.

This is a selective port, not a merge. Networking/authentication, server
notifications, trusted domains, online settings strings, custom difficulty/PP
evaluators and source CI workflows are excluded. Existing Torii score notes,
replay actions and modern mod selection remain available.

No assets were added from the source resources repository. The existing pinned
Torii resources supply the classic assets and optional fonts.

## Settings

`UIThemeOption.StableUI` uses five independent component preferences:

- `ToriiStableUISongSelect`: retro carousel and classic song select.
- `ToriiStableUIResults`: classic ranking panel.
- `ToriiStableUIMods`: classic mod selector; All mods / customise opens Torii's
  complete modern selector, including custom mods and their settings.
- `ToriiStableUIFooter`: skinnable footer and buttons.
- `ToriiStableUIFonts`: existing Aller font option.

The settings subsection stages checkbox edits without writing configuration.
Apply is enabled only when those edits differ from saved preferences. Apply
saves them, selects Stable UI and offers one restart dialog. Later keeps the
saved configuration but does not rebuild the running UI. The startup snapshot
is scoped to each config instance, not a process-global config singleton.

Old legacy preferences are retained outside Stable UI. One-time migration
copies an existing custom legacy combination into the new theme preferences
without changing the selected theme. New installations default to all five
components when Stable UI is selected. The V2 discovery popup uses its own
shown flag and credits telecomadm1145.

## Engine

2B/Aspire support is independent of UI theme. Stable-style piece ordering is
enabled by default as an osu ruleset preference, not a theme preference.
The port retains finite legacy timing metadata while keeping native/editor
timing bounded; includes round-trip handling, simultaneous-object ordering,
slider path fixes and a bounded tick budget. It does not port Relax evaluators
or performance formulas. These engine changes can still affect gameplay on
edge-case maps; they are not claimed to reproduce stable exactly.

## Verification

- Release Desktop build on `net10.0`: passed, zero errors.
- Direct NUnitLite: 8/8 configuration and mod-selection cases passed.
- Direct engine fixture runner: 32/32 cases passed, including signed timing,
  JSON/editor round-trips, simultaneous objects and extreme slider bounds.
- `git diff --check`: passed.
- Source networking/PP/workflows were not ported. Torii's own resources pin
  was subsequently updated to match the maintainer's current mod-icon assets.

The existing base is not warning-free. A clean baseline Desktop build reported
374 warnings. The full port build reported 331; comparison of production
compiler warnings found no new warning identities. Incremental warning counts
are smaller and must not be presented as a clean zero-warning build. No global
warning suppression or unrelated warning cleanup was added.

The standard VSTest testhost could not run here (Windows native startup failure
and parent-process access denied), so nonvisual fixtures were executed directly.
Local verification uses clean dependency worktrees matching the workflow pins:
framework `25e68d530f7116437c4cfaf127169ac4d6619927`, resources
`052c77623424f3f1a571d8bcbac0d608d458f8f8`. The initial test build used the older
resources pin `2a516e45`; it must not be used as the release candidate.
A local-only MSBuild targets override resolves dependency paths from this deeper
worktree. The maintainer's `9379351b58` mod-icon change is also included without
discarding the private repo's separate subprofiles integration. Uncommitted
editor/framework work in other worktrees was left untouched.

## English UI follow-up

Default Stable UI hints, promo text, credits and restart dialog are English.
Strictly vertical UI remains under User Interface > Song Select, not Stable UI.
The ranked-play comfort picker no longer shows raw server exception messages:
409/422/401 and fallback errors have explicit English copy. Existing backend
Spanish error strings were identified but no backend edits/deployment were made.
An exposed Mapperatorinator PyTorch setup status line was also translated.
The local NUnitLite regression suite includes six English-copy cases, alongside
the eight existing configuration/mod-selection cases (14 passing cases).

## Permanent credits

The small pink heart below the settings version opens an English credits dialog
thanking GooGuTeam (correct spelling), shigetiro, telecomadm1145 and ppy/upstream
contributors. The Stable UI discovery popup also credits telecomadm1145.
THIRD-PARTY-NOTICES.txt now includes GooGuTeam's 2026 MIT copyright and full
permission/warranty notice for the selected contributions. The existing Desktop
project copies this notice into build and publish outputs. Code permission and
attribution do not establish permissions for every resource, font or trademark.

## Before release

Visible built-in mode names are now `torii`, `torii drums`, `torii fruits` and
`torii keys`. Settings and ordinary selectors use ruleset descriptions; generator
choices, profile variants and the activity breakdown have matching labels.
Internal short names (`osu`, `taiko`, `fruits`, `mania`), legacy/online IDs,
assembly names, API values and generator enum values remain unchanged.
Existing Realm ruleset names refresh through the unchanged ruleset-store startup
consistency pass; no schema or identity migration was added. Regression cases
check display names, identity and recreation from existing ruleset metadata.

Not yet visually validated: modern and Stable UI, all five component combinations,
missing/custom skins, 4:3/widescreen/resize, footer/logo/proxy layering, mod-overlay
navigation, results retry/replay, seeking and pooled hitobject lifetimes.
Visual/gameplay tests must use a verified silent test-only audio backend.
No real account login, leaderboard request or score/replay submission was made
as part of verification. Unchanged networking is not an end-to-end server test.

No commit, push, release or production deployment was performed for this port.
