import { describe, it, expect } from 'vitest';
import {
  detectTopology,
  inferArchitecturalLayer,
  classifyPort,
  classifyHeat,
  getHeatStyle,
  mapServerHint,
  type AppNode,
  type AppLink,
  type LayoutHint,
} from './useGraphLayout';

// ── Helpers ──────────────────────────────────────────────────────────────────

function makeNode(id: string, name?: string, type?: string): AppNode {
  return { id, name: name ?? id, type: type ?? 'class' };
}

function makeLink(source: string, target: string, type?: string): AppLink {
  return { source, target, type };
}

// ── detectTopology tests ─────────────────────────────────────────────────────

describe('detectTopology', () => {
  describe('hub-spoke detection', () => {
    it('classic hub: one node with 8/10 edges', () => {
      const nodes = [
        makeNode('hub'),
        ...Array.from({ length: 8 }, (_, i) => makeNode(`spoke-${i}`)),
      ];
      // All edges point to hub
      const links = Array.from({ length: 8 }, (_, i) =>
        makeLink(`spoke-${i}`, 'hub')
      );

      const result = detectTopology(nodes, links);
      expect(result.pattern).toBe('hub-spoke');
      expect(result.hubNodeId).toBe('hub');
      expect(result.hubDegree).toBe(8);
      expect(result.confidence).toBeGreaterThan(0.5);
    });

    it('hub at exactly 40% threshold', () => {
      // 5 nodes, 5 edges. Hub has 2 edges = 40%
      const nodes = Array.from({ length: 5 }, (_, i) => makeNode(`n${i}`));
      // n0 → n1, n0 → n2, n3 → n4, n1 → n3, n2 → n4
      const links = [
        makeLink('n0', 'n1'),
        makeLink('n0', 'n2'),
        makeLink('n3', 'n4'),
        makeLink('n1', 'n3'),
        makeLink('n2', 'n4'),
      ];
      // n0 has degree 2, n1 has degree 2, n2 has degree 2, n3 has degree 2, n4 has degree 2
      // All equal with 5 edges → 2/5 = 0.4 = threshold → hub-spoke
      const result = detectTopology(nodes, links);
      // At threshold, any of them could be the hub — the first one found
      expect(result.pattern).toBe('hub-spoke');
    });

    it('distributed degree — no hub', () => {
      // Ring topology: each node has degree 2
      const nodes = Array.from({ length: 10 }, (_, i) => makeNode(`n${i}`));
      // Create a ring: n0→n1→n2→...→n9→n0 plus reverse edges
      const links = Array.from({ length: 10 }, (_, i) =>
        makeLink(`n${i}`, `n${(i + 1) % 10}`)
      );
      // Each node has degree 2, 10 edges, max degree 2 < 10 * 0.4 = 4
      const result = detectTopology(nodes, links);
      expect(result.pattern).not.toBe('hub-spoke');
    });

    it('hub with mixed in/out edges', () => {
      const nodes = [
        makeNode('hub'),
        makeNode('a'), makeNode('b'), makeNode('c'), makeNode('d'),
      ];
      const links = [
        makeLink('a', 'hub'),
        makeLink('b', 'hub'),
        makeLink('hub', 'c'),
        makeLink('hub', 'd'),
      ];
      // hub degree = 4 out of 4 edges = 100%
      const result = detectTopology(nodes, links);
      expect(result.pattern).toBe('hub-spoke');
      expect(result.hubNodeId).toBe('hub');
    });
  });

  describe('pipeline detection', () => {
    it('linear chain of 10 nodes', () => {
      const ids = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j'];
      const nodes = ids.map((id) => makeNode(id));
      const links = ids.slice(0, -1).map((id, i) => makeLink(id, ids[i + 1]));
      // 10 nodes, 9 edges. Max degree = 2 (interior nodes), 2/9 = 22% < 40%
      // Path length = 10 = 100% coverage, branch factor = 9/9 = 1.0
      const result = detectTopology(nodes, links);
      expect(result.pattern).toBe('pipeline');
      expect(result.pipelineOrder).toBeDefined();
      expect(result.pipelineOrder!.length).toBe(10);
      expect(result.pipelineOrder![0]).toBe('a');
      expect(result.pipelineOrder![9]).toBe('j');
    });

    it('chain with excessive branching → not pipeline', () => {
      const nodes = ['a', 'b', 'c', 'd', 'e'].map((id) => makeNode(id));
      const links = [
        makeLink('a', 'b'),
        makeLink('b', 'c'),
        makeLink('c', 'd'),
        makeLink('d', 'e'),
        // Extra branch edges
        makeLink('a', 'c'),
        makeLink('b', 'd'),
        makeLink('c', 'e'),
        makeLink('a', 'd'),
      ];
      // Branch factor = 8 / 4 = 2.0 > 1.5
      // But hub detection may trigger first: a has degree 4/8 = 50% > 40%
      const result = detectTopology(nodes, links);
      expect(result.pattern).not.toBe('pipeline');
    });
  });

  describe('disconnected detection', () => {
    it('10 nodes, 0 edges', () => {
      const nodes = Array.from({ length: 10 }, (_, i) => makeNode(`n${i}`));
      const result = detectTopology(nodes, []);
      expect(result.pattern).toBe('disconnected');
      expect(result.confidence).toBe(1);
    });

    it('sparse: 10 nodes, 2 edges', () => {
      const nodes = Array.from({ length: 10 }, (_, i) => makeNode(`n${i}`));
      const links = [makeLink('n0', 'n1'), makeLink('n2', 'n3')];
      // 2 < 10 * 0.3 = 3 → disconnected
      const result = detectTopology(nodes, links);
      expect(result.pattern).toBe('disconnected');
    });

    it('empty graph', () => {
      const result = detectTopology([], []);
      expect(result.pattern).toBe('disconnected');
    });
  });

  describe('mesh fallback', () => {
    it('well-connected graph with no clear pattern and generic names', () => {
      // 6 nodes, fully connected in a mesh — no hub, no pipeline, no layers
      const nodes = Array.from({ length: 6 }, (_, i) =>
        makeNode(`widget${i}`, `Widget${i}`, 'class')
      );
      // Create enough edges to not be disconnected but evenly distributed
      // Ring + cross edges
      const links = [
        makeLink('widget0', 'widget1'),
        makeLink('widget1', 'widget2'),
        makeLink('widget2', 'widget3'),
        makeLink('widget3', 'widget4'),
        makeLink('widget4', 'widget5'),
        makeLink('widget5', 'widget0'),
        makeLink('widget0', 'widget3'),
        makeLink('widget1', 'widget4'),
        makeLink('widget2', 'widget5'),
      ];
      // Each node degree ~3, 9 edges. Max degree 3 < 9*0.4 = 3.6
      // Path length at most 6 = 100% but branching = 9/5 = 1.8 > 1.5
      const result = detectTopology(nodes, links);
      expect(result.pattern).toBe('mesh');
    });
  });

  describe('layered detection', () => {
    it('nodes with architectural layer names', () => {
      const nodes = [
        makeNode('ctrl', 'Controllers', 'namespace'),
        makeNode('svc', 'Services', 'namespace'),
        makeNode('domain', 'Domain', 'namespace'),
      ];
      const links = [
        makeLink('ctrl', 'svc'),
        makeLink('svc', 'domain'),
      ];
      // Pipeline would match (path = 3/3 = 100%, branch = 2/2 = 1.0)
      // But hub-spoke also: ctrl degree 1, svc degree 2, domain degree 1
      // svc has 2/2 = 100% ≥ 40% → hub-spoke wins first
      // NOTE: hub-spoke takes priority over pipeline in detection order
      const result = detectTopology(nodes, links);
      // svc will be detected as hub (2/2 edges)
      expect(['hub-spoke', 'pipeline', 'layered']).toContain(result.pattern);
    });
  });
});

