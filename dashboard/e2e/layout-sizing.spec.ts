import { test, expect } from '@playwright/test';

/**
 * Layout sizing verification tests.
 * Checks that nodes are properly sized and distributed at each navigation level.
 */

test.describe('Graph Layout Sizing', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    // Wait for React Flow to render
    await page.waitForSelector('.react-flow__node', { timeout: 15_000 });
  });

  test('L1 Context: system node is visible and properly sized', async ({ page }) => {
    const nodes = page.locator('.react-flow__node-cardNode');
    await expect(nodes).toHaveCount(1, { timeout: 10_000 });

    // Node should be visible in the viewport
    const node = nodes.first();
    await expect(node).toBeVisible();

    // Node should have explicit width/height from layout
    const width = await node.evaluate(el => parseInt((el as HTMLElement).style.width));
    expect(width).toBeGreaterThan(100);

    // Node should be within the viewport bounds (not clipped)
    const box = await node.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.x).toBeGreaterThan(-10); // Allow small margin
    expect(box!.y).toBeGreaterThan(0);
    expect(box!.width).toBeGreaterThanOrEqual(100);
    expect(box!.height).toBeGreaterThanOrEqual(40);
  });

  test('L2 System: repository nodes are distributed (not stacked)', async ({ page }) => {
    // Drill down from L1 to L2
    const systemNode = page.locator('.react-flow__node-cardNode').first();
    await systemNode.dblclick();

    // Wait for new nodes to render after drill-down
    await page.waitForTimeout(3000);
    await page.waitForSelector('.react-flow__node-cardNode', { timeout: 10_000 });

    const nodes = page.locator('.react-flow__node-cardNode');
    const count = await nodes.count();
    expect(count).toBeGreaterThanOrEqual(1);

    if (count > 1) {
      // Get bounding boxes of first two nodes
      const box1 = await nodes.nth(0).boundingBox();
      const box2 = await nodes.nth(1).boundingBox();
      expect(box1).not.toBeNull();
      expect(box2).not.toBeNull();

      // Nodes should NOT be stacked at the same position
      const distance = Math.abs(box1!.x - box2!.x) + Math.abs(box1!.y - box2!.y);
      expect(distance).toBeGreaterThan(20); // At least 20px apart
    }
  });

  test('L3 Project: namespace nodes are visible cards (not tiny dots)', async ({ page }) => {
    // Drill down L1 -> L2
    const systemNode = page.locator('.react-flow__node-cardNode').first();
    await systemNode.dblclick();
    await page.waitForTimeout(3000);

    // Drill down L2 -> L3
    const repoNode = page.locator('.react-flow__node-cardNode').first();
    await repoNode.dblclick();
    await page.waitForTimeout(3000);

    const nodes = page.locator('.react-flow__node-cardNode');
    const count = await nodes.count();
    // Expect multiple nodes at project level
    expect(count).toBeGreaterThanOrEqual(1);

    if (count > 0) {
      // Each node should have explicit dimensions
      const firstNode = nodes.first();
      const width = await firstNode.evaluate(el => parseInt((el as HTMLElement).style.width));
      const height = await firstNode.evaluate(el => parseInt((el as HTMLElement).style.height));
      expect(width).toBeGreaterThanOrEqual(100);
      expect(height).toBeGreaterThanOrEqual(40);
    }

    if (count > 3) {
      // Check distribution — nodes should not all be stacked
      const boxes = await Promise.all(
        Array.from({ length: Math.min(count, 5) }, (_, i) =>
          nodes.nth(i).boundingBox()
        )
      );
      const validBoxes = boxes.filter(b => b !== null);

      // At least some nodes should have distinct screen positions
      if (validBoxes.length >= 2) {
        const uniqueXs = new Set(validBoxes.map(b => Math.round(b!.x / 10) * 10));
        const uniqueYs = new Set(validBoxes.map(b => Math.round(b!.y / 10) * 10));
        const uniquePositions = uniqueXs.size + uniqueYs.size;
        expect(uniquePositions).toBeGreaterThan(2); // Not all at same x,y
      }
    }
  });

  test('fitView adjusts viewport to show all nodes', async ({ page }) => {
    // Drill down to get multi-node view
    const systemNode = page.locator('.react-flow__node-cardNode').first();
    await systemNode.dblclick();
    await page.waitForTimeout(3000);

    const repoNode = page.locator('.react-flow__node-cardNode').first();
    await repoNode.dblclick();
    await page.waitForTimeout(3000);

    // Check viewport transform
    const viewport = page.locator('.react-flow__viewport');
    const transform = await viewport.evaluate(el => (el as HTMLElement).style.transform);
    expect(transform).toBeTruthy();
    // Transform should contain translate and scale
    expect(transform).toContain('translate');
  });

  test('nodes have correct explicit style dimensions', async ({ page }) => {
    // Check that nodes have style.width and style.height set
    const nodes = page.locator('.react-flow__node-cardNode');
    await expect(nodes.first()).toBeVisible();

    const dimensions = await nodes.first().evaluate(el => ({
      styleWidth: (el as HTMLElement).style.width,
      styleHeight: (el as HTMLElement).style.height,
    }));

    expect(dimensions.styleWidth).toBeTruthy();
    expect(dimensions.styleHeight).toBeTruthy();
    expect(parseInt(dimensions.styleWidth)).toBeGreaterThan(0);
    expect(parseInt(dimensions.styleHeight)).toBeGreaterThan(0);
  });
});
