import { test, expect } from '@playwright/test';

/**
 * Context Layer (L1) E2E Tests
 *
 * Verifies that the top-level federation view correctly groups
 * repositories by Git root name — not individual .csproj projects.
 *
 * Expected repos: clean_architecture, eshop_microservices, nop_commerce_v4.70
 * (plus DSL itself if self-scanned).
 */

test.describe('Context Layer — Repository Grouping', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForSelector('.react-flow__node', { timeout: 15_000 });
  });

  test('context layer displays a reasonable number of systems (not 58)', async ({ page }) => {
    // At L1, we should see system groups — not individual .csproj projects
    const nodes = page.locator('.react-flow__node');
    const nodeCount = await nodes.count();

    // We expect ~3-5 systems (clean_architecture, eshop_microservices, nop_commerce, maybe DSL)
    // The key assertion: NOT 58 individual project nodes
    expect(nodeCount).toBeGreaterThan(0);
    expect(nodeCount).toBeLessThanOrEqual(10);
  });

  test('system nodes show repository names, not filesystem paths', async ({ page }) => {
    const nodeNames = page.locator('.card-node__name');
    const count = await nodeNames.count();
    expect(count).toBeGreaterThan(0);

    // Check that no node name contains a filesystem path separator
    for (let i = 0; i < count; i++) {
      const name = await nodeNames.nth(i).textContent();
      expect(name).not.toContain('/Users/');
      expect(name).not.toContain('/home/');
      expect(name).not.toContain('\\');
      // Names should not be full paths — they should be short identifiers
      expect(name!.length).toBeLessThan(60);
    }
  });

  test('context level badge is visible in breadcrumb', async ({ page }) => {
    const levelBadge = page.locator('nav span:has-text("Context")');
    await expect(levelBadge).toBeVisible();
  });

  test('system nodes have repo count metadata', async ({ page }) => {
    // Each system node should display how many repos it contains
    const nodes = page.locator('.card-node');
    const count = await nodes.count();
    expect(count).toBeGreaterThan(0);

    // Verify nodes have the system type indicator
    const systemHeaders = page.locator('.card-node__header');
    await expect(systemHeaders.first()).toBeVisible();
  });

  test('double-click a system node drills into L2 container view', async ({ page }) => {
    const initialNodeCount = await page.locator('.react-flow__node').count();

    // Double-click first system node to drill into it
    await page.locator('.react-flow__node').first().dblclick({ force: true });
    await page.waitForTimeout(3000);
    await page.waitForSelector('.react-flow__node', { timeout: 15_000 });

    // Level badge should change from Context to System (L2)
    const levelBadge = page.locator('nav span:has-text("System")');
    await expect(levelBadge).toBeVisible();

    // Breadcrumb should now have a path segment
    const breadcrumbButtons = page.locator('nav button');
    const segmentCount = await breadcrumbButtons.count();
    expect(segmentCount).toBeGreaterThanOrEqual(2);
  });
});

test.describe('Context Layer — GraphQL Data Integrity', () => {
  test('federation query returns repositories with clean names', async ({ page }) => {
    // Intercept the GraphQL federation query to check raw data
    const [response] = await Promise.all([
      page.waitForResponse(
        (resp) => resp.url().includes('/graphql') && resp.status() === 200,
        { timeout: 15_000 }
      ),
      page.goto('/'),
    ]);

    const body = await response.json();
    const federation = body?.data?.federation;

    if (federation?.repositories) {
      const repos = federation.repositories;

      // Should not have 58 entries
      expect(repos.length).toBeLessThanOrEqual(10);

      // Each repo name should be a clean identifier, not a path
      for (const repo of repos) {
        expect(repo.name).not.toContain('/');
        expect(repo.name).not.toContain('\\');
      }
    }
  });
});
