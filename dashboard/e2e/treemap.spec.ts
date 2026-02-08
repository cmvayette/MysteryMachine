import { test, expect } from '@playwright/test';

/**
 * Treemap View E2E Tests (Phase 7)
 *
 * Verifies the treemap toggle at L2 (Repository level), rectangle rendering,
 * labels, and click-to-drill navigation.
 *
 * Pre-condition: The app loads with data at L1 → auto-drills or is navigated
 * to L2 (system level), then to L3 (repository level), where containers appear.
 */

test.describe('Treemap View (P7)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    // Wait for initial graph render
    await page.waitForSelector('.react-flow__node', { timeout: 15_000 });

    // Drill to L2 system level by double-clicking first node
    const firstNode = page.locator('.react-flow__node').first();
    await firstNode.dblclick({ force: true });
    await page.waitForTimeout(3000);
    await page.waitForSelector('.react-flow__node', { timeout: 15_000 });

    // Drill to L3 repository level (containers visible)
    const nextNode = page.locator('.react-flow__node').first();
    await nextNode.dblclick({ force: true });
    await page.waitForTimeout(3000);
    await page.waitForSelector('.react-flow__node', { timeout: 15_000 });
  });

  test('toggle button visible at repository level', async ({ page }) => {
    const toggle = page.locator('[data-testid="treemap-toggle"]');
    await expect(toggle).toBeVisible({ timeout: 5_000 });
  });

  test('treemap renders SVG rectangles after toggle', async ({ page }) => {
    const toggle = page.locator('[data-testid="treemap-toggle"]');
    await toggle.click();
    await page.waitForTimeout(1000);

    const treemapView = page.locator('[data-testid="treemap-view"]');
    await expect(treemapView).toBeVisible({ timeout: 5_000 });

    const cells = page.locator('.treemap-rect');
    const count = await cells.count();
    expect(count).toBeGreaterThan(0);
  });

  test('rectangle labels contain container names', async ({ page }) => {
    const toggle = page.locator('[data-testid="treemap-toggle"]');
    await toggle.click();
    await page.waitForTimeout(1000);

    const labels = page.locator('.treemap-label');
    const count = await labels.count();
    expect(count).toBeGreaterThan(0);

    // At least one label should have non-empty text
    const firstLabel = await labels.first().textContent();
    expect(firstLabel?.length).toBeGreaterThan(0);
  });

  test('clicking a rectangle drills down to namespace level', async ({ page }) => {
    const toggle = page.locator('[data-testid="treemap-toggle"]');
    await toggle.click();
    await page.waitForTimeout(1000);

    // Get current breadcrumb state
    const breadcrumbBefore = await page.locator('nav').textContent();

    // Click the first treemap cell
    const firstCell = page.locator('.treemap-cell').first();
    await firstCell.click();
    await page.waitForTimeout(3000);

    // After drill-down, treemap should be replaced by the graph view at L4
    // and breadcrumb should have changed
    const breadcrumbAfter = await page.locator('nav').textContent();
    expect(breadcrumbAfter).not.toBe(breadcrumbBefore);
  });
});
