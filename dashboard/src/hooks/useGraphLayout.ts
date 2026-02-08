import { useMemo, useState, useEffect } from 'react';
import ELK, { type ElkNode, type ElkExtendedEdge, type LayoutOptions } from 'elkjs/lib/elk.bundled.js';
import { Position, MarkerType, type Node as RFNode, type Edge as RFEdge } from '@xyflow/react';

// ── Public domain types ──────────────────────────────────────────────────────

export interface AppNode {
  id: string;
  name: string;
  type: string;
  riskScore?: number;
  churnScore?: number;
  maintenanceCost?: number;
  consumerCount?: number;
  group?: string;
  status?: 'added' | 'removed' | 'modified' | 'unchanged';
  linesOfCode?: number;
  language?: string;
  atomCount?: number;
  dtoCount?: number;
  interfaceCount?: number;
  branch?: string;
}

export interface AppLink {
  source: string;
  target: string;
  type?: string;
  crossRepo?: boolean;
  isViolation?: boolean;
}

// ── Architectural Layer Detection ────────────────────────────────────────────
// Mirrors the backend's governance rules (Domain, Infrastructure, Web) but
// extended for layout purposes with 5 tiers:
//   0 = Presentation (Controllers, Web, API, UI)
//   1 = Application  (Services, Handlers, Orchestration)
//   2 = Domain       (Core models, Entities, ValueObjects)
//   3 = Infrastructure (Persistence, Repositories, DbContexts)
//   4 = External     (Tests, Migrations, Scripts)

export type ArchitecturalLayer = 'presentation' | 'application' | 'domain' | 'infrastructure' | 'external' | 'unknown';

const LAYER_ORDER: Record<ArchitecturalLayer, number> = {
  presentation:    0,
  application:     1,
  domain:          2,
  infrastructure:  3,
  external:        4,
  unknown:         2, // default to middle
};

export function inferArchitecturalLayer(name: string, type: string): ArchitecturalLayer {
  const n = name.toLowerCase();
  const t = type?.toLowerCase() ?? '';

  // C4 hierarchy types at the top level don't get layered
  if (['system', 'repository'].includes(t)) return 'unknown';

  // Namespace-level detection (L3 view) — match common .NET/Java patterns
  // Presentation layer
  if (/\b(controllers?|endpoints?|api|web|ui|views?|pages?|blazor|razor)\b/i.test(n))
    return 'presentation';

  // Application / Service layer
  if (/\b(services?|application|handlers?|commands?|queries|cqrs|orchestrat|mediator|usecases?)\b/i.test(n))
    return 'application';

  // Domain layer
  if (/\b(domain|core|models?|entities|valueobjects?|aggregates?|events?)\b/i.test(n))
    return 'domain';

  // Infrastructure layer
  if (/\b(infrastructure|persistence|repositories|data|migrations?|dbcontext|database|storage|adapters?)\b/i.test(n))
    return 'infrastructure';

  // External / Peripheral
  if (/\b(tests?|specs?|mocks?|fixtures?|scripts?|tools?|benchmarks?)\b/i.test(n))
    return 'external';

  // Atom-level (L4) fallback: use name suffix
  if (n.endsWith('controller') || n.endsWith('endpoint') || n.endsWith('api'))
    return 'presentation';
  if (n.endsWith('service') || n.endsWith('handler') || n.endsWith('manager'))
    return 'application';
  if (n.endsWith('repository') || n.endsWith('repo') || n.endsWith('store') || n.endsWith('dbcontext'))
    return 'infrastructure';

  return 'unknown';
}

// ── Topology Detection ───────────────────────────────────────────────────────
// Analyzes edge distribution to classify the graph's structural pattern.
// Detection priority: disconnected → hub-spoke → pipeline → layered → mesh

export type TopologyPattern = 'hub-spoke' | 'pipeline' | 'layered' | 'mesh' | 'disconnected';

export interface TopologyResult {
  pattern: TopologyPattern;
  hubNodeId?: string;        // for hub-spoke: the central node
  hubDegree?: number;        // total degree of the hub
  pipelineOrder?: string[];  // for pipeline: ordered node ids
  confidence: number;        // 0–1
}

// ── Server Layout Hint (Phase 6) ────────────────────────────────────────────

