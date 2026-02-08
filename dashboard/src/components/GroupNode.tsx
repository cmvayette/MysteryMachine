import { memo } from 'react';
import type { NodeProps } from '@xyflow/react';

/**
 * GroupNode — a container "zone" node for compound layouts.
 *
 * Renders a frosted-glass background with a label header.
 * Used at L3 (project level) to visually nest namespace nodes
 * inside their parent container.
 *
 * React Flow renders group nodes behind their children
 * when children have `parentId` pointing to this node.
 */

interface GroupNodeData {
  label: string;
  childCount?: number;
  [key: string]: unknown;
}

function GroupNodeComponent({ data }: NodeProps) {
  const { label, childCount } = data as GroupNodeData;

  return (
    <div className="group-node">
      <div className="group-node__header">
        <span className="group-node__label">{label}</span>
        {childCount != null && childCount > 0 && (
          <span className="group-node__count">{childCount}</span>
        )}
      </div>
    </div>
  );
}

export const GroupNode = memo(GroupNodeComponent);
