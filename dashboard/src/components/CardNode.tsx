import { memo } from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import { useNavigationStore } from '../store/navigationStore';
import type { ArchitecturalLayer } from '../hooks/useGraphLayout';

// ─── Stereotype Detection ───────────────────────────────────────────
// Infers an architectural role from the atom's C4 type + naming convention.
// This drives shape, icon, and color — making the layout communicate architecture.

type Stereotype =
  | 'system' | 'repository' | 'container' | 'namespace'
  | 'controller' | 'service' | 'repository-pattern' | 'handler'
  | 'factory' | 'middleware' | 'validator'
  | 'interface' | 'dto' | 'record' | 'enum' | 'struct' | 'delegate'
  | 'table' | 'storedprocedure' | 'dbcontext'
  | 'class' | 'unknown';

function inferStereotype(type: string, name: string): Stereotype {
  const t = type?.toLowerCase() ?? '';
  const n = name ?? '';

  // C4 hierarchy types pass through directly
  if (t === 'system') return 'system';
  if (t === 'repository' && !n.endsWith('Repository')) return 'repository';
  if (t === 'container') return 'container';
  if (t === 'namespace') return 'namespace';
  
  // SQL types
  if (t === 'table') return 'table';
  if (t === 'storedprocedure') return 'storedprocedure';
  
  // Type-level pass-throughs
  if (t === 'interface') return 'interface';
  if (t === 'dto') return 'dto';
  if (t === 'record') return 'record';
  if (t === 'enum') return 'enum';
  if (t === 'struct') return 'struct';
  if (t === 'delegate') return 'delegate';

  // ─── Name-based stereotype inference for Classes ───
  if (n.endsWith('Controller') || n.endsWith('Endpoint') || n.endsWith('Api'))
    return 'controller';
  if (n.endsWith('Service') || n.endsWith('Manager') || n.endsWith('Orchestrator'))
    return 'service';
  if (n.endsWith('Repository') || n.endsWith('Repo') || n.endsWith('Store'))
    return 'repository-pattern';
  if (n.endsWith('Handler') || n.endsWith('Consumer') || n.endsWith('Listener'))
    return 'handler';
  if (n.endsWith('Factory') || n.endsWith('Builder') || n.endsWith('Provider'))
    return 'factory';
  if (n.endsWith('Middleware') || n.endsWith('Filter') || n.endsWith('Interceptor'))
    return 'middleware';
  if (n.endsWith('Validator') || n.endsWith('Guard') || n.endsWith('Policy'))
    return 'validator';
  if (n.endsWith('DbContext') || n.endsWith('Context'))
    return 'dbcontext';

  if (t === 'class') return 'class';
  return 'unknown';
}

// ─── Stereotype → Visual Mappings ───────────────────────────────────

// Material Symbol icon per stereotype
const STEREOTYPE_ICONS: Record<Stereotype, string> = {
  system:               'hub',
  repository:           'hexagon',
  container:            'dns',
  namespace:            'folder_open',
  controller:           'api',
  service:              'settings',
  'repository-pattern': 'storage',
  handler:              'bolt',
  factory:              'precision_manufacturing',
  middleware:           'filter_alt',
  validator:            'verified',
  interface:            'circle',
  dto:                  'data_object',
  record:               'receipt_long',
  enum:                 'list',
  struct:               'diamond',
  delegate:             'arrow_forward',
  table:                'table_chart',
  storedprocedure:      'code',
  dbcontext:            'database',
  class:                'square',
  unknown:              'help_outline',
};

// Color per stereotype — clustered by architectural role
function getStereotypeColor(s: Stereotype): string {
  switch (s) {
    // C4 hierarchy
    case 'system':              return '#3b82f6';
    case 'repository':          return '#f59e0b';
    case 'container':           return '#0ea5e9';
    case 'namespace':           return '#8b5cf6';

    // Boundary layer (warm tones — "facing outward")
    case 'controller':          return '#f97316';
    case 'middleware':          return '#fb923c';

    // Business logic layer (cool tones — "core")
    case 'service':             return '#06b6d4';
    case 'handler':             return '#22d3ee';
    case 'validator':           return '#2dd4bf';
    case 'factory':             return '#34d399';

    // Data layer (earth tones — "grounded")
    case 'repository-pattern':  return '#a3e635';
    case 'dbcontext':           return '#10b981';
    case 'table':               return '#8b9dc3';
    case 'storedprocedure':     return '#6d8a6d';

    // Type definitions (muted — "passive")
    case 'interface':           return '#94a3b8';
    case 'dto':                 return '#a08cba';
    case 'record':              return '#b0a0c8';
    case 'enum':                return '#bc9a9a';
    case 'struct':              return '#b8a07a';
    case 'delegate':            return '#a09870';

    // Generic
    case 'class':               return '#7aa3a3';
    default:                    return '#8a8a8a';
  }
}

// CSS shape class per stereotype
function getShapeClass(s: Stereotype): string {
  switch (s) {
    // Boundary nodes — hexagonal (facing the outside world)
    case 'controller':
    case 'middleware':
    case 'repository':          return 'card-node--hexagon';

    // Service nodes — octagonal (the engine room)
    case 'service':
    case 'handler':             return 'card-node--octagon';

    // Data access — stadium/cylinder shape
    case 'repository-pattern':
    case 'dbcontext':
    case 'table':               return 'card-node--stadium';

    // Creational patterns — diamond
    case 'factory':             return 'card-node--diamond';

    // Contracts — pill (soft boundary)
    case 'interface':
    case 'validator':           return 'card-node--pill';

    // Data carriers — notched (transfer shape)
    case 'dto':
    case 'record':              return 'card-node--notched';

    // Enums — stacked cards
    case 'enum':                return 'card-node--stacked';

    // Stored procedures — trapezoid (processing)
    case 'storedprocedure':     return 'card-node--trapezoid';

    // Delegates — arrow-like
    case 'delegate':            return 'card-node--arrow';

    // Container/namespace/system — rounded
    case 'container':
    case 'namespace':
    case 'system':              return 'card-node--rounded';

    // Everything else — square
    case 'class':
    case 'struct':
    default:                    return 'card-node--square';
  }
}