export interface LayoutHint {
  pattern: string;
  confidence: number;
  hubNodeId?: string | null;
  pipelineOrder?: string[] | null;
  layerAssignments?: { nodeId: string; layer: string }[];
}

/**
 * Convert a server-side LayoutHint into the frontend TopologyResult shape.
 * Falls back to client-side detectTopology() when hint is null/undefined.
 */
export function mapServerHint(hint: LayoutHint): TopologyResult {
  const validPatterns: TopologyPattern[] = ['hub-spoke', 'pipeline', 'layered', 'mesh', 'disconnected'];
  const pattern: TopologyPattern = validPatterns.includes(hint.pattern as TopologyPattern)
    ? (hint.pattern as TopologyPattern)
    : 'mesh';

  return {
    pattern,
    confidence: hint.confidence,
    hubNodeId: hint.hubNodeId ?? undefined,
    pipelineOrder: hint.pipelineOrder ?? undefined,
  };
}

/** Hub-spoke threshold: node with ≥ this fraction of total edges */
const HUB_THRESHOLD = 0.4;
/** Pipeline: longest path must cover ≥ this fraction of nodes */
const PIPELINE_COVERAGE = 0.6;
/** Disconnected: edge density below this fraction */
const DISCONNECTED_DENSITY = 0.3;
/** Radial layout fallback: switch to layered above this node count */
export const RADIAL_MAX_NODES = 25;

export function detectTopology(nodes: AppNode[], links: AppLink[]): TopologyResult {
  if (nodes.length === 0) {
    return { pattern: 'disconnected', confidence: 1 };
  }

  const nodeIds = new Set(nodes.map((n) => n.id));
  const validLinks = links.filter((l) => nodeIds.has(l.source) && nodeIds.has(l.target));
  const totalEdges = validLinks.length;

  // ── 1. Disconnected check ──────────────────────────────────────────────────
  if (nodes.length > 1 && totalEdges < nodes.length * DISCONNECTED_DENSITY) {
    return { pattern: 'disconnected', confidence: 1 };
  }

  // ── 2. Hub-spoke check (degree analysis) ───────────────────────────────────
  if (totalEdges > 0) {
    const degree = new Map<string, number>();
    for (const link of validLinks) {
      degree.set(link.source, (degree.get(link.source) ?? 0) + 1);
      degree.set(link.target, (degree.get(link.target) ?? 0) + 1);
    }

    let maxDegree = 0;
    let hubId = '';
    for (const [id, d] of degree) {
      if (d > maxDegree) {
        maxDegree = d;
        hubId = id;
      }
    }

    // Each edge counts twice in degree sum, so compare to totalEdges directly
    if (maxDegree >= totalEdges * HUB_THRESHOLD) {
      return {
        pattern: 'hub-spoke',
        hubNodeId: hubId,
        hubDegree: maxDegree,
        confidence: Math.min(1, maxDegree / totalEdges),
      };
    }
  }

  // ── 3. Pipeline check (longest path via BFS from sources) ──────────────────
  if (totalEdges > 0) {
    const adj = new Map<string, string[]>();
    const inDeg = new Map<string, number>();
    for (const id of nodeIds) {
      adj.set(id, []);
      inDeg.set(id, 0);
    }
    for (const link of validLinks) {
      adj.get(link.source)!.push(link.target);
      inDeg.set(link.target, (inDeg.get(link.target) ?? 0) + 1);
    }

    // Topological sort to find longest path
    const sources = [...nodeIds].filter((id) => (inDeg.get(id) ?? 0) === 0);
    const dist = new Map<string, number>();
    const parent = new Map<string, string | null>();
    for (const id of nodeIds) {
      dist.set(id, 0);
      parent.set(id, null);
    }

    // BFS topological order
    const queue = [...sources];
    const visited = new Set<string>();
    while (queue.length > 0) {
      const node = queue.shift()!;
      if (visited.has(node)) continue;
      visited.add(node);
      for (const next of adj.get(node) ?? []) {
        const newDist = (dist.get(node) ?? 0) + 1;
        if (newDist > (dist.get(next) ?? 0)) {
          dist.set(next, newDist);
          parent.set(next, node);
        }
        // Only enqueue if all predecessors visited (rough check)
        queue.push(next);
      }
    }

    // Find longest path length
    let maxLen = 0;
    let endNode = '';
    for (const [id, d] of dist) {
      if (d > maxLen) {
        maxLen = d;
        endNode = id;
      }
    }

    const pathLength = maxLen + 1; // +1 because dist counts edges
    if (pathLength >= nodes.length * PIPELINE_COVERAGE) {
      // Reconstruct path
      const path: string[] = [];
      let current: string | null = endNode;
      while (current) {
        path.unshift(current);
        current = parent.get(current) ?? null;
      }

      // Check branching factor: total edges / (nodes - 1) should be low
      const branchFactor = totalEdges / Math.max(1, nodes.length - 1);
      if (branchFactor <= 1.5) {
        return {
          pattern: 'pipeline',
          pipelineOrder: path,
          confidence: pathLength / nodes.length,
        };
      }
    }
  }

  // ── 4. Check for layered pattern (existing layer detection) ────────────────
  const layered = nodes.some((n) => inferArchitecturalLayer(n.name, n.type) !== 'unknown');
  if (layered) {
    return { pattern: 'layered', confidence: 0.6 };
  }

  // ── 5. Default: mesh ───────────────────────────────────────────────────────
  return { pattern: 'mesh', confidence: 0.5 };
}

