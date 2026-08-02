---
name: PDS Admin
description: A restrained, technical control room for a self-hosted ATProto PDS instance.
colors:
  primary: "#2563eb"
  primary-hover: "#1d4ed8"
  focus-ring: "#3b82f6"
  accent-indigo: "#6366f1"
  accent-yellow: "#eab308"
  success: "#16a34a"
  success-deep: "#15803d"
  danger: "#dc2626"
  danger-deep: "#b91c1c"
  warning-bg: "#fef9c3"
  warning-text: "#854d0e"
  page-bg: "#f9fafb"
  surface: "#ffffff"
  border-subtle: "#e5e7eb"
  border-input: "#d1d5db"
  hover-bg: "#f3f4f6"
  row-hover: "#f9fafb"
  overlay: "rgba(0, 0, 0, 0.4)"
  text-primary: "#111827"
  text-secondary: "#6b7280"
  text-muted: "#9ca3af"
  data-string: "#15803d"
  data-number: "#2563eb"
  data-boolean: "#9333ea"
  data-null: "#9ca3af"
typography:
  headline:
    fontFamily: "ui-sans-serif, system-ui, sans-serif"
    fontSize: "1.5rem"
    fontWeight: 700
  title:
    fontFamily: "ui-sans-serif, system-ui, sans-serif"
    fontSize: "1.125rem"
    fontWeight: 600
  body:
    fontFamily: "ui-sans-serif, system-ui, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
  label:
    fontFamily: "ui-sans-serif, system-ui, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
    color: "{colors.text-secondary}"
  mono:
    fontFamily: "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace"
    fontSize: "0.75rem"
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
    backgroundColor: "{colors.primary}"
    textColor: "{colors.surface}"
    rounded: "{rounded.md}"
    padding: "8px 16px"
  button-primary-hover:
    backgroundColor: "{colors.primary-hover}"
  button-danger:
    backgroundColor: "{colors.danger}"
    textColor: "{colors.surface}"
    rounded: "{rounded.md}"
    padding: "8px 16px"
  button-danger-hover:
    backgroundColor: "{colors.danger-deep}"
  input:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.md}"
    padding: "8px 12px"
  nav-link-active:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.surface}"
    rounded: "{rounded.sm}"
    padding: "8px 12px"
  card:
    backgroundColor: "{colors.surface}"
    rounded: "{rounded.md}"
    padding: "20px"
---

# Design System: PDS Admin

## Overview

**Creative North Star: "The Control Room"**

A quiet operations console for a self-hosted PDS. Every element exists to be read
or acted on; nothing exists to be admired. The system is deliberately flat,
dense, and data-forward: white surfaces separated by hairline rules, one blue
action per context, and monospace wherever the operator must copy an identifier
verbatim.

The aesthetic philosophy is restraint-as-respect: the operator is usually pasting
DIDs, reading CID prefixes, or deciding whether to act on an account. Typography
is small and legible (12–14px body, 24px page titles), spacing is tight but never
cramped, and color is reserved for meaning — blue for the single primary action,
red for destructive, green for confirmation, yellow for warnings. There is no
gradient, no ambient illustration, no decorative flourish. This is not an absence
of design; it is a design that disappears so the data can be inspected.

**Key Characteristics:**
- Flat white surfaces divided by gray-200 hairline borders, never shadows
- A single blue-600 primary action per context; red is destructive, never blue
- Monospace + text-xs for every identifier: DIDs, CIDs, codes, rkeys
- Small dense text (12–14px), tight 4px–24px spacing rhythm
- Semantic JSON colors so record payloads are scannable at a glance

## Colors

A cool, low-chroma operational palette: neutral grays carry the structure, blue
carries the action, and red/green/yellow carry only meaning.

