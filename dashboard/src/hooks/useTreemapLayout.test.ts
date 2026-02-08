import { describe, it, expect } from 'vitest';
import { squarify, type TreemapItem, type Rect } from './useTreemapLayout';

const bounds: Rect = { x: 0, y: 0, width: 800, height: 600 };

describe('squarify (treemap algorithm)', () => {
  it('returns empty array for empty input', () => {
    expect(squarify([], bounds)).toEqual([]);
  });

  it('single item fills the entire bounds', () => {
    const items: TreemapItem[] = [{ id: 'a', name: 'Alpha', value: 100 }];
    const rects = squarify(items, bounds);
    expect(rects).toHaveLength(1);
    expect(rects[0].x).toBe(bounds.x);
    expect(rects[0].y).toBe(bounds.y);
    expect(rects[0].width).toBeCloseTo(bounds.width);
    expect(rects[0].height).toBeCloseTo(bounds.height);
  });

  it('two equal items produce two rects with roughly 50% area each', () => {
    const items: TreemapItem[] = [
      { id: 'a', name: 'A', value: 50 },
      { id: 'b', name: 'B', value: 50 },
    ];
    const rects = squarify(items, bounds);
    expect(rects).toHaveLength(2);

    const totalArea = bounds.width * bounds.height;
    const areaA = rects[0].width * rects[0].height;
    const areaB = rects[1].width * rects[1].height;
    expect(areaA).toBeCloseTo(totalArea / 2, 0);
    expect(areaB).toBeCloseTo(totalArea / 2, 0);
  });

  it('aspect ratios stay reasonable (≤ 4) for varied data', () => {
    const items: TreemapItem[] = [
      { id: 'a', name: 'Large', value: 200 },
      { id: 'b', name: 'Medium', value: 80 },
      { id: 'c', name: 'Small', value: 30 },
      { id: 'd', name: 'Tiny', value: 10 },
    ];
    const rects = squarify(items, bounds);
    expect(rects).toHaveLength(4);

    for (const r of rects) {
      const ratio = Math.max(r.width / r.height, r.height / r.width);
      expect(ratio).toBeLessThanOrEqual(4);
    }
  });

  it('total area of all rects matches bounds area', () => {
    const items: TreemapItem[] = [
      { id: 'a', name: 'A', value: 100 },
      { id: 'b', name: 'B', value: 60 },
      { id: 'c', name: 'C', value: 40 },
      { id: 'd', name: 'D', value: 25 },
      { id: 'e', name: 'E', value: 15 },
    ];
    const rects = squarify(items, bounds);
    const totalRectArea = rects.reduce((sum, r) => sum + r.width * r.height, 0);
    const boundsArea = bounds.width * bounds.height;
    expect(totalRectArea).toBeCloseTo(boundsArea, 0);
  });

  it('largest item gets largest rectangle', () => {
    const items: TreemapItem[] = [
      { id: 'small', name: 'Small', value: 10 },
      { id: 'large', name: 'Large', value: 200 },
      { id: 'medium', name: 'Med', value: 50 },
    ];
    const rects = squarify(items, bounds);
    const largest = rects.reduce((max, r) =>
      r.width * r.height > max.width * max.height ? r : max
    );
    expect(largest.id).toBe('large');
  });
});