// ── Card dimensions per C4 level ─────────────────────────────────────────────

function getCardDimensions(type: string): { width: number; height: number } {
  switch (type?.toLowerCase()) {
    case 'system':     return { width: 280, height: 90 };
    case 'repository': return { width: 260, height: 84 };
    case 'container':  return { width: 240, height: 80 };
    case 'namespace':  return { width: 220, height: 76 };
    default:           return { width: 200, height: 72 };
  }
}

// ── Level-aware ELK layout options ───────────────────────────────────────────

function getElkOptions(
  direction: 'LR' | 'TB',
  nodeCount: number,
  hasLayers: boolean,
  topology: TopologyResult
): LayoutOptions {
  // Hub-spoke: use radial algorithm (with fallback for large graphs)
  if (topology.pattern === 'hub-spoke' && nodeCount <= RADIAL_MAX_NODES) {
    return {
      'elk.algorithm': 'radial',
      'elk.radial.centerOnRoot': 'true',
      'elk.radial.compactor': 'WEDGE_COMPACTION',
      'elk.spacing.nodeNode': '80',
    };
  }

  // Pipeline: force left-to-right regardless of user direction
  const effectiveDirection = topology.pattern === 'pipeline' ? 'LR' : direction;

  const base: LayoutOptions = {
    'elk.algorithm': 'layered',
    'elk.direction': effectiveDirection === 'LR' ? 'RIGHT' : 'DOWN',
    'elk.edgeRouting': 'SPLINES',
    'elk.layered.crossingMinimization.strategy': 'LAYER_SWEEP',
    'elk.spacing.componentComponent': '50',
    'elk.layered.spacing.edgeEdgeBetweenLayers': '25',
  };

  // Enable partitioning when we detect architectural layers
  if (hasLayers) {
    base['elk.partitioning.activate'] = 'true';
  }

  if (nodeCount <= 5) {
    base['elk.spacing.nodeNode'] = '100';
    base['elk.layered.spacing.nodeNodeBetweenLayers'] = '140';
  } else {
    base['elk.spacing.nodeNode'] = '60';
    base['elk.layered.spacing.nodeNodeBetweenLayers'] = '100';
  }

  return base;
}

// ── Semantic Port Routing ────────────────────────────────────────────────────
// Maps link types to ELK port sides for directional edge routing.
// Left=inbound, Right=outbound, Top=inherits, Bottom=data.

export type PortSide = 'NORTH' | 'SOUTH' | 'EAST' | 'WEST';

export interface PortAssignment {
  sourcePort: PortSide;
  targetPort: PortSide;
}

/**
 * Classify a link type into source/target port sides.
 *
 * - inherits/implements → NORTH (hierarchy flows upward)
 * - calls/uses/dependsOn → EAST (source out) / WEST (target in)
 * - dbAccess/event/queue → SOUTH (data sinks downward)
 * - default → EAST/WEST
 */
