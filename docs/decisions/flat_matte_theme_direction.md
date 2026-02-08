# Flat Matte Theme Direction

Moving from the current sci-fi glow aesthetic to a **flat matte** language. This doc catalogs what needs to change and proposes a direction.

---

## Current Sci-Fi Effects (What to Kill)

| Category           | Where                                                                                                                       | Specifics                                                                    |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| **Gradient title** | [Header.tsx](file:///Users/baxter/devProject/DiagnosticStructuralLens/dashboard/src/components/Header.tsx#L43)              | `bg-gradient-to-r from-blue-400 to-cyan-400` on "Diagnostic Structural Lens" |
| **Logo glow**      | [Header.tsx](file:///Users/baxter/devProject/DiagnosticStructuralLens/dashboard/src/components/Header.tsx#L40)              | `bg-gradient-to-br from-blue-500 to-cyan-500 shadow-lg shadow-blue-500/20`   |
| **Backdrop blur**  | Header, DetailsPanel, TimeControls, App.tsx                                                                                 | `backdrop-blur`, `backdrop-filter: blur(8px)` everywhere                     |
| **Translucent bg** | Multiple components                                                                                                         | `bg-slate-900/50`, `bg-slate-800/50`, `rgba(26,29,35,0.8)` panels            |
| **Glow shadows**   | [design-tokens.css](file:///Users/baxter/devProject/DiagnosticStructuralLens/dashboard/src/design-tokens.css#L51)           | `--shadow-glow: 0 0 12px rgba(122,155,163,0.3)`                              |
| **Hub node glow**  | [index.css](file:///Users/baxter/devProject/DiagnosticStructuralLens/dashboard/src/index.css#L133)                          | `box-shadow: 0 0 16px rgba(251,191,36,0.2)` on `.card-node--hub`             |
| **Diff glow**      | [CardNode.tsx](file:///Users/baxter/devProject/DiagnosticStructuralLens/dashboard/src/components/CardNode.tsx#L218)         | `boxShadow: 0 0 12px rgba(...)` for added/modified                           |
| **Edge pulse**     | [index.css](file:///Users/baxter/devProject/DiagnosticStructuralLens/dashboard/src/index.css#L335)                          | `@keyframes edge-pulse` with animated stroke-opacity                         |
| **Timeline thumb** | [TimeControls.tsx](file:///Users/baxter/devProject/DiagnosticStructuralLens/dashboard/src/components/TimeControls.tsx#L115) | `shadow-[0_0_10px_rgba(34,211,238,0.5)]` cyan glow                           |
| **Coverage dot**   | [DetailsPanel.tsx](file:///Users/baxter/devProject/DiagnosticStructuralLens/dashboard/src/components/DetailsPanel.tsx#L100) | `shadow-[0_0_8px_rgba(34,197,94,0.6)]` green glow                            |
| **Button glow**    | DetailsPanel drill buttons                                                                                                  | `shadow-lg shadow-cyan-900/20`, `shadow-violet-900/20`                       |

---

## Proposed Flat Matte Principles

### 1. Opaque Surfaces Only

Replace every `bg-*/50`, `bg-*/80`, `bg-*/95`, and `rgba()` background with **100% opaque values**. No glass, no blur.

```diff
- bg-slate-900/50 backdrop-blur
+ bg-[#13151a]            ← solid, no transparency
```

### 2. Zero Glow, Minimal Shadow

- Delete `--shadow-glow` entirely
- Replace `box-shadow` depth effects with **single-layer, low-opacity** drop shadows (e.g., `0 1px 3px rgba(0,0,0,0.3)`) for separation only — or remove shadows entirely and rely on **border + background contrast**
- Remove all animated pulses/glow keyframes

### 3. Solid Color Accents Instead of Gradients

- Title: solid `text-slate-100` or a single muted accent (your existing `--color-primary: #7a9ba3`)
- Logo square: solid `bg-[#7a9ba3]` instead of gradient
- Buttons: solid accent colors without glow shadows

### 4. Flat Borders Instead of Luminous Edges

- Nodes: keep the `border-left` color coding, but make the card body `bg-[#1a1d24]` with `border: 1px solid #2a3038` — no elevation shadow
- Selected state: thicker border or background tint, not box-shadow rings
- Hub indicator: solid thicker border, no glow

### 5. Muted, Earthy Palette (Optional Shift)

The current token colors are already muted. You could lean further into it:

| Token                  | Current             | Flat Matte Option                  |
| ---------------------- | ------------------- | ---------------------------------- |
| `--color-bg-dark`      | `#0d0e12`           | `#111318` (slightly warmer)        |
| `--color-surface-dark` | `#16181d`           | `#1a1d24`                          |
| `--color-primary`      | `#7a9ba3`           | Keep or warm slightly to `#7a9a96` |
| Card bg                | `#1e293b` (blueish) | `#1d2028` (neutral charcoal)       |

### 6. Edge / Link Styling

- Solid strokes (no animated dash, no pulse)
- Violation edges: solid red/orange stroke, thicker weight — not pulsing
- Selection highlight: brighter stroke color, not glow

---

## Open Questions

1. **How matte?** Should we keep _any_ subtle depth (1px shadow for card lift) or go fully flat like Material Design's "outlined" cards?
2. **Accent palette**: Stay with the current cool teal `#7a9ba3` or shift warmer (e.g., to a muted sand/sage direction)?
3. **Node hover**: Currently uses `translateY(-1px)` + shadow. Replace with just a background tint change, or keep the slight lift?
4. **Dark level**: Current `#0d0e12` is very dark. Want to bring it up to `#15171c` range, or keep the deep void?
5. **Button style**: Rounded-lg with solid fill (current) works for flat. Strip just the glow shadows, or also reduce border-radius for a more utilitarian feel?
