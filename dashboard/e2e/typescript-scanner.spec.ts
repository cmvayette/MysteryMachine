import { test, expect } from '@playwright/test';

/**
 * TypeScript Scanner — Dashboard Verification (Phase 4)
 *
 * Verifies that the digital_backbone snapshot (produced by the TypeScript scanner)
 * is correctly rendered in the DSL dashboard across all C4 levels.
 *
 * Prerequisites: Docker stack running, digital_backbone snapshot published to API.
 */

const API_BASE = 'http://localhost:8085';

test.describe('TypeScript Scanner — Federation View (L1)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForSelector('.react-flow__node', { timeout: 15_000 });
  });

  test('digital_backbone appears as a repository node at L1', async ({ page }) => {
    const nodeNames = page.locator('.card-node__name');
    const count = await nodeNames.count();
    expect(count).toBeGreaterThan(0);

    const names: string[] = [];
    for (let i = 0; i < count; i++) {
      names.push((await nodeNames.nth(i).textContent()) ?? '');
    }

    expect(names.some((n) => n.includes('digital_backbone'))).toBeTruthy();
  });

  test('GraphQL federation query includes digital_backbone', async ({ page }) => {
    const [response] = await Promise.all([
      page.waitForResponse(
        (resp) => resp.url().includes('/graphql') && resp.status() === 200,
        { timeout: 15_000 }
      ),
      page.goto('/'),
    ]);

    const body = await response.json();
    const repos = body?.data?.federation?.repositories ?? [];

    const dbRepo = repos.find((r: { name: string }) =>
      r.name.includes('digital_backbone')
    );
    expect(dbRepo).toBeDefined();
  });
});

test.describe('TypeScript Scanner — Container View (L2)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForSelector('.react-flow__node', { timeout: 15_000 });

    // Find and double-click the digital_backbone node to drill into L2
    const nodes = page.locator('.react-flow__node');
    const count = await nodes.count();
    for (let i = 0; i < count; i++) {
      const text = await nodes.nth(i).textContent();
      if (text?.includes('digital_backbone')) {
        await nodes.nth(i).dblclick({ force: true });
        break;
      }
    }
    await page.waitForTimeout(3000);
    await page.waitForSelector('.react-flow__node', { timeout: 15_000 });
  });

  test('L2 shows multiple namespace containers', async ({ page }) => {
    const nodes = page.locator('.react-flow__node');
    const nodeCount = await nodes.count();

    // digital_backbone has many namespaces — we should see multiple container nodes
    expect(nodeCount).toBeGreaterThanOrEqual(3);
  });

  test('namespace names are clean identifiers', async ({ page }) => {
    const nodeNames = page.locator('.card-node__name');
    const count = await nodeNames.count();
    expect(count).toBeGreaterThan(0);

    for (let i = 0; i < count; i++) {
      const name = await nodeNames.nth(i).textContent();
      expect(name).not.toContain('/Users/');
      expect(name?.length).toBeLessThan(100);
    }
  });
});

test.describe('TypeScript Scanner — GraphQL Data Integrity', () => {
  test('federation stats reflect TypeScript atoms', async ({ page }) => {
    const response = await page.request.post(`${API_BASE}/graphql`, {
      data: {
        query: `{
          federation {
            stats {
              totalCodeAtoms
              totalLinks
              totalRepos
            }
            repositories {
              name
              atomCount
              namespaces
            }
          }
        }`,
      },
    });

    const body = await response.json();
    const federation = body?.data?.federation;

    expect(federation).toBeDefined();
    expect(federation.stats.totalCodeAtoms).toBeGreaterThan(4000);
    expect(federation.stats.totalLinks).toBeGreaterThan(1000);
    expect(federation.stats.totalRepos).toBeGreaterThanOrEqual(1);

    // Find digital_backbone repo
    const dbRepo = federation.repositories.find(
      (r: { name: string }) => r.name === 'digital_backbone'
    );
    expect(dbRepo).toBeDefined();
    expect(dbRepo.atomCount).toBeGreaterThan(4000);
    expect(dbRepo.namespaces.length).toBeGreaterThanOrEqual(10);
  });

  test('namespace query returns TypeScript atoms with GQL. prefix', async ({ page }) => {
    // First get a namespace from digital_backbone that should have GQL atoms
    // som-tier0's graphql schema directory is where Pothos resolvers live
    const fedResponse = await page.request.post(`${API_BASE}/graphql`, {
      data: {
        query: `{
          federation {
            repositories {
              name
              namespaces
            }
          }
        }`,
      },
    });

    const fedBody = await fedResponse.json();
    const dbRepo = fedBody?.data?.federation?.repositories?.find(
      (r: { name: string }) => r.name === 'digital_backbone'
    );
    expect(dbRepo).toBeDefined();

    // Find a namespace that contains graphql atoms
    const gqlNamespace = dbRepo.namespaces.find(
      (ns: string) => ns.includes('graphql') || ns.includes('schema')
    );

    if (gqlNamespace) {
      // Query atoms in that namespace
      const nsResponse = await page.request.post(`${API_BASE}/graphql`, {
        data: {
          query: `{
            namespace(repoId: "digital_backbone", path: "${gqlNamespace}") {
              path
              atoms {
                name
                type
              }
            }
          }`,
        },
      });

      const nsBody = await nsResponse.json();
      const ns = nsBody?.data?.namespace;
      expect(ns).toBeDefined();

      const gqlAtoms = ns.atoms.filter((a: { name: string }) =>
        a.name.startsWith('GQL.')
      );
      expect(gqlAtoms.length).toBeGreaterThan(0);
    }
  });
});
