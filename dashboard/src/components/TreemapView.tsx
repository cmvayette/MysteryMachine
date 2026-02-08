import { useMemo, useState, useCallback } from 'react';
import { squarify, type TreemapItem, type TreemapRect } from '../hooks/useTreemapLayout';
import { useNavigationStore } from '../store/navigationStore';

interface TreemapNode {
  id: string;
  name: string;
  consumerCount?: number;
  riskScore?: number;
}

interface TreemapViewProps {
  nodes: TreemapNode[];
  width?: number;
  height?: number;
}

const PADDING = 4;
const MIN_LABEL_WIDTH = 60;
const MIN_LABEL_HEIGHT = 30;

export function TreemapView({ nodes, width = 900, height = 600 }: TreemapViewProps) {
  const drillDown = useNavigationStore((s) => s.drillDown);
  const [hoveredId, setHoveredId] = useState<string | null>(null);

  // Convert AppNodes to TreemapItems
  const items: TreemapItem[] = useMemo(
    () =>
      nodes
        .filter((n) => (n.consumerCount ?? 0) > 0)
        .map((n) => ({
          id: n.id,
          name: n.name,
          value: n.consumerCount ?? 1,
        })),
    [nodes]
  );

  // Compute layout
  const rects: TreemapRect[] = useMemo(
    () => squarify(items, { x: PADDING, y: PADDING, width: width - PADDING * 2, height: height - PADDING * 2 }),
    [items, width, height]
  );

  const totalValue = useMemo(() => items.reduce((s, i) => s + i.value, 0), [items]);

  const handleClick = useCallback(
    (id: string) => {
      drillDown(id);
    },
    [drillDown]
  );

  if (rects.length === 0) {
    return (
      <div className="treemap-empty" data-testid="treemap-empty">
        <p>No container data available for treemap view.</p>
      </div>
    );
  }

  return (
    <div className="treemap-container" data-testid="treemap-view">
      <svg
        width={width}
        height={height}
        viewBox={`0 0 ${width} ${height}`}
        className="treemap-svg"
      >
        {rects.map((rect) => {
          const isHovered = hoveredId === rect.id;
          const pct = totalValue > 0 ? ((rect.value / totalValue) * 100).toFixed(1) : '0';
          const showLabel = rect.width > MIN_LABEL_WIDTH && rect.height > MIN_LABEL_HEIGHT;

          return (
            <g
              key={rect.id}
              className="treemap-cell"
              data-testid={`treemap-cell-${rect.id}`}
              onClick={() => handleClick(rect.id)}
              onMouseEnter={() => setHoveredId(rect.id)}
              onMouseLeave={() => setHoveredId(null)}
              style={{ cursor: 'pointer' }}
            >
              {/* Rectangle */}
              <rect
                x={rect.x + 1}
                y={rect.y + 1}
                width={Math.max(rect.width - 2, 0)}
                height={Math.max(rect.height - 2, 0)}
                rx={4}
                fill={rect.color}
                fillOpacity={isHovered ? 0.95 : 0.75}
                stroke={isHovered ? '#e2e8f0' : 'rgba(255,255,255,0.1)'}
                strokeWidth={isHovered ? 2 : 1}
                className="treemap-rect"
              />

              {/* Label */}
              {showLabel && (
                <>
                  <text
                    x={rect.x + rect.width / 2}
                    y={rect.y + rect.height / 2 - 8}
                    textAnchor="middle"
                    dominantBaseline="middle"
                    className="treemap-label"
                    fill="#fff"
                    fontSize={Math.min(14, rect.width / 8)}
                    fontWeight={600}
                  >
                    {truncateName(rect.name, rect.width)}
                  </text>
                  <text
                    x={rect.x + rect.width / 2}
                    y={rect.y + rect.height / 2 + 12}
                    textAnchor="middle"
                    dominantBaseline="middle"
                    className="treemap-value"
                    fill="rgba(255,255,255,0.7)"
                    fontSize={Math.min(12, rect.width / 10)}
                  >
                    {rect.value.toLocaleString()} atoms ({pct}%)
                  </text>
                </>
              )}
            </g>
          );
        })}
      </svg>

      {/* Tooltip */}
      {hoveredId && (
        <div className="treemap-tooltip" data-testid="treemap-tooltip">
          {(() => {
            const rect = rects.find((r) => r.id === hoveredId);
            if (!rect) return null;
            const pct = totalValue > 0 ? ((rect.value / totalValue) * 100).toFixed(1) : '0';
            return (
              <>
                <strong>{rect.name}</strong>
                <br />
                {rect.value.toLocaleString()} atoms · {pct}%
                <br />
                <span className="treemap-tooltip-hint">Click to drill down</span>
              </>
            );
          })()}
        </div>
      )}
    </div>
  );
}

function truncateName(name: string, maxWidth: number): string {
  const maxChars = Math.floor(maxWidth / 9);
  if (name.length <= maxChars) return name;
  // Prefer last segment for readability
  const parts = name.split('.');
  const last = parts[parts.length - 1];
  if (last.length <= maxChars) return last;
  return last.slice(0, maxChars - 1) + '…';
}
