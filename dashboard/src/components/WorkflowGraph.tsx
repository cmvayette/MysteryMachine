import { useMemo, useCallback, useEffect, useRef } from 'react';
import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  BackgroundVariant,
  ReactFlowProvider,
  useReactFlow,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';

import { CardNode } from './CardNode';
import { GroupNode } from './GroupNode';
import { EdgeLegend } from './EdgeLegend';
import { useGraphLayout, type AppNode, type AppLink } from '../hooks/useGraphLayout';
import { useCanvasEvents } from '../hooks/useCanvasEvents';
import { useNavigationStore } from '../store/navigationStore';

// Re-export types for App.tsx
export type { AppNode, AppLink };

// Register custom node types
const nodeTypes = {
  cardNode: CardNode,
  groupNode: GroupNode,
};

interface WorkflowGraphProps {
  nodes: AppNode[];
  links: AppLink[];
  showLinks?: boolean;
  blastRadiusAtoms?: Set<string>;
  blastRadiusDepths?: Map<string, number>;
  width?: number;
  height?: number;
  diffMode?: boolean;
  governanceMode?: boolean;
  activeHeatmap?: string | null;
}

// Determine layout direction based on dominant node type
function getLayoutDirection(nodes: AppNode[]): 'LR' | 'TB' {
  if (nodes.length === 0) return 'LR';
  const firstType = nodes[0]?.type?.toLowerCase();
  // Higher C4 levels flow left-to-right, code level flows top-to-bottom
  if (firstType === 'system' || firstType === 'repository' || firstType === 'container') {
    return 'LR';
  }
  return 'TB';
}

// Apply heatmap colors to nodes
function applyHeatmap(nodes: AppNode[], heatmap: string | null): AppNode[] {
  if (!heatmap) return nodes;
  return nodes.map((n) => ({ ...n, _heatmap: heatmap }));
}

function WorkflowGraphInner({
  nodes,
  links,
  showLinks = true,
  blastRadiusAtoms,
  blastRadiusDepths,
  // diffMode and governanceMode reserved for future visual overlays
  activeHeatmap = null,
}: WorkflowGraphProps) {

  // Use navigation state for the ReactFlow key — forces remount + fitView on level change
  const level = useNavigationStore((s) => s.level);
  const path = useNavigationStore((s) => s.path);
  const flowKey = `${level}/${path.join('/')}`;

  const direction = useMemo(() => getLayoutDirection(nodes), [nodes]);
  const { fitView } = useReactFlow();
  const prevNodeCountRef = useRef(0);

  // Enrich nodes with blast radius / governance data
  const enrichedNodes = useMemo(() => {
    let result = applyHeatmap(nodes, activeHeatmap);

    if (blastRadiusAtoms && blastRadiusAtoms.size > 0) {
      result = result.map((n) => ({
        ...n,
        _blastDepth: blastRadiusDepths?.get(n.id),
        _inBlastRadius: blastRadiusAtoms.has(n.id),
      }));
    }

    return result;
  }, [nodes, activeHeatmap, blastRadiusAtoms, blastRadiusDepths]);

  const { layoutNodes, layoutEdges } = useGraphLayout(
    enrichedNodes,
    showLinks ? links : [],
    direction
  );

  // Programmatic fitView after async layout completes
  // The ELK layout is async, so layoutNodes arrive after React Flow mounts.
  // Without this, fitView fires on mount with empty nodes and never re-fits.
  useEffect(() => {
    if (layoutNodes.length > 0 && layoutNodes.length !== prevNodeCountRef.current) {
      prevNodeCountRef.current = layoutNodes.length;
      // Use double rAF to ensure React Flow has rendered the nodes into the DOM
      requestAnimationFrame(() => {
        requestAnimationFrame(() => {
          fitView({ padding: 0.3, maxZoom: 1.5, duration: 200 });
        });
      });
    }
  }, [layoutNodes, fitView]);

  // Event handlers from extracted hook — selection goes to Zustand store directly
  const { handleNodeClick, handleNodeDoubleClick, handlePaneClick } = useCanvasEvents(nodes);

  // Minimap node color
  const minimapNodeColor = useCallback((node: { data?: Record<string, unknown> }) => {
    const type = (node.data?.type as string) || '';
    switch (type.toLowerCase()) {
      case 'system':     return '#3b82f6';
      case 'repository': return '#f59e0b';
      case 'service':    return '#06b6d4';
      case 'namespace':  return '#8b5cf6';
      case 'interface':  return '#94a3b8';
      case 'class':      return '#7aa3a3';
      default:           return '#4a5568';
    }
  }, []);

  return (
    <div className="workflow-graph" style={{ width: '100%', height: '100%' }}>
      <ReactFlow
        key={flowKey}
        nodes={layoutNodes}
        edges={layoutEdges}
        nodeTypes={nodeTypes}
        onNodeClick={handleNodeClick}
        onNodeDoubleClick={handleNodeDoubleClick}
        onPaneClick={handlePaneClick}
        fitView
        fitViewOptions={{ padding: 0.3, maxZoom: 1.5 }}
        minZoom={0.05}
        maxZoom={4}
        proOptions={{ hideAttribution: true }}
        colorMode="dark"
        nodesDraggable={true}
        nodesConnectable={false}
        elementsSelectable={true}
        defaultEdgeOptions={{
          type: 'default',
        }}
      >
        <Background
          variant={BackgroundVariant.Dots}
          gap={30}
          size={1}
          color="#2a3038"
        />
        <Controls
          position="bottom-left"
          showInteractive={false}
          className="workflow-graph__controls"
        />
        <MiniMap
          position="bottom-right"
          nodeColor={minimapNodeColor}
          maskColor="rgba(13, 14, 18, 0.7)"
          className="workflow-graph__minimap"
          pannable
          zoomable
        />
      </ReactFlow>
      <EdgeLegend />
    </div>
  );
}

// Wrap with ReactFlowProvider for hook access
export function WorkflowGraph(props: WorkflowGraphProps) {
  return (
    <ReactFlowProvider>
      <WorkflowGraphInner {...props} />
    </ReactFlowProvider>
  );
}
