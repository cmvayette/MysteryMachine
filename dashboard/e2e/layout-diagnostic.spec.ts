import { test, expect } from '@playwright/test';

/**
 * Diagnostic test to understand why L3 nodes stack.
 * Captures viewport transform, node transforms, and position data.
 */
test('L3 diagnostic: inspect viewport and node transforms', async ({ page }) => {
  await page.goto('/');
  await page.waitForSelector('.react-flow__node', { timeout: 15_000 });

  // L1 → L2
  await page.locator('.react-flow__node-cardNode').first().dblclick();
  await page.waitForTimeout(4000);
  await page.waitForSelector('.react-flow__node-cardNode', { timeout: 10_000 });

  // L2 info
  const l2Count = await page.locator('.react-flow__node-cardNode').count();
  console.log(`L2 node count: ${l2Count}`);

  // L2 → L3
  await page.locator('.react-flow__node-cardNode').first().dblclick();
  await page.waitForTimeout(4000);

  // Gather L3 diagnostic data
  const diagnostics = await page.evaluate(() => {
    const viewport = document.querySelector('.react-flow__viewport') as HTMLElement;
    const nodes = document.querySelectorAll('.react-flow__node');
    
    const nodeData = Array.from(nodes).slice(0, 10).map(n => {
      const el = n as HTMLElement;
      const rect = n.getBoundingClientRect();
      return {
        text: el.innerText?.split('\n')[0]?.substring(0, 30),
        inlineTransform: el.style.transform,
        styleWidth: el.style.width,
        styleHeight: el.style.height,
        rectLeft: Math.round(rect.left),
        rectTop: Math.round(rect.top),
        rectWidth: Math.round(rect.width),
        rectHeight: Math.round(rect.height),
      };
    });
    
    return {
      viewportTransform: viewport?.style.transform || 'NOT FOUND',
      viewportComputedTransform: viewport ? window.getComputedStyle(viewport).transform : 'N/A',
      totalNodes: nodes.length,
      nodeData,
      // Check the parent container of nodes
      nodeParentTagName: nodes[0]?.parentElement?.tagName,
      nodeParentClassName: nodes[0]?.parentElement?.className,
    };
  });

  console.log('=== L3 Diagnostic Data ===');
  console.log('Viewport transform:', diagnostics.viewportTransform);
  console.log('Viewport computed:', diagnostics.viewportComputedTransform);
  console.log('Total nodes:', diagnostics.totalNodes);
  console.log('Node parent:', diagnostics.nodeParentTagName, diagnostics.nodeParentClassName);
  console.log('First 10 nodes:');
  for (const n of diagnostics.nodeData) {
    console.log(`  "${n.text}" | transform: ${n.inlineTransform} | size: ${n.styleWidth}x${n.styleHeight} | rect: (${n.rectLeft},${n.rectTop}) ${n.rectWidth}x${n.rectHeight}`);
  }

  // Basic assertions
  expect(diagnostics.totalNodes).toBeGreaterThan(1);

  // Check if nodes have DIFFERENT inline transforms
  const transforms = new Set(diagnostics.nodeData.map(n => n.inlineTransform));
  console.log(`Unique transforms among first 10: ${transforms.size}`);
  
  // Check if nodes have different bounding rects
  const positions = new Set(diagnostics.nodeData.map(n => `${n.rectLeft},${n.rectTop}`));
  console.log(`Unique screen positions among first 10: ${positions.size}`);

  // The key question: do inline transforms differ but screen positions don't?
  if (transforms.size > 1 && positions.size <= 1) {
    console.log('DIAGNOSIS: Nodes have different transforms but same screen position.');
    console.log('This means the viewport zoom is too extreme, collapsing all positions.');
  } else if (transforms.size <= 1) {
    console.log('DIAGNOSIS: Nodes all have the SAME transform — layout is not distributing positions.');
  }
});
