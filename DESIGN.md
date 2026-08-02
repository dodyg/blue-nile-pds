---
name: PDS Admin
description: A split-flap departure board for operating a self-hosted ATProto PDS instance.
colors:
  primary: "#1d4ed8"
  primary-hover: "#1e40af"
  focus-ring: "#3b82f6"
  accent: "#0560ff"
  accent-hover: "#0547c9"
  accent-soft: "#dbeafe"
  accent-ring: "#93c5fd"
  warning: "#eab308"
  warning-hover: "#ca8a04"
  warning-bg: "#fef9c3"
  warning-text: "#854d0e"
  success: "#16a34a"
  success-deep: "#15803d"
  danger: "#dc2626"
  danger-hover: "#b91c1c"
  danger-deep: "#991b1b"
  page: "#f9fafb"
  surface: "#ffffff"
  surface-raised: "#fafafa"
  subtle: "#e5e7eb"
  input: "#d1d5db"
  hover: "#f3f4f6"
  row-hover: "#f9fafb"
  overlay: "rgba(15, 23, 42, 0.4)"
  shadow-white: "rgb(255 255 255 / 0.1)"
  shadow-black: "rgb(0 0 0 / 0.35)"
  shadow-card: "rgb(15 23 42 / 0.06)"
  shadow-modal: "rgb(0 0 0 / 0.28)"
  ink: "#232e3e"
  secondary: "#6b7280"
  muted: "#9ca3af"
  ghost: "#374151"
  neutral: "#4b5563"
  board: "#eaf1ff"
  board-hover: "#dde9ff"
  board-text: "#1d4ed8"
  board-text-dim: "#45679b"
  data-string: "#15803d"
  data-number: "#2563eb"
  data-boolean: "#7c3aed"
  data-null: "#9ca3af"
  dark:
    page: "#0f172a"
    surface: "#1e293b"
    surface-raised: "#243247"
    subtle: "#334155"
    input: "#475569"
    hover: "#263447"
    row-hover: "#1b263c"
    overlay: "rgba(0, 0, 0, 0.6)"
    ink: "#e2e8f0"
    secondary: "#94a3b8"
    muted: "#64748b"
    ghost: "#cbd5e1"
    neutral: "#94a3b8"
    board: "#0a1120"
    board-hover: "#141f36"
    board-text: "#6ea8ff"
    board-text-dim: "#7f9bc4"
    primary: "#60a5fa"
    primary-hover: "#93c5fd"
    focus-ring: "#60a5fa"
    accent: "#3b82f6"
    accent-hover: "#60a5fa"
    accent-soft: "rgba(59, 130, 246, 0.15)"
    accent-ring: "#60a5fa"
    warning: "#facc15"
    warning-hover: "#fde047"
    warning-bg: "rgba(250, 204, 21, 0.15)"
    warning-text: "#fde047"
    success: "#4ade80"
    success-deep: "#22c55e"
    danger: "#f87171"
    danger-hover: "#fca5a5"
    danger-deep: "#dc2626"
    data-string: "#86efac"
    data-number: "#60a5fa"
    data-boolean: "#c084fc"
    data-null: "#64748b"
typography:
  display:
    fontFamily: "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace"
    fontSize: "1.5rem"
    fontWeight: 700
    letterSpacing: "-0.02em"
  title:
    fontFamily: "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace"
    fontSize: "0.875rem"
    fontWeight: 700
    letterSpacing: "0.08em"
  label:
    fontFamily: "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace"
    fontSize: "0.625rem"
    fontWeight: 600
    letterSpacing: "0.16em"
    textTransform: "uppercase"
  body:
    fontFamily: "ui-sans-serif, system-ui, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.5
  mono:
    fontFamily: "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace"
    fontSize: "0.75rem"
    fontWeight: 400
rounded:
  sm: "4px"
  md: "8px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "20px"
  xl: "24px"