// Display label for the stereotype (shown in card header)
function getStereotypeLabel(s: Stereotype): string {
  switch (s) {
    case 'repository-pattern':  return 'repository';
    case 'dbcontext':           return 'db-context';
    default:                    return s;
  }
}

// ─── Metrics + Utilities ────────────────────────────────────────────

function getRiskDotColor(score?: number): string {
  if (score == null || score === 0) return '#5a8a5a';
  if (score >= 0.75) return '#b45454';
  if (score >= 0.5)  return '#b87c4a';
  if (score >= 0.25) return '#a89645';
  return '#5a8a5a';
}

function getMetrics(data: Record<string, unknown>): string {
  const parts: string[] = [];

  if (typeof data.consumerCount === 'number' && data.consumerCount > 0)
    parts.push(`${data.consumerCount} repos`);
  if (typeof data.atomCount === 'number')
    parts.push(`${data.atomCount} atoms`);
  if (typeof data.linesOfCode === 'number')
    parts.push(`LOC: ${data.linesOfCode}`);
  if (typeof data.riskScore === 'number' && data.riskScore > 0)
    parts.push(`Risk: ${(data.riskScore as number).toFixed(1)}`);
  if (typeof data.branch === 'string')
    parts.push(data.branch as string);
  if (typeof data.language === 'string')
    parts.push(data.language as string);

  return parts.slice(0, 3).join('  ·  ');
}

function getDiffStyle(status?: string): React.CSSProperties {
  switch (status) {
    case 'added':    return { boxShadow: '0 0 12px rgba(34,197,94,0.3)', borderColor: 'rgba(34,197,94,0.5)' };
    case 'removed':  return { opacity: 0.5, borderStyle: 'dashed', borderColor: 'rgba(239,68,68,0.5)' };
    case 'modified': return { boxShadow: '0 0 12px rgba(234,179,8,0.3)', borderColor: 'rgba(234,179,8,0.5)' };
    default:         return {};
  }
}

// ─── Layer Badge ────────────────────────────────────────────────────

const LAYER_COLORS: Record<ArchitecturalLayer, string | null> = {
  presentation:   '#f97316',   // warm orange
  application:    '#06b6d4',   // cyan
  domain:         '#a78bfa',   // purple
  infrastructure: '#10b981',   // green
  external:       '#6b7280',   // gray
  unknown:        null,        // no badge
};

// ─── Component ──────────────────────────────────────────────────────

function CardNodeComponent({ data, id }: NodeProps) {
  const selectedId = useNavigationStore((s) => s.selectedAtomId);
  const isSelected = selectedId === id;
  const nodeData = data as Record<string, unknown>;
  const type = (nodeData.type as string) || 'unknown';
  const name = (nodeData.name as string) || (nodeData.id as string) || '?';

  // Infer architectural stereotype from type + naming convention
  const stereotype = inferStereotype(type, name);
  const icon = STEREOTYPE_ICONS[stereotype];
  const color = getStereotypeColor(stereotype);
  const riskColor = getRiskDotColor(nodeData.riskScore as number | undefined);
  const shapeClass = getShapeClass(stereotype);
  const label = getStereotypeLabel(stereotype);
  const metrics = getMetrics(nodeData);
  const diffStyle = getDiffStyle(nodeData.status as string | undefined);

  // Layer detection (passed from useGraphLayout, or infer locally as fallback)
  const layer = (nodeData.layer as ArchitecturalLayer) ?? 'unknown';
  const layerColor = LAYER_COLORS[layer];

  // Topology-aware hub indicator
  const isHub = nodeData.isHub === true;

  return (
    <>
      <Handle type="target" position={Position.Left} className="card-node__handle" />

      <div
        className={`card-node ${shapeClass} ${isSelected ? 'card-node--selected' : ''} ${isHub ? 'card-node--hub' : ''}`}
        style={{
          borderLeftColor: color,
          ...(layerColor ? { borderTopColor: layerColor, borderTopWidth: 3 } : {}),
          ...diffStyle,
        }}
      >
        {/* Header row: icon + stereotype + risk dot */}
        <div className="card-node__header">
          <span
            className="material-symbols-outlined card-node__icon"
            style={{ color }}
          >
            {icon}
          </span>
          <span className="card-node__type">{label}</span>
          {layerColor && (
            <span className="card-node__layer-badge" style={{ color: layerColor }}>
              {layer}
            </span>
          )}
          {isHub && (
            <span className="card-node__hub-badge">⊛ HUB</span>
          )}
          <span
            className="card-node__risk-dot"
            style={{ backgroundColor: riskColor }}
          />
        </div>

        {/* Name */}
        <div className="card-node__name" title={name}>
          {name}
        </div>

        {/* Metrics */}
        {metrics && (
          <div className="card-node__metrics">{metrics}</div>
        )}
      </div>

      <Handle type="source" position={Position.Right} className="card-node__handle" />
    </>
  );
}

export const CardNode = memo(CardNodeComponent);
