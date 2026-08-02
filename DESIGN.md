---
name: PDS Admin
description: A split-flap departure board for operating a self-hosted ATProto PDS instance.
colors:
  primary: "#b45309"
  primary-hover: "#92400e"
  focus-ring: "#d97706"
  accent: "#d97706"
  accent-hover: "#b45309"
  accent-soft: "#fdf0ce"
  accent-ring: "#f59e0b"
  warning: "#d97706"
  warning-hover: "#b45309"
  warning-bg: "#fdf0ce"
  warning-text: "#92400e"
  success: "#4d7c0f"
  success-deep: "#3f6212"
  danger: "#b91c1c"
  danger-hover: "#991b1b"
  danger-deep: "#7f1d1d"
  page: "#efe9da"
  surface: "#f8f4e8"
  surface-raised: "#fdfaf1"
  subtle: "#ddd5c0"
  input: "#c8bfa6"
  hover: "#e9e2cd"
  row-hover: "#f3eedd"
  overlay: "rgba(24, 20, 12, 0.45)"
  ink: "#211d15"
  secondary: "#6d6550"
  muted: "#948c76"
  ghost: "#4a4434"
  neutral: "#544d3b"
  board: "#2e2921"
  board-hover: "#3a342b"
  board-text: "#fbbf24"
  board-text-dim: "#b98a1c"
  data-string: "#4d7c0f"
  data-number: "#a16207"
  data-boolean: "#6d28d9"
  data-null: "#948c76"
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

The admin console is a departure board at a busy station. Charcoal flip-chips
carry the live values, amber phosphor glows behind every active state, and dense
data — accounts, invite codes, JSON payloads — sits on ruled timetable paper
where it can be read and copied without ceremony. Each page is a "board": a
tracked-caps destination header, a set of flip-chips for the numbers that matter,
and a paper table for everything else.

The system reads in both registers on purpose. Light mode is the daylight
station — warm bone paper, bronze ink, amber signage. Dark mode is the night
concourse — near-black warm charcoal, dimmed amber phosphor, the same rules at
lower light. Identity stays in the data: DIDs, CIDs, codes, and handles always
render in monospace, and the board treats every identifier like a flight number
— something to be read exactly and never misread.

**Key Characteristics:**
- Charcoal flip-chips (`board`) carry live values and active states; warm paper (`surface`) carries dense data
- One amber phosphor accent (`primary`/`board-text`); status green/red/yellow only for meaning, blue is retired
- Monospace + text-xs for every identifier: DIDs, CIDs, codes, rkeys, handles
- Tracked small-caps mono headers on boards and tables only; body text stays plain
- Mechanical flip motion on value change — one orchestrated move, disabled under `prefers-reduced-motion`

## Colors

A warm, low-chroma palette built on bone and charcoal, with a single amber accent.