components:
  button-primary:
    backgroundColor: "{colors.board}"
    textColor: "{colors.board-text}"
    rounded: "{rounded.sm}"
    padding: "8px 16px"
  button-primary-hover:
    backgroundColor: "{colors.board-hover}"
  button-secondary:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ghost}"
    rounded: "{rounded.sm}"
    padding: "8px 16px"
  button-danger:
    backgroundColor: "{colors.danger}"
    textColor: "#ffffff"
    rounded: "{rounded.sm}"
    padding: "8px 16px"
  input:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.sm}"
    padding: "8px 12px"
  nav-link-active:
    backgroundColor: "{colors.board}"
    textColor: "{colors.board-text}"
    rounded: "{rounded.sm}"
    padding: "8px 12px"
  card:
    backgroundColor: "{colors.surface}"
    rounded: "{rounded.md}"
    padding: "20px"
  chip-board:
    backgroundColor: "{colors.board}"
    textColor: "{colors.board-text}"
    rounded: "{rounded.sm}"
    padding: "4px 10px"
  board-stat:
    backgroundColor: "{colors.board}"
    textColor: "{colors.board-text}"
    rounded: "{rounded.md}"
    padding: "12px"
---

# Design System: PDS Admin

## Overview

**Creative North Star: "The Split-Flap Departure Board"**

The admin console is a departure board at a busy station. Board flip-cells
carry the live values, AT Protocol blue ink glows behind every active state,
and dense data — accounts, invite codes, JSON payloads — sits on ruled
timetable paper where it can be read and copied without ceremony. Each page is a
"board": a tracked-caps destination header, a set of flip-chips for the numbers
that matter, and a paper table for everything else.

The palette is the official AT Protocol brand: Primary Blue `#0560FF` as the
accent, Bluesky's cool neutrals (Neutral50 `#F9FAFB` paper, Neutral900 `#232E3E`
ink), and navy-toned dark surfaces at night. The system reads in both registers
on purpose. Light mode is the daylight station — cool white paper, pale blue
board cells with deep blue ink, blue signage. Dark mode is the night concourse —
near-black navy board cells with bright blue phosphor, the same rules at lower
light. Identity stays in the data:
DIDs, CIDs, codes, and handles always render in monospace, and the board treats
every identifier like a flight number — something to be read exactly and never
misread.

**Key Characteristics:**
- Board flip-cells (`board`) carry live values and active states — pale blue with deep blue ink in light mode, navy with blue phosphor in dark mode; cool white paper (`surface`) carries dense data
- One AT Protocol blue accent (`primary`/`board-text`); status green/red/yellow only for meaning
- Monospace + text-xs for every identifier: DIDs, CIDs, codes, rkeys, handles
- Tracked small-caps mono headers on boards and tables only; body text stays plain
- Mechanical flip motion on value change — one orchestrated move, disabled under `prefers-reduced-motion`

## Colors

A cool, low-chroma palette built on the official AT Protocol brand: Bluesky
Neutral50/Neutral900 grays, a single Primary Blue `#0560FF` accent, and
navy-flavored dark surfaces.