export function classifyPort(linkType?: string): PortAssignment {
  const t = linkType?.toLowerCase() || '';

  // OOP hierarchy — top ports
  if (t === 'inherits' || t === 'implements') {
    return { sourcePort: 'NORTH', targetPort: 'SOUTH' };
  }

  // Data flows — bottom ports
  if (t === 'dbaccess' || t === 'event' || t === 'queue' || t === 'dataflow') {
    return { sourcePort: 'SOUTH', targetPort: 'NORTH' };
  }

  // Calls / dependencies — left-to-right
  return { sourcePort: 'EAST', targetPort: 'WEST' };
}

// ── Heat Channel Edge Styling ────────────────────────────────────────────────
// Unified 4-tier health model for edge coloring.
//   🟢 Green  (#22c55e) — clean call, no violation
//   🟡 Amber  (#f59e0b) — cross-module/cross-repo warning
//   🔴 Red    (#ef4444) — governance violation
//   🟣 Purple (#8b5cf6) — circular dependency

export type HeatLevel = 'clean' | 'warning' | 'violation' | 'circular';

const HEAT_COLORS: Record<HeatLevel, string> = {
  clean:     '#22c55e',
  warning:   '#f59e0b',
  violation: '#ef4444',
  circular:  '#8b5cf6',
};

/**
 * Determine the heat level for a link based on its properties.
 */
export function classifyHeat(link: AppLink): HeatLevel {
  if (link.isViolation) return 'violation';
  if (link.crossRepo) return 'warning';

  const t = link.type?.toLowerCase() || '';
  if (t === 'circular' || t === 'circulardependency') return 'circular';
  if (t === 'inherits' || t === 'implements') return 'circular'; // purple for OOP hierarchy

  return 'clean';
}

/**
 * Get React Flow edge style properties based on heat channel classification.
 */
export function getHeatStyle(link: AppLink): Partial<RFEdge> {
  const heat = classifyHeat(link);
  const color = HEAT_COLORS[heat];
  const t = link.type?.toLowerCase() || '';

  // Violation — animated, dashed, thick
  if (heat === 'violation') {
    return {
      type: 'default',
      animated: true,
      style: { stroke: color, strokeWidth: 2.5, strokeDasharray: '6 3' },
      markerEnd: { type: MarkerType.ArrowClosed, color, width: 12, height: 12 },
      label: link.type || 'violation',
      labelStyle: { fontSize: 9, fill: color },
    };
  }

  // Warning (cross-repo) — dashed
  if (heat === 'warning') {
    return {
      type: 'default',
      style: { stroke: color, strokeWidth: 2, strokeDasharray: '10 4' },
      markerEnd: { type: MarkerType.ArrowClosed, color, width: 12, height: 12 },
      label: 'cross-repo',
      labelStyle: { fontSize: 9, fill: color },
    };
  }

  // Circular / hierarchy — medium weight, purple
  if (heat === 'circular') {
    return {
      type: 'default',
      style: {
        stroke: color,
        strokeWidth: 2,
        strokeDasharray: t === 'implements' ? '8 4' : undefined,
      },
      markerEnd: { type: MarkerType.Arrow, color, width: 14, height: 14 },
      label: t || undefined,
      labelStyle: { fontSize: 9, fill: color },
    };
  }

  // Clean — slim green line
  return {
    type: 'default',
    style: { stroke: color, strokeWidth: 1.5 },
    markerEnd: { type: MarkerType.ArrowClosed, color, width: 10, height: 10 },
    label: t || undefined,
    labelStyle: t ? { fontSize: 9, fill: color } : undefined,
  };
}

// ── Singleton ELK instance ───────────────────────────────────────────────────

const elk = new ELK();

// ── Manual grid layout for dense graphs with few edges ───────────────────────

