import { memo, useState } from 'react';

/**
 * EdgeLegend — toggleable overlay showing the heat channel color key.
 *
 * Shows the 4-tier health model and port side meanings.
 * Hidden by default, toggled via the 🔥 button.
 */

const HEAT_ENTRIES = [
  { color: '#22c55e', label: 'Clean', desc: 'No violation' },
  { color: '#f59e0b', label: 'Warning', desc: 'Cross-module' },
  { color: '#ef4444', label: 'Violation', desc: 'Governance rule broken' },
  { color: '#8b5cf6', label: 'Hierarchy', desc: 'Inherits / implements' },
] as const;

const PORT_ENTRIES = [
  { side: '←', label: 'Inbound', desc: 'Consumers flow in' },
  { side: '→', label: 'Outbound', desc: 'Dependencies flow out' },
  { side: '↑', label: 'Inherits', desc: 'Hierarchy upward' },
  { side: '↓', label: 'Data', desc: 'Data sinks down' },
] as const;

function EdgeLegendComponent() {
  const [open, setOpen] = useState(false);

  return (
    <div className="edge-legend-container">
      <button
        className="edge-legend-toggle"
        onClick={() => setOpen(!open)}
        title="Edge Legend"
        aria-label="Toggle edge legend"
      >
        🔥
      </button>

      {open && (
        <div className="edge-legend">
          <div className="edge-legend__section">
            <div className="edge-legend__title">Heat Channels</div>
            {HEAT_ENTRIES.map((e) => (
              <div key={e.label} className="edge-legend__row">
                <span
                  className="edge-legend__swatch"
                  style={{ backgroundColor: e.color }}
                />
                <span className="edge-legend__label">{e.label}</span>
                <span className="edge-legend__desc">{e.desc}</span>
              </div>
            ))}
          </div>

          <div className="edge-legend__divider" />

          <div className="edge-legend__section">
            <div className="edge-legend__title">Port Sides</div>
            {PORT_ENTRIES.map((p) => (
              <div key={p.label} className="edge-legend__row">
                <span className="edge-legend__arrow">{p.side}</span>
                <span className="edge-legend__label">{p.label}</span>
                <span className="edge-legend__desc">{p.desc}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

export const EdgeLegend = memo(EdgeLegendComponent);