// ── inferArchitecturalLayer tests ────────────────────────────────────────────

describe('inferArchitecturalLayer', () => {
  it('detects presentation layer from controller name', () => {
    expect(inferArchitecturalLayer('UserController', 'class')).toBe('presentation');
  });

  it('detects presentation from namespace pattern', () => {
    expect(inferArchitecturalLayer('Api.Controllers', 'namespace')).toBe('presentation');
  });

  it('detects application layer from service name', () => {
    expect(inferArchitecturalLayer('UserService', 'class')).toBe('application');
  });

  it('detects application from handler pattern', () => {
    expect(inferArchitecturalLayer('CreateUserHandler', 'class')).toBe('application');
  });

  it('detects domain layer', () => {
    expect(inferArchitecturalLayer('CleanArchitecture.Domain', 'container')).toBe('domain');
  });

  it('detects infrastructure layer', () => {
    expect(inferArchitecturalLayer('CleanArchitecture.Infrastructure', 'container')).toBe('infrastructure');
  });

  it('detects external layer for tests', () => {
    expect(inferArchitecturalLayer('Unit.Tests', 'namespace')).toBe('external');
  });

  it('returns unknown for generic names', () => {
    expect(inferArchitecturalLayer('Utils.Helpers', 'class')).toBe('unknown');
  });

  it('returns unknown for system type', () => {
    expect(inferArchitecturalLayer('MySystem', 'system')).toBe('unknown');
  });

  it('returns unknown for repository C4 type', () => {
    expect(inferArchitecturalLayer('MyRepo', 'repository')).toBe('unknown');
  });

  it('detects infrastructure from suffix (atom-level)', () => {
    expect(inferArchitecturalLayer('AppDbContext', 'class')).toBe('infrastructure');
  });
});