### Primary
- **Operational Blue** (#2563eb): the single primary action per context — buttons, active nav, links, focus ring (blue-500 #3b82f6). Hover deepens to blue-700 (#1d4ed8).

### Secondary
- **Edit Indigo** (#6366f1): secondary account-editing actions (update email/handle).
- **Reset Yellow** (#eab308): the lone password-reset action, visually distinct from both primary and destructive.

### Tertiary
- **Success Green** (#16a34a): confirmation messages, "enable invites", JSON string values (deep #15803d).
- **Danger Red** (#dc2626): destructive actions (takedown, delete, disable), errors, never blue. Stronger red-700 (#b91c1c) for permanent deletes.

### Neutral
- **Page Gray** (#f9fafb): application background and row hover.
- **Panel White** (#ffffff): cards, tables, inputs, sidebar, modals.
- **Surface Gray** (#f3f4f6): hover fills, secondary/ghost buttons, segmented-control track.
- **Hairline Gray** (#e5e7eb): all borders and table rules.
- **Field Gray** (#d1d5db): input borders.
- **Muted Gray** (#9ca3af): de-emphasized text, empty states, caret glyphs, null values.
- **Label Gray** (#6b7280): field labels and secondary text.
- **Ink Gray** (#111827): primary text.

### Named Rules
**The Flat-By-Default Rule.** Surfaces are flat with hairline borders at rest. Shadows appear only as a response to state — card lift (shadow-sm), modal layering (shadow-xl).

**The One-Action Blue Rule.** blue-600 marks exactly one primary action per view. Destructive, warning, and editing actions use their semantic color, never blue.

**The Mono-For-Identity Rule.** Anything the operator must copy verbatim — DIDs, CIDs, invite codes, record keys, handles — renders in monospace at text-xs. The font itself says "this is data."

## Typography

**Display Font:** None — the system uses the Tailwind default UI sans stack (ui-sans-serif, system-ui, sans-serif) at every size.
**Label/Mono Font:** Default UI monospace stack (ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace) for identifiers, JSON, and raw record payloads.

**Character:** Quiet, dense, and unassuming. No display face, no letter-spacing games, no uppercase styling. Hierarchy is communicated by size and weight alone, and the palette stays tight so record payloads remain the loudest thing on screen.

### Hierarchy
- **Headline** (700, 24px/1.2): page titles — "Accounts", "Invite Codes", "Subject Status".
- **Title** (600, 18px/1.2): section and card headers ("Repo Collections", "Account Invite Codes").
- **Body** (400, 14px/1.5): table cells, button labels, form text. Works at 14px for readability; tables scroll horizontally rather than wrap.
- **Label** (400, 14px, gray-600): field labels and card field captions.
- **Mono** (400, 12px/1.4): identifiers, JSON tree, raw payloads, timestamps.

### Named Rules
**The Flat-Type Rule.** No letter-spacing, no uppercase, no custom weights beyond 400/600/700. Size and color carry all emphasis.

## Layout

A single-column operations layout under a fixed left sidebar. The app shell is a full-height flex with a gray-50 canvas. The sidebar is white, 224px (w-56), with a right hairline border and its own brand block ("PDS Admin", text-lg bold) at the top; navigation and a logout row below. On screens below `md` the sidebar becomes an off-canvas drawer (slide-in with a black/40 backdrop and a hamburger toggle); the mobile header row repeats the brand.

Content lives in a main column (p-4, md:p-6) with a 24px page title at the top. Data surfaces are white cards with rounded-md corners and hairline borders: stat cards in a `grid-cols-1 md:grid-cols-3` row, tables full-width with `overflow-x-auto` so wide record/account tables scroll on small screens, and forms capped at `max-w-lg`. Account action buttons sit in a `grid-cols-2 sm:grid-cols-3` grid.

**Spacing rhythm:** 4px (xs) for inline gaps and badge padding, 8px (sm) for button/input padding and nav gaps, 12px for table cell padding, 16px (md) for standard gaps and section margins, 20px (lg) for card padding, 24px (xl) for page-level margins.

## Elevation & Depth

Flat by default. Cards, tables, inputs, and the sidebar are flat white surfaces separated by hairline borders — depth is communicated by rule, not shadow. Elevation appears in exactly two places, both state-driven:

- **Card lift** (`box-shadow: 0 1px 2px 0 rgb(0 0 0 / 0.05)`): a whisper of depth on dashboard stat cards and form cards, always paired with a border.
- **Modal layer** (`box-shadow: 0 20px 25px -5px rgb(0 0 0 / 0.1), 0 8px 10px -6px rgb(0 0 0 / 0.1)`): modals and confirm dialogs float above a 40% black backdrop, the only place shadows do heavy lifting.

### Named Rules
**The No-Ambient-Shadow Rule.** Resting content never casts a shadow. If a surface sits on the page, it earns its separation with a hairline border; shadows are reserved for floating layers.

## Shapes

A soft-rectangle form language with exactly two radii. **Rounded-sm (4px)** for small controls — badges, chips, collection pills, nav links, the disclosure caret. **Rounded-md (8px)** for anything larger — buttons, inputs, cards, tables, modals, the segmented control. Everything is rectangle-with-mild-corners; there are no pills, circles, or clipped geometries. Borders are 1px hairlines in gray-200 (or gray-300 on inputs), drawn on every surface boundary. Table rows divide on gray-200; the hover row tints gray-50.

## Components

### Buttons
- **Shape:** soft rectangle, rounded-md (8px), 1px borders on light variants, height ~36px with px-4 py-2 (small variants px-3 py-1/py-1.5 text-xs).
- **Primary:** flat blue-600 fill, white text (text-sm, medium). Hover deepens to blue-700. Disabled fades to 50% opacity.
- **Secondary / Outline:** white fill, ink text, gray-300 border; hover fills gray-50.
- **Ghost:** gray-100 fill, gray-700 text, gray-300 border; hover fills gray-200. Used for modal cancels and "remove takedown".
- **Destructive:** red-600 fill, white text; hover red-700. Permanent delete escalates to red-700 → red-800 hover.
- **Text-only action:** links and light actions ("View", "Disable", "Load more") are blue-600 or red-600 text at text-xs/medium, no fill, hover darkening one step.

### Chips / Badges
- **Style:** text-xs, rounded-sm (4px), px-2 py-0.5. Warning badge: yellow-100 fill, yellow-800 text. Neutral badge ("deactivated"): gray-100 fill, gray-600 text.
- **Collection pills:** text-xs monospace, gray-50 fill, gray-200 border; hover tints blue-50 with a blue-300 border — the only hover that admits blue outside a primary action.

### Cards / Containers
- **Corner Style:** rounded-md (8px).
- **Background:** white, always paired with a gray-200 hairline border.
- **Shadow Strategy:** flat at rest; optional shadow-sm only on stat/action surfaces. See The No-Ambient-Shadow Rule.
- **Internal Padding:** 20px (lg).

### Inputs / Fields
- **Style:** white fill, gray-300 border, rounded-md (8px), px-3 py-2, ink text.
- **Focus:** border shifts to blue-500 with the native outline removed — the entire focus treatment.
- **Error / Disabled:** errors surface as red-600 text beside the field, not as field states; disabled buttons fade to 50% opacity.

### Navigation
- **Style:** left rail (224px) or mobile drawer; text-sm.
- **Default:** gray-600 text. **Hover:** gray-100 fill. **Active:** flat blue-600 fill, white text, rounded-sm. Logout is a muted text row at the rail bottom. **Mobile:** off-canvas drawer over a black/40 backdrop with a hamburger toggle in the header row.

### Table (signature)
The system's primary instrument. White card, rounded-md, hairline border, `overflow-x-auto`. Header row: gray-500 text, medium, left-aligned, p-3, hairline bottom rule. Body rows: p-3, hairline rules between rows, gray-50 hover. Identifiers render text-xs monospace with truncation (max-w) and a `title` tooltip; timestamps use `toLocaleString()`. Expandable invite rows slide in a nested gray-50 detail row with its own sub-table.

### JsonTree (signature)
A semantic JSON inspector, text-xs monospace, indent per level via a 1px gray-200 left rule. Keys are gray-600; values carry type color — string green-700, number blue-600, boolean purple-600, null gray-400. Objects/arrays show their size in muted gray with a ▼/▶ caret and expand to depth 2 by default. Image blobs render inline as thumbnails via the blob endpoint.

### Modal / Confirm Dialog
Centered panel (max-w-sm, mx-4), rounded-md, white, gray-200 border, shadow-xl, p-6, over a fixed 40% black backdrop. Title is text-lg semibold; actions are right-aligned ghost (cancel) + solid (confirm, red for destructive) buttons. The input variant auto-focuses its single field.

## Do's and Don'ts

### Do:
- **Do** keep every data surface flat and hairline-bordered; add shadow only for state or floating layers.
- **Do** render identifiers (DIDs, CIDs, codes, rkeys, handles) in text-xs monospace with truncation and a tooltip.
- **Do** use exactly one blue-600 primary action per view; color every other action semantically.
- **Do** let wide tables scroll horizontally (`overflow-x-auto`) instead of wrapping cells on small screens.
- **Do** use the semantic JSON colors so record payloads stay scannable at a glance.

### Don't:
- **Don't** add decorative color, gradients, illustrations, or ambient shadows to resting surfaces.
- **Don't** use blue for destructive actions — destructive is always red.
- **Don't** introduce display type, uppercase, letter-spacing, or weights outside 400/600/700.
- **Don't** let a view carry two competing primary actions.
- **Don't** render JSON or identifiers in proportional type.
