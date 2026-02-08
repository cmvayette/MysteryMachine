// ── Squarified Treemap Layout ────────────────────────────────────────────────
// Bruls, Huizing & van Wijk (2000) — area-proportional rectangle packing
// with optimized aspect ratios.

export interface TreemapItem {
  id: string;
  name: string;
  value: number; // atomCount / consumerCount
}

export interface Rect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface TreemapRect extends Rect {
  id: string;
  name: string;
  value: number;
  color: string;
}

// ── Color palette (warm gradient by relative size) ───────────────────────────

const TREEMAP_COLORS = [
  '#0d9488', // teal-600 (smallest)
  '#0891b2', // cyan-600
  '#2563eb', // blue-600
  '#7c3aed', // violet-600
  '#c026d3', // fuchsia-600
  '#e11d48', // rose-600
  '#f59e0b', // amber-500 (largest)
];

function pickColor(index: number, total: number): string {
  if (total <= 1) return TREEMAP_COLORS[3];
  const bucket = Math.floor((index / (total - 1)) * (TREEMAP_COLORS.length - 1));
  return TREEMAP_COLORS[Math.min(bucket, TREEMAP_COLORS.length - 1)];
}

// ── Core algorithm ───────────────────────────────────────────────────────────

/**
 * Compute squarified treemap rectangles for the given items within bounds.
 * Items are sorted by value (descending) and assigned colors from cool→warm.
 */
export function squarify(items: TreemapItem[], bounds: Rect): TreemapRect[] {
  if (items.length === 0) return [];

  // Sort descending by value
  const sorted = [...items].sort((a, b) => b.value - a.value);
  const totalValue = sorted.reduce((sum, item) => sum + item.value, 0);

  if (totalValue <= 0) return [];

  const totalArea = bounds.width * bounds.height;
  const rects: TreemapRect[] = [];

  // Assign normalized areas
  const areas = sorted.map((item) => (item.value / totalValue) * totalArea);

  // Recursive squarified layout
  layoutStrip(sorted, areas, { ...bounds }, rects);

  // Assign colors (largest = warmest)
  for (let i = 0; i < rects.length; i++) {
    rects[i].color = pickColor(i, rects.length);
  }

  return rects;
}

// ── Layout engine ────────────────────────────────────────────────────────────

function layoutStrip(
  items: TreemapItem[],
  areas: number[],
  remaining: Rect,
  output: TreemapRect[]
): void {
  if (items.length === 0) return;

  if (items.length === 1) {
    output.push({
      id: items[0].id,
      name: items[0].name,
      value: items[0].value,
      x: remaining.x,
      y: remaining.y,
      width: remaining.width,
      height: remaining.height,
      color: '',
    });
    return;
  }

  // Determine orientation: lay out along the shorter side
  const isWide = remaining.width >= remaining.height;
  const sideLength = isWide ? remaining.height : remaining.width;

  // Greedily add items to the current row while aspect ratio improves
  const rowItems: TreemapItem[] = [];
  const rowAreas: number[] = [];
  let bestWorst = Infinity;

  let splitIndex = 0;
  for (let i = 0; i < items.length; i++) {
    const testAreas = [...rowAreas, areas[i]];
    const worst = worstAspectRatio(testAreas, sideLength);

    if (worst <= bestWorst) {
      rowItems.push(items[i]);
      rowAreas.push(areas[i]);
      bestWorst = worst;
      splitIndex = i + 1;
    } else {
      break;
    }
  }

  // Lay out the row
  const rowTotalArea = rowAreas.reduce((s, a) => s + a, 0);
  const rowThickness = sideLength > 0 ? rowTotalArea / sideLength : 0;

  let offset = 0;
  for (let i = 0; i < rowItems.length; i++) {
    const itemLength = rowThickness > 0 ? rowAreas[i] / rowThickness : 0;

    if (isWide) {
      output.push({
        id: rowItems[i].id,
        name: rowItems[i].name,
        value: rowItems[i].value,
        x: remaining.x,
        y: remaining.y + offset,
        width: rowThickness,
        height: itemLength,
        color: '',
      });
    } else {
      output.push({
        id: rowItems[i].id,
        name: rowItems[i].name,
        value: rowItems[i].value,
        x: remaining.x + offset,
        y: remaining.y,
        width: itemLength,
        height: rowThickness,
        color: '',
      });
    }
    offset += itemLength;
  }

  // Recurse with remaining items and reduced bounds
  const newRemaining = isWide
    ? {
        x: remaining.x + rowThickness,
        y: remaining.y,
        width: remaining.width - rowThickness,
        height: remaining.height,
      }
    : {
        x: remaining.x,
        y: remaining.y + rowThickness,
        width: remaining.width,
        height: remaining.height - rowThickness,
      };

  layoutStrip(
    items.slice(splitIndex),
    areas.slice(splitIndex),
    newRemaining,
    output
  );
}

/**
 * Worst aspect ratio in a row of rectangles laid out along `sideLength`.
 * Lower = more square-like = better.
 */
function worstAspectRatio(areas: number[], sideLength: number): number {
  if (areas.length === 0 || sideLength <= 0) return Infinity;

  const totalArea = areas.reduce((s, a) => s + a, 0);
  const rowThickness = totalArea / sideLength;

  let worst = 0;
  for (const area of areas) {
    const length = area / rowThickness;
    const ratio = Math.max(rowThickness / length, length / rowThickness);
    worst = Math.max(worst, ratio);
  }
  return worst;
}
