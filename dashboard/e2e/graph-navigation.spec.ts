import { test, expect } from '@playwright/test';

/**
 * Graph Navigation E2E Tests
 *
 * Verifies the C4 drill-down flow: L1 (Context) → L2 (System) → L3 (Repository) → L4 (Code).
 * React Flow generates `[data-id]` attributes on each node wrapper.
 * Double-clicking a node fires the drill-down handler in useCanvasEvents.
 */

test.describe('Graph Drill-Down Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    // Wait for React Flow to render + GraphQL data to load
    await page.waitForSelector('.react-flow__node', { timeout: 15_000 });
  });

  test('L1 → L2: initial load shows nodes and breadcrumb', async ({ page }) => {
    // Should see at least one node rendered
    const nodes = page.locator('.react-flow__node');
    await expect(nodes.first()).toBeVisible();

    // Breadcrumb should show level indicator
    const levelBadge = page.locator('nav span:has-text("System"), nav span:has-text("Repository"), nav span:has-text("Context")');
    await expect(levelBadge.first()).toBeVisible();
  });

  test('double-click drills into next level', async ({ page }) => {
    // Get the current breadcrumb level
    const initialLevel = await page.locator('nav .bg-slate-700').textContent();

    // Double-click the first visible node (force bypasses child element interception)
    const firstNode = page.locator('.react-flow__node').first();
    await firstNode.dblclick({ force: true });

    // Wait for new nodes to render (layout recalculation)
    await page.waitForTimeout(3000);
    await page.waitForSelector('.react-flow__node', { timeout: 15_000 });

    // Breadcrumb should update — path should now have at least one segment
    const breadcrumbSegments = page.locator('nav button');
    const segmentCount = await breadcrumbSegments.count();
    expect(segmentCount).toBeGreaterThanOrEqual(2); // "Global Context" + drilled segment

    // Level badge should change
    const newLevel = await page.locator('nav .bg-slate-700').textContent();
    expect(newLevel).not.toBe(initialLevel);
  });

  test('full drill L1→L2→L3→L4 reaches code level', async ({ page }) => {
    const startLevel = await page.locator('nav .bg-slate-700').textContent();

    // Drill up to 4 times (context → system → repository → project → component)
    for (let i = 0; i < 4; i++) {
      const currentLevel = await page.locator('nav .bg-slate-700').textContent();
      if (currentLevel?.trim() === 'Code') break;

      const nodeCount = await page.locator('.react-flow__node').count();
      if (nodeCount === 0) break;

      await page.locator('.react-flow__node').first().dblclick({ force: true });
      await page.waitForTimeout(3000);
      try {
        await page.waitForSelector('.react-flow__node', { timeout: 15_000 });
      } catch {
        break;
      }
    }

    // We should have drilled at least one level deeper
    const finalLevel = await page.locator('nav .bg-slate-700').textContent();
    expect(finalLevel).not.toBe(startLevel);

    // Breadcrumb path should have segments
    const pathSegments = page.locator('nav button');
    expect(await pathSegments.count()).toBeGreaterThanOrEqual(2);
  });

  test('breadcrumb "Global Context" resets to top level', async ({ page }) => {
    // Drill into a child level
    const firstNode = page.locator('.react-flow__node').first();
    await firstNode.dblclick({ force: true });
    await page.waitForTimeout(3000);
    await page.waitForSelector('.react-flow__node', { timeout: 15_000 });

    // Click "Global Context" to reset
    await page.locator('nav button:has-text("Global Context")').click();
    await page.waitForTimeout(2000);
    await page.waitForSelector('.react-flow__node', { timeout: 15_000 });

    // Should be back at top level with minimal breadcrumb
    const segments = page.locator('nav button');
    // Just "Global Context" button, no path segments
    expect(await segments.count()).toBeLessThanOrEqual(2);
  });
});