### Primary
- **AT Protocol Blue** (`primary` #1d4ed8): links, focus rings, and accents on paper — deepened from the brand blue to hold 4.5:1 on white. Hover deepens to `primary-hover` (#1e40af). On dark backgrounds the accent brightens to `board-text` (sky #6ea8ff).

### Secondary
- **Brand Blue** (`accent` #0560ff): the official AT Protocol Primary Blue, for fills, icons, and highlights. Hover deepens to `accent-hover` (#0547c9), with `accent-soft` (#dbeafe wash) and `accent-ring` (#93c5fd border).

### Tertiary
- **Platform Green** (`success` #16a34a): confirmation messages and "all clear" states; deep `success-deep` #15803d on light paper.
- **Late-Train Yellow** (`warning` #eab308): flag states ("invites off"), with `warning-bg` #fef9c3 wash and `warning-text` #854d0e ink.
- **Cancelled Red** (`danger` #dc2626): destructive actions and errors — takedown, delete, disable, failed backups. `danger-hover` #b91c1c deepens, `danger-deep` #991b1b escalates permanent deletes.

### Neutral
- **Concourse Cloud** (`page` #f9fafb): the light-mode application background — Bluesky Neutral50.
- **Timetable Paper** (`surface` #ffffff): cards, tables, inputs, sidebar, modals — Bluesky Neutral0; `surface-raised` #fafafa for table header bands.
- **Platform Gray** (`hover` #f3f4f6): hover fills and segmented-control tracks; `row-hover` #f9fafb for table row hovers.
- **Hairline** (`subtle` #e5e7eb): all borders and table rules; `input` #d1d5db for field strokes.
- **Ink Navy** (`ink` #232e3e): primary text — Bluesky Neutral900. `secondary` #6b7280 labels, `muted` #9ca3af de-emphasis, `ghost` #374151 secondary-button text, `neutral` #4b5563 neutral fills.
- **Flip Cell** (`board` #eaf1ff): the pale blue board module — the daylight analog of the dark navy cell; `board-hover` #dde9ff on hover; `board-text` #1d4ed8 deep blue ink; `board-text-dim` #45679b captions. In dark mode the same tokens resolve to navy (`board` #0a1120) with blue phosphor (`board-text` #6ea8ff).

### Dark Theme
The night concourse keeps the same rules at lower light: `page` #0f172a (slate-900), `surface` #1e293b, hairlines `subtle` #334155. Ink lifts to `ink` #e2e8f0, phosphor stays `board-text` #6ea8ff on a deeper `board` #0a1120. The blue accent becomes `accent` #3b82f6 and links `primary` #60a5fa. Status colors lift to `success` #4ade80, `danger` #f87171, `warning` #facc15.

### Named Rules
**The Sky-Signal Rule.** AT Protocol blue is the sole accent. Everything structural is cool neutral or board blue; green, red, and yellow appear only to carry meaning, never decoration.

**The Mono-For-Identity Rule.** Anything the operator must copy verbatim — DIDs, CIDs, invite codes, record keys, handles — renders in monospace at text-xs. The font itself says "this is data."

## Typography

**Display Font:** Default UI monospace stack (ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace) — the board's flip-digit face.
**Body Font:** Default UI sans stack (ui-sans-serif, system-ui, sans-serif).
**Mono Font:** Same default UI monospace stack for identifiers, JSON, and raw payloads.

**Character:** A station-board pairing. The monospace face drives every headline, stat, label, and identifier — numbers and codes feel like they were stamped in place. Proportional sans handles only the readable prose. Tracked small-caps mono marks board hierarchy; everything else stays lowercase and quiet.

> Note: the atproto.com site uses IBM Plex Sans, but this admin panel deliberately stays on system stacks — no webfonts, so the UI works even when the font self-hoster is unreachable.

### Hierarchy
- **Display** (700, 24px, -0.02em): page titles — "Accounts", "Invite Codes", "Backups".
- **Title** (700, 14px, +0.08em): card headers ("Repo Collections", "Account Invite Codes") and stat values.
- **Label** (600, 10px, +0.16–0.24em, uppercase): board eyebrows, table headers, field labels, badges.
- **Body** (400, 14px, 1.5): table cells, button labels, form text, prose (65–75ch max).
- **Mono** (400, 12px, 1.4): identifiers, JSON tree, raw payloads, timestamps.

### Named Rules
**The Departure-Board Header Rule.** Tracked small-caps mono belongs to board headers only — eyebrows, table headers, field labels, badges. Body and paragraph text are never uppercase and never letter-spaced.

## Layout

A single-column operations layout under a fixed left rail. The rail is the station wall: `surface` paper, a board brand chip at top ("PDS ADMIN"), then the board list — each nav item numbered like a departure (01–05) with the active one lit as a board flip-cell. Below `md` the rail becomes an off-canvas drawer over a `overlay` backdrop with a hamburger toggle; the mobile header row repeats the brand chip and the theme toggle.

Content lives in a main column (p-4, md:p-6). Every page opens with a `PageHeader`: a tracked-caps eyebrow ("operations · overview"), a mono display title, and a right-aligned actions slot. Data surfaces are `surface` paper cards with rounded-md corners and hairline borders: stat modules in a responsive grid, tables full-width with `overflow-x-auto`, forms capped at `max-w-lg`. Account actions sit in a `grid-cols-2 sm:grid-cols-3` grid.

**Spacing rhythm:** 4px (xs) inline gaps and badge padding, 8px (sm) button/input padding and nav gaps, 12px table-cell padding, 16px (md) standard gaps, 20px (lg) card padding, 24px (xl) page margins. Groups of controls use 8px–12px gaps; sections separate on 24px.

## Elevation & Depth

Flat by default; depth is carried by rule, not shadow. Boards and paper surfaces are separated by hairline borders; the only cast shadows are the chip's inset bevel and the modal's lift:

- **Chip bevel** (`shadow-chip`): `box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.7), 0 1px 2px rgb(15 23 42 / 0.08)` — the tactile depth of a physical flip-cell, used on every board chip (soft and cool on pale daylight cells; the `.dark` theme resolves it to a deeper bevel on navy cells).
- **Card lift** (`shadow-card`): `box-shadow: 0 1px 2px rgb(15 23 42 / 0.06), 0 2px 8px rgb(15 23 42 / 0.05)` — a whisper under stat and form cards, always paired with a hairline border.
- **Modal layer** (`shadow-modal`): `box-shadow: 0 4px 12px rgb(0 0 0 / 0.12), 0 24px 48px -12px rgb(0 0 0 / 0.28)` — modals and confirm dialogs float above the `overlay` backdrop.

### Named Rules
**The Paper-Rule.** Resting content never casts an ambient shadow. Paper surfaces earn separation with a hairline border; board chips carry the tactile bevel; only floating layers get the deep shadow.

## Shapes

A soft-rectangle form language with two radii. **Rounded-sm (4px)** for chips, badges, buttons, inputs, nav links, and the disclosure caret. **Rounded-md (8px)** for anything larger — cards, tables, modals, stat modules. Everything is a rectangle with mild corners; there are no pills or circles. Borders are 1px hairlines in `subtle` (or `input` on fields), drawn on every surface boundary. Board chips add a `border-black/15` seam and the inset bevel, echoing the slit between flip cells.

## Components

### Buttons
- **Shape:** soft rectangle, rounded-sm (4px), height ~36px (small variants px-2.5 py-1.5 text-xs).
- **Primary:** `board` fill — pale blue with deep blue ink in light mode, navy with blue phosphor in dark mode — plus the chip bevel. Hover lifts to `board-hover`. The primary action lights up like a lit destination cell.
- **Secondary / Outline:** `surface` fill, `ghost` text, `input` border; hover fills `hover`.
- **Ghost:** quiet text button (`secondary`, hover `ink` on `hover` fill), used for "View", back links, "Download".
- **Destructive:** `danger` red fill, white text; hover deepens. Permanent deletes escalate to `danger-deep`.
- **Disabled:** 50% opacity, no pointer events.

### Chips / Badges
- **Style:** text-[10px], mono, uppercase, +0.12em tracking, rounded-sm, px-2 py-0.5. Tones: `warning` (yellow wash/ink), `success` (green), `danger` (red), `accent` (blue wash), `neutral` (gray), `board` (pale blue cell in light, navy in dark).
- **FlipChip:** the shared board chip (px-2.5 py-1, text-xs mono, chip bevel) — used for status CIDs, counts, mode toggles.
- **Collection pills:** paper chips with `subtle` border, mono text-xs; hover tints `accent-soft` with an `accent-ring` border.

### Cards / Containers
- **Corner Style:** rounded-md (8px).
- **Background:** `surface` paper, always with a `subtle` hairline border.
- **Shadow Strategy:** flat at rest, optional `shadow-card` on stat/action surfaces. See The Paper-Rule.
- **Internal Padding:** 20px (lg); card headers carry a hairline rule with a mono Title and optional actions.

### Inputs / Fields
- **Style:** `surface` fill, `input` stroke, rounded-sm (4px), px-3 py-2, `ink` text, `muted` placeholder.
- **Focus:** stroke shifts to `focus-ring` (blue) plus a 2px `focus-ring`/30 ring; no other treatment.
- **Error / Disabled:** errors surface as red text beside the field; disabled controls fade to 50%.

### Navigation
- **Style:** left rail (240px) or mobile drawer; numbered mono indices.
- **Default:** `secondary` text. **Hover:** `hover` fill. **Active:** `board` cell — pale blue with deep blue ink in light mode, navy with blue phosphor in dark mode — plus chip bevel. Logout is a quiet `muted` row at the rail bottom; a theme toggle sits beside it.
- **Mobile:** off-canvas drawer over `overlay` with a hamburger toggle; header row carries the brand chip and theme toggle.

### Board Stat (signature)
A board flip module for live numbers — total accounts, backup counts. A `board-text-dim` tracked-caps caption, a large mono `board-text` value, and the flip animation (`animate-flap`) keyed to the value so each change reads as one mechanical shutter drop. Honors `prefers-reduced-motion`.

### Table (signature)
The paper ledger. `surface` card, rounded-md, hairline border, `overflow-x-auto`. Header row: `surface-raised` band with mono 10px tracked-caps `muted` headers. Body rows: 12px padding, hairline rules, `row-hover` tint. Identifiers render text-xs monospace with truncation and a tooltip; timestamps use `toLocaleString()`. Expandable invite rows open a nested detail band with its own sub-table.

### JsonTree (signature)
A semantic JSON inspector, text-xs mono, indented under a 1px `subtle` left rule. Keys are `secondary`; values carry type color — `data-string` (green-700), `data-number` (blue), `data-boolean` (violet-700), `data-null` (muted). Objects/arrays show their size in `muted` with a ▸/▾ caret and expand to depth 2 by default. Image blobs render inline as thumbnails via the blob endpoint.

### Modal / Confirm Dialog
Centered panel (max-w-sm), rounded-md, `surface` paper, `subtle` border, `shadow-modal`, p-6, over a fixed `overlay` backdrop. Title is mono Title-case; actions are right-aligned secondary (cancel) + solid (confirm, red for destructive). The input variant auto-focuses its single field.

## Do's and Don'ts

### Do:
- **Do** let every identifier (DIDs, CIDs, codes, rkeys, handles) render in text-xs monospace, truncated with a tooltip where wide.
- **Do** reserve board flip-cells (pale blue in light mode, navy in dark) + AT Protocol blue ink for live values and the active state; put dense data on ruled paper.
- **Do** use tracked small-caps mono only for board headers — eyebrows, table headers, field labels, badges.
- **Do** keep one blue primary action per view; color every other action semantically (green/red/yellow).
- **Do** run the JSON colors so record payloads stay scannable; keep wide tables scrolling horizontally instead of wrapping.

### Don't:
- **Don't** introduce warm tones (amber, bone, bronze) — the accent is AT Protocol blue on cool neutrals.
- **Don't** use blue for destructive actions — destructive is always red.
- **Don't** uppercase or letter-space body text; that register belongs to board headers only.
- **Don't** scatter multiple animations — one mechanical flip on value change, and honor `prefers-reduced-motion`.
- **Don't** render JSON or identifiers in proportional type.
- **Don't** add gradients, illustrations, or ambient shadows to resting surfaces.