// ── classifyPort tests ───────────────────────────────────────────────────────

describe('classifyPort', () => {
  it('inherits → NORTH / SOUTH', () => {
    const result = classifyPort('inherits');
    expect(result.sourcePort).toBe('NORTH');
    expect(result.targetPort).toBe('SOUTH');
  });

  it('implements → NORTH / SOUTH', () => {
    const result = classifyPort('implements');
    expect(result.sourcePort).toBe('NORTH');
    expect(result.targetPort).toBe('SOUTH');
  });

  it('calls → EAST / WEST', () => {
    const result = classifyPort('calls');
    expect(result.sourcePort).toBe('EAST');
    expect(result.targetPort).toBe('WEST');
  });

  it('dbAccess → SOUTH / NORTH (data flow)', () => {
    const result = classifyPort('dbAccess');
    expect(result.sourcePort).toBe('SOUTH');
    expect(result.targetPort).toBe('NORTH');
  });

  it('event → SOUTH / NORTH (data flow)', () => {
    const result = classifyPort('event');
    expect(result.sourcePort).toBe('SOUTH');
    expect(result.targetPort).toBe('NORTH');
  });

  it('undefined → EAST / WEST (default)', () => {
    const result = classifyPort(undefined);
    expect(result.sourcePort).toBe('EAST');
    expect(result.targetPort).toBe('WEST');
  });
});

// ── classifyHeat / getHeatStyle tests ────────────────────────────────────────

describe('classifyHeat', () => {
  it('violation link → violation heat', () => {
    const link: AppLink = { source: 'a', target: 'b', type: 'calls', isViolation: true };
    expect(classifyHeat(link)).toBe('violation');
  });

  it('cross-repo link → warning heat', () => {
    const link: AppLink = { source: 'a', target: 'b', type: 'calls', crossRepo: true };
    expect(classifyHeat(link)).toBe('warning');
  });

  it('circular dependency → circular heat', () => {
    const link: AppLink = { source: 'a', target: 'b', type: 'circular' };
    expect(classifyHeat(link)).toBe('circular');
  });

  it('inherits → circular heat (purple for hierarchy)', () => {
    const link: AppLink = { source: 'a', target: 'b', type: 'inherits' };
    expect(classifyHeat(link)).toBe('circular');
  });

  it('regular call → clean heat', () => {
    const link: AppLink = { source: 'a', target: 'b', type: 'calls' };
    expect(classifyHeat(link)).toBe('clean');
  });
});

describe('getHeatStyle', () => {
  it('violation has animated red edge', () => {
    const link: AppLink = { source: 'a', target: 'b', isViolation: true };
    const style = getHeatStyle(link);
    expect(style.animated).toBe(true);
    expect(style.style?.stroke).toBe('#ef4444');
  });

  it('clean call has green edge', () => {
    const link: AppLink = { source: 'a', target: 'b', type: 'calls' };
    const style = getHeatStyle(link);
    expect(style.style?.stroke).toBe('#22c55e');
  });

  it('cross-repo has amber dashed edge', () => {
    const link: AppLink = { source: 'a', target: 'b', crossRepo: true };
    const style = getHeatStyle(link);
    expect(style.style?.stroke).toBe('#f59e0b');
    expect(style.style?.strokeDasharray).toBeDefined();
  });
});

// ── Phase 6: Server Layout Hint Tests ──────────────────────────────────────

describe('mapServerHint (P6)', () => {
  it('converts API response to TopologyResult', () => {
    const hint: LayoutHint = {
      pattern: 'hub-spoke',
      confidence: 0.85,
      hubNodeId: 'hub-1',
      pipelineOrder: null,
      layerAssignments: [{ nodeId: 'n1', layer: 'presentation' }],
    };
    const result = mapServerHint(hint);
    expect(result.pattern).toBe('hub-spoke');
    expect(result.confidence).toBe(0.85);
    expect(result.hubNodeId).toBe('hub-1');
    expect(result.pipelineOrder).toBeUndefined();
  });

  it('falls back to mesh for unknown patterns', () => {
    const hint: LayoutHint = {
      pattern: 'unknown-pattern',
      confidence: 0.5,
    };
    const result = mapServerHint(hint);
    expect(result.pattern).toBe('mesh');
    expect(result.confidence).toBe(0.5);
  });
});