### Primary
- **Departure Amber** (`primary` #b45309): links, focus rings, and amber accents on paper. Hover deepens to `primary-hover` (#92400e). On dark backgrounds the accent brightens to `board-text` (amber-400 #fbbf24).

### Secondary
- **Signal Amber** (`accent` #d97706): editable/secondary highlights, accent chips, hover tints (`accent-soft` #fdf0ce wash, `accent-ring` #f59e0b border).

### Tertiary
- **Platform Green** (`success` #4d7c0f): confirmation messages and "all clear" states; deep `success-deep` #3f6212 on light paper.
- **Late-Train Yellow** (`warning` #d97706): flag states ("invites off"), with `warning-bg` #fdf0ce wash and `warning-text` #92400e ink.
- **Cancelled Red** (`danger` #b91c1c): destructive actions and errors — takedown, delete, disable, failed backups. `danger-hover` #991b1b deepens, `danger-deep` #7f1d1d escalates permanent deletes.

### Neutral
- **Concourse Bone** (`page` #efe9da): the light-mode application background.
- **Timetable Paper** (`surface` #f8f4e8): cards, tables, inputs, sidebar, modals; `surface-raised` #fdfaf1 for table header bands.
- **Platform Shadow** (`hover` #e9e2cd): hover fills and segmented-control tracks; `row-hover` #f3eedd for table row hovers.
- **Hairline** (`subtle` #ddd5c0): all borders and table rules; `input` #c8bfa6 for field strokes.
- **Ink Bronze** (`ink` #211d15): primary text. `secondary` #6d6550 labels, `muted` #948c76 de-emphasis, `ghost` #4a4434 secondary-button text, `neutral` #544d3b neutral fills.
- **Flip Chip** (`board` #2e2921): the charcoal board module; `board-hover` #3a342b on hover; `board-text` #fbbf24 phosphor; `board-text-dim` #b98a1c captions.

### Named Rules
**The Phosphor Rule.** Amber is the sole accent. Everything structural is bone or charcoal; green, red, and yellow appear only to carry meaning, never decoration. Blue is retired.

**The Mono-For-Identity Rule.** Anything the operator must copy verbatim — DIDs, CIDs, invite codes, record keys, handles — renders in monospace at text-xs. The font itself says "this is data."

## Typography

**Display Font:** Default UI monospace stack (ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace) — the board's flip-digit face.
**Body Font:** Default UI sans stack (ui-sans-serif, system-ui, sans-serif).
**Mono Font:** Same default UI monospace stack for identifiers, JSON, and raw payloads.

**Character:** A station-board pairing. The monospace face drives every headline, stat, label, and identifier — numbers and codes feel like they were stamped in place. Proportional sans handles only the readable prose. Tracked small-caps mono marks board hierarchy; everything else stays lowercase and quiet.

### Hierarchy
- **Display** (700, 24px, -0.02em): page titles — "Accounts", "Invite Codes", "Backups".
- **Title** (700, 14px, +0.08em): card headers ("Repo Collections", "Account Invite Codes") and stat values.
- **Label** (600, 10px, +0.16–0.24em, uppercase): board eyebrows, table headers, field labels, badges.
- **Body** (400, 14px, 1.5): table cells, button labels, form text, prose (65–75ch max).
- **Mono** (400, 12px, 1.4): identifiers, JSON tree, raw payloads, timestamps.

### Named Rules
**The Departure-Board Header Rule.** Tracked small-caps mono belongs to board headers only — eyebrows, table headers, field labels, badges. Body and paragraph text are never uppercase and never letter-spaced.

## Layout

A single-column operations layout under a fixed left rail. The rail is the station wall: `surface` paper, a charcoal brand board-chip at top ("PDS ADMIN"), then the board list — each nav item numbered like a departure (01–05) with the active one lit as a charcoal flip-chip. Below `md` the rail becomes an off-canvas drawer over a `overlay` backdrop with a hamburger toggle; the mobile header row repeats the brand board-chip and the theme toggle.

Content lives in a main column (p-4, md:p-6). Every page opens with a `PageHeader`: a tracked-caps eyebrow ("operations · overview"), a mono display title, and a right-aligned actions slot. Data surfaces are `surface` paper cards with rounded-md corners and hairline borders: stat modules in a responsive grid, tables full-width with `overflow-x-auto`, forms capped at `max-w-lg`. Account actions sit in a `grid-cols-2 sm:grid-cols-3` grid.

**Spacing rhythm:** 4px (xs) inline gaps and badge padding, 8px (sm) button/input padding and nav gaps, 12px table-cell padding, 16px (md) standard gaps, 20px (lg) card padding, 24px (xl) page margins. Groups of controls use 8px–12px gaps; sections separate on 24px.

## Elevation & Depth

Flat by default; depth is carried by rule, not shadow. Boards and paper surfaces are separated by hairline borders; the only cast shadows are the chip's inset bevel and the modal's lift:

- **Chip bevel** (`shadow-chip`): `box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.1), 0 1px 3px rgb(0 0 0 / 0.35)` — the tactile depth of a physical flip-cell, used on every charcoal chip.
- **Card lift** (`shadow-card`): `box-shadow: 0 1px 2px rgb(30 24 12 / 0.06), 0 2px 8px rgb(30 24 12 / 0.05)` — a whisper under stat and form cards, always paired with a hairline border.
- **Modal layer** (`shadow-modal`): `box-shadow: 0 4px 12px rgb(0 0 0 / 0.12), 0 24px 48px -12px rgb(0 0 0 / 0.28)` — modals and confirm dialogs float above the `overlay` backdrop.

### Named Rules
**The Paper-Rule.** Resting content never casts an ambient shadow. Paper surfaces earn separation with a hairline border; charcoal chips carry the tactile bevel; only floating layers get the deep shadow.

## Shapes

A soft-rectangle form language with two radii. **Rounded-sm (4px)** for chips, badges, buttons, inputs, nav links, and the disclosure caret. **Rounded-md (8px)** for anything larger — cards, tables, modals, stat modules. Everything is a rectangle with mild corners; there are no pills or circles. Borders are 1px hairlines in `subtle` (or `input` on fields), drawn on every surface boundary. Board chips add a `border-black/15` seam and the inset bevel, echoing the slit between flip cells.

## Components

### Buttons
- **Shape:** soft rectangle, rounded-sm (4px), height ~36px (small variants px-2.5 py-1.5 text-xs).
- **Primary:** charcoal `board` fill, amber `board-text` phosphor, chip bevel. Hover lifts to `board-hover`. The primary action lights up like a lit destination cell.
- **Secondary / Outline:** `surface` fill, `ghost` text, `input` border; hover fills `hover`.
- **Ghost:** quiet text button (`secondary`, hover `ink` on `hover` fill), used for "View", back links, "Download".
- **Destructive:** `danger` red fill, white text; hover deepens. Permanent deletes escalate to `danger-deep`.
- **Disabled:** 50% opacity, no pointer events.

### Chips / Badges
- **Style:** text-[10px], mono, uppercase, +0.12em tracking, rounded-sm, px-2 py-0.5. Tones: `warning` (yellow wash/ink), `success` (green), `danger` (red), `accent` (amber wash), `neutral` (gray), `board` (solid charcoal).
- **FlipChip:** the shared charcoal chip (px-2.5 py-1, text-xs mono, chip bevel) — used for status CIDs, counts, mode toggles.
- **Collection pills:** paper chips with `subtle` border, mono text-xs; hover tints `accent-soft` with an `accent-ring` border.

### Cards / Containers
- **Corner Style:** rounded-md (8px).
- **Background:** `surface` paper, always with a `subtle` hairline border.
- **Shadow Strategy:** flat at rest, optional `shadow-card` on stat/action surfaces. See The Paper-Rule.
- **Internal Padding:** 20px (lg); card headers carry a hairline rule with a mono Title and optional actions.

### Inputs / Fields
- **Style:** `surface` fill, `input` stroke, rounded-sm (4px), px-3 py-2, `ink` text, `muted` placeholder.
- **Focus:** stroke shifts to `focus-ring` (amber) plus a 2px `focus-ring`/30 ring; no other treatment.
- **Error / Disabled:** errors surface as red text beside the field; disabled controls fade to 50%.

### Navigation
- **Style:** left rail (240px) or mobile drawer; numbered mono indices.
- **Default:** `secondary` text. **Hover:** `hover` fill. **Active:** charcoal `board` chip, amber `board-text`, chip bevel. Logout is a quiet `muted` row at the rail bottom; a theme toggle sits beside it.
- **Mobile:** off-canvas drawer over `overlay` with a hamburger toggle; header row carries the brand chip and theme toggle.

### Board Stat (signature)
A charcoal flip module for live numbers — total accounts, backup counts. A `board-text-dim` tracked-caps caption, a large mono phosphor value, and the flip animation (`animate-flap`) keyed to the value so each change reads as one mechanical shutter drop. Honors `prefers-reduced-motion`.

### Table (signature)
The paper ledger. `surface` card, rounded-md, hairline border, `overflow-x-auto`. Header row: `surface-raised` band with mono 10px tracked-caps `muted` headers. Body rows: 12px padding, hairline rules, `row-hover` tint. Identifiers render text-xs monospace with truncation and a tooltip; timestamps use `toLocaleString()`. Expandable invite rows open a nested detail band with its own sub-table.

### JsonTree (signature)
A semantic JSON inspector, text-xs mono, indented under a 1px `subtle` left rule. Keys are `secondary`; values carry type color — `data-string` (green-700), `data-number` (amber), `data-boolean` (violet-700), `data-null` (muted). Objects/arrays show their size in `muted` with a ▸/▾ caret and expand to depth 2 by default. Image blobs render inline as thumbnails via the blob endpoint.

### Modal / Confirm Dialog
Centered panel (max-w-sm), rounded-md, `surface` paper, `subtle` border, `shadow-modal`, p-6, over a fixed `overlay` backdrop. Title is mono Title-case; actions are right-aligned secondary (cancel) + solid (confirm, red for destructive). The input variant auto-focuses its single field.

## Do's and Don'ts

### Do:
- **Do** let every identifier (DIDs, CIDs, codes, rkeys, handles) render in text-xs monospace, truncated with a tooltip where wide.
- **Do** reserve charcoal flip-chips + amber phosphor for live values and the active state; put dense data on ruled paper.
- **Do** use tracked small-caps mono only for board headers — eyebrows, table headers, field labels, badges.
- **Do** keep one amber primary action per view; color every other action semantically (green/red/yellow).
- **Do** run the JSON colors so record payloads stay scannable; keep wide tables scrolling horizontally instead of wrapping.

### Don't:
- **Don't** reintroduce blue anywhere — the accent is amber.
- **Don't** use amber for destructive actions — destructive is always red.
- **Don't** uppercase or letter-space body text; that register belongs to board headers only.
- **Don't** scatter multiple animations — one mechanical flip on value change, and honor `prefers-reduced-motion`.
- **Don't** render JSON or identifiers in proportional type.
- **Don't** add gradients, illustrations, or ambient shadows to resting surfaces.