function gridLayout(
  nodes: AppNode[],
  links: AppLink[],
  direction: 'LR' | 'TB'
): { layoutNodes: RFNode[]; layoutEdges: RFEdge[] } {
  const isHorizontal = direction === 'LR';
  const gapX = 50;
  const gapY = 40;
  const nodeIdSet = new Set(nodes.map((n) => n.id));

  // Sort nodes by architectural layer so grid visually clusters by tier
  const sorted = [...nodes].sort((a, b) => {
    const la = LAYER_ORDER[inferArchitecturalLayer(a.name, a.type)];
    const lb = LAYER_ORDER[inferArchitecturalLayer(b.name, b.type)];
    return la - lb;
  });

  const cols = Math.ceil(Math.sqrt(sorted.length));

  const layoutNodes: RFNode[] = sorted.map((node, i) => {
    const { width, height } = getCardDimensions(node.type);
    const col = i % cols;
    const row = Math.floor(i / cols);

    return {
      id: node.id,
      type: 'cardNode',
      position: {
        x: col * (width + gapX),
        y: row * (height + gapY),
      },
      style: { width, height },
      data: {
        ...node,
        layer: inferArchitecturalLayer(node.name, node.type),
        cardWidth: width,
        cardHeight: height,
      },
      sourcePosition: isHorizontal ? Position.Right : Position.Bottom,
      targetPosition: isHorizontal ? Position.Left : Position.Top,
    };
  });

  const layoutEdges: RFEdge[] = links
    .filter((link) => nodeIdSet.has(link.source) && nodeIdSet.has(link.target))
    .map((link, i) => ({
      id: `e-${link.source}-${link.target}-${i}`,
      source: link.source,
      target: link.target,
      ...getHeatStyle(link),
      data: {
        linkType: link.type,
        isViolation: link.isViolation,
        crossRepo: link.crossRepo,
      },
    }));

  return { layoutNodes, layoutEdges };
}

// ── Core layout function ─────────────────────────────────────────────────────

/** Padding inside group nodes (px) */
const GROUP_PADDING_X = 20;
const GROUP_PADDING_Y = 36; // extra top for the label header
const GROUP_PADDING_BOTTOM = 16;

