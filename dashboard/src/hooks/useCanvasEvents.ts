import { useCallback, useRef } from 'react';
import { type NodeMouseHandler } from '@xyflow/react';
import { useNavigationStore } from '../store/navigationStore';
import type { AppNode } from './useGraphLayout';

/**
 * Extracted canvas event handlers.
 * Selection goes directly into Zustand store — no node re-creation needed.
 * Drill-down fades out siblings, then transitions levels.
 * Post-layout fitView is handled in WorkflowGraph.tsx.
 */
export function useCanvasEvents(nodes: AppNode[]) {
  const selectAtom = useNavigationStore((s) => s.selectAtom);
  const drillDown = useNavigationStore((s) => s.drillDown);
  const level = useNavigationStore((s) => s.level);

  // Guard against double-fire during animation
  const isAnimating = useRef(false);

  const handleNodeClick: NodeMouseHandler = useCallback(
    (_event, node) => {
      selectAtom(node.id);
    },
    [selectAtom]
  );

  const handleNodeDoubleClick: NodeMouseHandler = useCallback(
    (_event, node) => {
      if (level === 'code' || isAnimating.current) return;

      const appNode = nodes.find((n) => n.id === node.id);
      if (!appNode) return;

      isAnimating.current = true;

      // Fade out sibling nodes via CSS class
      const container = document.querySelector('.react-flow__viewport');
      const allNodeEls = container?.querySelectorAll('.react-flow__node');
      allNodeEls?.forEach((el) => {
        if (!el.getAttribute('data-id')?.includes(node.id)) {
          el.classList.add('node-fade-out');
        }
      });

      // After fade completes, trigger the drill-down (which swaps data)
      setTimeout(() => {
        allNodeEls?.forEach((el) => {
          el.classList.remove('node-fade-out');
        });
        drillDown(node.id);
        isAnimating.current = false;
      }, 350);
    },
    [nodes, level, drillDown]
  );

  const handlePaneClick = useCallback(() => {
    selectAtom(null);
  }, [selectAtom]);

  return { handleNodeClick, handleNodeDoubleClick, handlePaneClick };
}
