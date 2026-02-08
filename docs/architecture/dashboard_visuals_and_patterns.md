This document defines the authoritative UX and visual standards for the Diagnostic Structural Lens (DSL) dashboard, grounded in architectural legibility, viewport stability, and the **Deep Void** design system.

## 0. Design Philosophy (The Tactical Instrument)

The DSL dashboard is a tactical instrument for engineers.

- **Aesthetic**: "Deep Void" (Tactical Dark Mode). High contrast for data, receding backgrounds for containers.
- **Density**: High. Visualizes complex systems without generous whitespace.
- **Motion**: Instant (0ms) or fast (350ms) to ensure zero perceived latency.

## 1. Tactical Palette (OKLCH-native)

We utilize the core Digital Backbone OKLCH palette, ensuring consistency across the platform.

### 1.1 Backgrounds & Surfaces

| Surface    | Token          | Value                    | Usage                           |
| :--------- | :------------- | :----------------------- | :------------------------------ |
| **Canvas** | `--bg-canvas`  | `oklch(16% 0.04 265deg)` | Infinite graph background.      |
| **Panels** | `--bg-panel`   | `oklch(20% 0.06 265deg)` | Floating toolbars and sidebars. |
| **Active** | `--bg-surface` | `oklch(28% 0.07 265deg)` | Hover states, selected items.   |

### 1.2 Core Tones

Finalized desaturated tones used for structural differentiation in the graph:

- **Repository**: `#f59e0b` (Amber-500)
- **Container (Project)**: `#0ea5e9` (Sky-500)
- **Namespace**: `#8b5cf6` (Violet-500)
- **Atom (Component)**: Desaturated tones (e.g., `#a08cba` for DTOs) to minimize visual noise at high density.
- **Risk/Violation**: Consistent tactical palette (Red/Orange/Yellow) for architectural risk and rule failures.

## 2. Visual Mapping: The C4 Standard

The system uses specific SVG paths and Material Symbols to represent architectural tiers.

| Tier   | Type         | Symbol          | SVG Shape        | C4 Context          |
| :----- | :----------- | :-------------- | :--------------- | :------------------ |
| **L1** | `repository` | `hexagon`       | Hexagon          | Software System     |
| **L2** | `container`  | `dns`           | Rounded Rect     | Container (Project) |
| **L3** | `namespace`  | `folder_open`   | Rounded Rect     | Component Grouping  |
| **L4** | `interface`  | `circle`        | Hollow Circle    | Component Contract  |
| **L4** | `class`      | `square`        | Solid Square     | Class / Component   |
| **L4** | `service`    | `settings`      | Octagon          | Service Component   |
| **L4** | `dto`        | `data_object`   | Notched Square   | Data Component      |
| **L4** | `record`     | `receipt_long`  | Rounded Rect (L) | Information Carrier |
| **L4** | `enum`       | `list`          | Horizontal Bars  | Type Set            |
| **L4** | `struct`     | `diamond`       | Diamond          | Value Type          |
| **L4** | `delegate`   | `arrow_forward` | Arrow            | Function Pointer    |
| **DB** | `table`      | `table_chart`   | Cylinder         | Database Table      |
| **DB** | `storedproc` | `code`          | Terminal         | Stored Procedure    |

## 2. Dynamic Visual Feedback

### 2.1 Level of Detail (LOD)

Label visibility is dynamically toggled based on the zoom scale ($k$):

- **Threshold**: `1.2`
- **Behavior**: Under $1.2$, node labels (text) have `opacity: 0`. Above $1.2$, they are revealed.

### 2.2 Semantic Highlighting

- **Violations**: Edges in Governance Mode that represent rule violations use `stroke-dasharray: '4,2'` and a `pulse-edge` animation.
- **Corrosion (Entropy)**: High-risk nodes (risk score $> 0.7$) apply a SVG `feTurbulence` filter (ID: `corrosion`) to visually represent technical debt as "rust" or "jitter" on the node shape.
- **Blast Radius**: Affected nodes are colored using a red-to-orange gradient based on their distance from the selection.
- **Salience**: Nodes scale in size based on a strict C4 hierarchy:
  - **L1 Repository**: 140px (Width)
  - **L2 Container**: 120px (Width)
  - **L3 Namespace**: 100px (Width)
  - **L4 Atom**: 100px (Width)

## 3. Interaction Design & Navigation

### 3.1 Layout Engine: ELK.js

DSL has transitioned from D3.js Force Graphs to **React Flow** powered by **ELK.js**. This provides deterministic, layered layouts that respect call-flow direction.

- **L1-L3 Strategy**: Uses `elk.algorithm: layered` with orthogonal edge routing.
- **Direction**: `DOWN` for federation/repository levels, `RIGHT` for namespace internal flows.

### 3.2 Viewport Stabilization: The Remount Pattern

To ensure the graph always fits the viewport after a data swap (drill-down or drill-up), the dashboard uses the **Navigation Remount Pattern**.

- **Trigger**: The `<ReactFlow>` component is keyed by the navigation state: `key={level + path.join('/')}`.
- **Behavior**: Changing level/path causes a complete remount.
- **Benefit**: Naturally triggers React Flow's internal `fitView` on mount, avoiding race conditions between async layout (ELK) and DOM measurement.

### 3.3 Drill-Down Animation: Sibling Fade

High-density transitions are smoothed by a 350ms animation sequence:

1. **Phase 1**: All nodes except the double-clicked target receive the `.node-fade-out` CSS class.
2. **Phase 2**: After 350ms, the navigation state is updated.
3. **Phase 3**: The new level mounts with a fresh `fitView`, and nodes slide/fade in using standard framer-motion or CSS transitions.

## 4. Grid Layout Fallback (The "Clean Room" Mode)

ELK's `layered` algorithm fails to spread out disconnected nodes (stacking them in a single column). To prevent this "Stacked Node" failure, the dashboard implements a manual **Grid Fallback**.

- **Trigger**: Activated for dense, sparsely connected graphs where:
  - `nodes.length > 20`
  - `links.length < nodes.length * 0.3`
- **Logic**:
  - **Columns**: `cols = ceil(sqrt(nodes.length))`.
  - **Spacing**: Horizontal `240px`, Vertical `112px`.
- **Benefit**: Ensures that L2/L3 levels with many disconnected services (containers) are immediately readable without overlapping.

## 5. Control Consolidation

Toggles for **Links**, **Blast Radius**, **Diff Mode**, and **Governance** are consolidated into a single unified toolbar (Bottom-Left) to maintain the "Control Room" aesthetic.

## 6. Text Legibility: The "Paint-Order" Outline

To ensure label readability against high-density graph backgrounds, use the `paint-order` SVG property.

- **Standard**:
  - `fill`: `#e2e8f0` (High contrast text)
  - `stroke`: `#0f172a` (Background depth color)
  - `stroke-width`: `3px`
  - `paint-order`: `stroke`
- **Result**: A clean 3px dark border around the letters that prevents "clash" with overlapping lines.