async function computeElkLayout(
  nodes: AppNode[],
  links: AppLink[],
  direction: 'LR' | 'TB',
  layoutHint?: LayoutHint | null
): Promise<{ layoutNodes: RFNode[]; layoutEdges: RFEdge[] }> {
  if (nodes.length === 0) {
    return { layoutNodes: [], layoutEdges: [] };
  }

  // Use server-side layout hint when available; otherwise detect client-side
  const topology = layoutHint
    ? mapServerHint(layoutHint)
    : detectTopology(nodes, links);

  // For disconnected graphs, use a simple grid layout
  // ELK's layered algorithm stacks disconnected nodes in one column
  if (topology.pattern === 'disconnected') {
    return gridLayout(nodes, links, direction);
  }

  // Detect which nodes have a known architectural layer
  const nodeLayers = new Map<string, ArchitecturalLayer>();
  for (const node of nodes) {
    const layer = inferArchitecturalLayer(node.name, node.type);
    nodeLayers.set(node.id, layer);
  }
  const hasLayers = [...nodeLayers.values()].some((l) => l !== 'unknown');

  const nodeIdSet = new Set(nodes.map((n) => n.id));
  const validEdges = links.filter((l) => nodeIdSet.has(l.source) && nodeIdSet.has(l.target));

  // ── Compound group detection ─────────────────────────────────────────
  // Nodes with a `group` property are nested inside a parent group node.
  // This creates the visual containment zones at L3 (project view).

  const groupMap = new Map<string, AppNode[]>(); // groupName → children
  const ungroupedNodes: AppNode[] = [];

  for (const node of nodes) {
    if (node.group) {
      if (!groupMap.has(node.group)) {
        groupMap.set(node.group, []);
      }
      groupMap.get(node.group)!.push(node);
    } else {
      ungroupedNodes.push(node);
    }
  }

  const hasGroups = groupMap.size > 0;

  // Build ELK graph with per-node layer partitions
  const isRadial = topology.pattern === 'hub-spoke' && nodes.length <= RADIAL_MAX_NODES;

  // ── Build ELK children (flat or compound) ────────────────────────────

  let elkChildren: ElkNode[];

  if (hasGroups && !isRadial) {
    // Compound layout: create parent nodes with nested children
    elkChildren = [];

    // Group nodes (compound parents)
    for (const [groupName, children] of groupMap) {
      const groupChildren: ElkNode[] = children.map((node) => {
        const { width, height } = getCardDimensions(node.type);
        return { id: node.id, width, height };
      });

      elkChildren.push({
        id: `group:${groupName}`,
        layoutOptions: {
          'elk.algorithm': 'layered',
          'elk.direction': direction === 'LR' ? 'RIGHT' : 'DOWN',
          'elk.padding': `[top=${GROUP_PADDING_Y},left=${GROUP_PADDING_X},bottom=${GROUP_PADDING_BOTTOM},right=${GROUP_PADDING_X}]`,
          'elk.spacing.nodeNode': '30',
        },
        children: groupChildren,
      });
    }

    // Ungrouped nodes at root level
    for (const node of ungroupedNodes) {
      const { width, height } = getCardDimensions(node.type);
      elkChildren.push({ id: node.id, width, height });
    }
  } else {
    // Flat layout (no groups or radial mode — existing behavior)
    elkChildren = nodes.map((node) => {
      const { width, height } = getCardDimensions(node.type);
      const layer = nodeLayers.get(node.id) ?? 'unknown';
      const partition = LAYER_ORDER[layer];

      const child: ElkNode = { id: node.id, width, height };

      if (!isRadial && hasLayers && layer !== 'unknown') {
        child.layoutOptions = {
          'elk.partitioning.partition': String(partition),
        };
      }

      return child;
    });
  }

  // For radial layout, restructure edges so the hub node is the root
  // ELK radial expects a tree rooted at the first child
  let elkEdges: ElkExtendedEdge[];
  let orderedChildren = elkChildren;

  if (isRadial && topology.hubNodeId) {
    // Move hub node to first position (ELK radial uses first child as root)
    const hubIdx = orderedChildren.findIndex((c) => c.id === topology.hubNodeId);
    if (hubIdx > 0) {
      const [hub] = orderedChildren.splice(hubIdx, 1);
      orderedChildren = [hub, ...orderedChildren];
    }

    // Create tree edges from hub to all other connected nodes
    const hubEdges: ElkExtendedEdge[] = [];
    const connected = new Set<string>();
    for (const link of validEdges) {
      const other = link.source === topology.hubNodeId ? link.target :
                    link.target === topology.hubNodeId ? link.source : null;
      if (other && !connected.has(other)) {
        connected.add(other);
        hubEdges.push({
          id: `e-hub-${other}`,
          sources: [topology.hubNodeId],
          targets: [other],
        });
      }
    }
    // Also connect non-hub nodes that have edges between them
    let extraIdx = 0;
    for (const link of validEdges) {
      if (link.source !== topology.hubNodeId && link.target !== topology.hubNodeId) {
        hubEdges.push({
          id: `e-extra-${extraIdx++}`,
          sources: [link.source],
          targets: [link.target],
        });
      }
    }
    elkEdges = hubEdges;
  } else if (hasGroups) {
    // For compound graphs, edges reference the leaf node IDs.
    // ELK resolves hierarchy crossing automatically.
    elkEdges = validEdges.map((l, i) => ({
      id: `e-${l.source}-${l.target}-${i}`,
      sources: [l.source],
      targets: [l.target],
    }));
  } else {
    elkEdges = validEdges.map((l, i) => ({
      id: `e-${l.source}-${l.target}-${i}`,
      sources: [l.source],
      targets: [l.target],
    }));
  }

  const elkGraph: ElkNode = {
    id: 'root',
    layoutOptions: getElkOptions(direction, nodes.length, hasLayers, topology),
    children: orderedChildren,
    edges: elkEdges,
  };

  const layoutResult = await elk.layout(elkGraph);

  const isHorizontal = direction === 'LR';

  // ── Map ELK positions back to React Flow nodes ─────────────────────

  const layoutNodes: RFNode[] = [];

  if (hasGroups && !isRadial) {
    // Extract group parents + nested children
    for (const elkChild of layoutResult.children ?? []) {
      if (elkChild.id.startsWith('group:')) {
        const groupName = elkChild.id.slice('group:'.length);
        const groupChildren = groupMap.get(groupName) ?? [];

        // Group parent node
        layoutNodes.push({
          id: elkChild.id,
          type: 'groupNode',
          position: {
            x: elkChild.x ?? 0,
            y: elkChild.y ?? 0,
          },
          data: {
            label: groupName.split('.').pop() || groupName,
            childCount: groupChildren.length,
          },
          style: {
            width: elkChild.width ?? 200,
            height: elkChild.height ?? 150,
          },
          // No drag — let children handle interaction
          draggable: false,
          selectable: false,
          sourcePosition: isHorizontal ? Position.Right : Position.Bottom,
          targetPosition: isHorizontal ? Position.Left : Position.Top,
        });

        // Child nodes (relative to parent)
        for (const elkGrandchild of elkChild.children ?? []) {
          const appNode = nodes.find((n) => n.id === elkGrandchild.id);
          if (!appNode) continue;
          const { width, height } = getCardDimensions(appNode.type);

          layoutNodes.push({
            id: appNode.id,
            type: 'cardNode',
            parentId: elkChild.id,
            extent: 'parent' as const,
            position: {
              x: elkGrandchild.x ?? 0,
              y: elkGrandchild.y ?? 0,
            },
            style: { width, height },
            data: {
              ...appNode,
              layer: nodeLayers.get(appNode.id) ?? 'unknown',
              topology: topology.pattern,
              isHub: topology.hubNodeId === appNode.id,
              pipelineIndex: topology.pipelineOrder?.indexOf(appNode.id) ?? -1,
              cardWidth: width,
              cardHeight: height,
            },
            sourcePosition: isHorizontal ? Position.Right : Position.Bottom,
            targetPosition: isHorizontal ? Position.Left : Position.Top,
          });
        }
      } else {
        // Ungrouped nodes at root level
        const appNode = nodes.find((n) => n.id === elkChild.id);
        if (!appNode) continue;
        const { width, height } = getCardDimensions(appNode.type);

        layoutNodes.push({
          id: appNode.id,
          type: 'cardNode',
          position: {
            x: elkChild.x ?? 0,
            y: elkChild.y ?? 0,
          },
          style: { width, height },
          data: {
            ...appNode,
            layer: nodeLayers.get(appNode.id) ?? 'unknown',
            topology: topology.pattern,
            isHub: topology.hubNodeId === appNode.id,
            pipelineIndex: topology.pipelineOrder?.indexOf(appNode.id) ?? -1,
            cardWidth: width,
            cardHeight: height,
          },
          sourcePosition: isHorizontal ? Position.Right : Position.Bottom,
          targetPosition: isHorizontal ? Position.Left : Position.Top,
        });
      }
    }
  } else {
    // Flat layout (existing behavior)
    for (const elkChild of layoutResult.children ?? []) {
      const appNode = nodes.find((n) => n.id === elkChild.id)!;
      const { width, height } = getCardDimensions(appNode.type);

      layoutNodes.push({
        id: appNode.id,
        type: 'cardNode',
        position: {
          x: elkChild.x ?? 0,
          y: elkChild.y ?? 0,
        },
        style: { width, height },
        data: {
          ...appNode,
          layer: nodeLayers.get(appNode.id) ?? 'unknown',
          topology: topology.pattern,
          isHub: topology.hubNodeId === appNode.id,
          pipelineIndex: topology.pipelineOrder?.indexOf(appNode.id) ?? -1,
          cardWidth: width,
          cardHeight: height,
        },
        sourcePosition: isHorizontal ? Position.Right : Position.Bottom,
        targetPosition: isHorizontal ? Position.Left : Position.Top,
      });
    }
  }

  // Build React Flow edges with semantic styling
  const layoutEdges: RFEdge[] = links
    .filter((link) => nodeIdSet.has(link.source) && nodeIdSet.has(link.target))
    .map((link, i) => ({
      id: `e-${link.source}-${link.target}-${i}`,
      source: link.source,
      target: link.target,
      ...getHeatStyle(link),
      data: {
        linkType: link.type,
        isViolation: link.isViolation,
        crossRepo: link.crossRepo,
      },
    }));

  return { layoutNodes, layoutEdges };
}

// ── React hook ───────────────────────────────────────────────────────────────

export function useGraphLayout(
  nodes: AppNode[],
  links: AppLink[],
  direction: 'LR' | 'TB' = 'LR'
): { layoutNodes: RFNode[]; layoutEdges: RFEdge[] } {
  const [result, setResult] = useState<{
    layoutNodes: RFNode[];
    layoutEdges: RFEdge[];
  }>({ layoutNodes: [], layoutEdges: [] });

  // Stable identity key so we only re-layout when data actually changes
  const dataKey = useMemo(
    () => JSON.stringify({ ids: nodes.map((n) => n.id), links: links.map((l) => `${l.source}-${l.target}`), direction }),
    [nodes, links, direction]
  );

  useEffect(() => {
    let cancelled = false;

    computeElkLayout(nodes, links, direction).then((layout) => {
      if (!cancelled) {
        setResult(layout);
      }
    });

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dataKey]);

  return result;
}
