import { useEffect, useRef, useCallback } from 'react';
import * as d3 from 'd3';

interface Node {
  id: string;
  name: string;
  type: string;
  riskScore?: number;
  consumerCount?: number;
  group?: string;
  x?: number;
  y?: number;
  fx?: number | null;
  fy?: number | null;
  status?: 'added' | 'removed' | 'modified' | 'unchanged';
  vx?: number;
  vy?: number;
}

interface Link {
  source: string | Node;
  target: string | Node;
  type?: string;
  crossRepo?: boolean;
  isViolation?: boolean;
}

interface ForceGraphProps {
  nodes: Node[];
  links: Link[];
  showLinks?: boolean;
  selectedNodeId?: string | null;
  onNodeSelect?: (node: Node) => void;
  onNodeDrillDown?: (node: Node) => void;
  blastRadiusAtoms?: Set<string>;
  blastRadiusDepths?: Map<string, number>;
  width?: number;
  height?: number;
  onViewportChange?: (x: number, y: number, nodeCount: number, edgeCount: number) => void;
  diffMode?: boolean;
  governanceMode?: boolean;
  // Physics tuning props (for GraphLab)
  chargeStrength?: number;
  linkDistance?: number;
  collisionRadiusMultiplier?: number;
}

// Material Symbol names mapped to atom types (C4 aligned)
const TYPE_ICONS: Record<string, string> = {
  repository: 'hexagon',           // C4: bounded system
  container: 'dns',                // C4: container (Project)
  namespace: 'folder_open',        // container grouping
  dto: 'data_object',              // data transfer object
  interface: 'circle',             // hollow = contract
  class: 'square',                 // solid = concrete
  service: 'settings',             // gear = service
  record: 'receipt_long',          // data carrier
  enum: 'list',                    // stacked lines
  table: 'table_chart',            // database table
  column: 'view_column',           // unchanged
  storedprocedure: 'code',         // procedure
  delegate: 'arrow_forward',       // function pointer
  struct: 'diamond',               // value type
  neighbor: 'help_outline'         // external/unknown
};

// Risk score to color mapping (muted tones)
function getRiskColor(riskScore: number): string {
  if (riskScore >= 0.75) return '#b45454'; // muted red
  if (riskScore >= 0.5) return '#b87c4a';  // muted orange
  if (riskScore >= 0.25) return '#a89645'; // muted yellow
  return '#5a8a5a'; // muted green
}

// Type to muted color mapping - flat desaturated tones
function getTypeColor(type: string): string {
  switch (type?.toLowerCase()) {
    case 'repository': return '#f59e0b'; // Amber-500
    case 'container': return '#0ea5e9';  // Sky-500 (Container/Project)
    case 'namespace': return '#8b5cf6';  // Violet-500
    case 'dto': return '#a08cba'; // muted lavender
    case 'interface': return '#c9a87c'; // muted amber
    case 'class': return '#7aa3a3'; // muted teal
    case 'service': return '#7aa3a3'; // muted teal
    case 'record': return '#9ba087'; // muted sage
    case 'enum': return '#bc9a9a'; // muted rose
    case 'table': return '#8b9dc3'; // muted periwinkle
    case 'storedprocedure': return '#a89690'; // muted taupe
    default: return '#8a8a8a'; // neutral grey
  }
}

// Shape path for each node type (C4 aligned, centered at 0,0, size ~40px)
function getNodeShape(type: string): string {
  switch (type?.toLowerCase()) {
    case 'repository': // Hexagon - bounded system (C4 L1)
      return 'M 0,-20 L 17.3,-10 L 17.3,10 L 0,20 L -17.3,10 L -17.3,-10 Z';
    case 'container': // Container (Project) - same as namespace for now (grouping)
    case 'namespace': // Rounded rectangle - container grouping (C4 L2)
      return 'M -22,-14 L 22,-14 Q 26,-14 26,-10 L 26,10 Q 26,14 22,14 L -22,14 Q -26,14 -26,10 L -26,-10 Q -26,-14 -22,-14 Z';
    case 'interface': // Hollow circle - contract (C4 L3)
      return 'M 0,-18 A 18,18 0 1,1 0,18 A 18,18 0 1,1 0,-18 Z';
    case 'class': // Solid square - concrete implementation (C4 L3)
      return 'M -16,-16 L 16,-16 L 16,16 L -16,16 Z';
    case 'dto': // Square with corner notch - data carrier (C4 L3)
      return 'M -16,-16 L 16,-16 L 16,8 L 8,16 L -16,16 Z';
    case 'service': // Octagon - service component (C4 L3)
      return 'M -8,-18 L 8,-18 L 18,-8 L 18,8 L 8,18 L -8,18 L -18,8 L -18,-8 Z';
    case 'record': // Rounded rectangle with lines - data record
      return 'M -20,-12 L 20,-12 Q 24,-12 24,-8 L 24,8 Q 24,12 20,12 L -20,12 Q -24,12 -24,8 L -24,-8 Q -24,-12 -20,-12 Z';
    case 'enum': // Stacked horizontal bars (C4 spec)
      return 'M -16,-14 L 16,-14 L 16,-6 L -16,-6 Z M -16,-2 L 16,-2 L 16,6 L -16,6 Z M -16,10 L 16,10 L 16,18 L -16,18 Z';
    case 'struct': // Diamond - value type (C4 spec)
      return 'M 0,-20 L 18,0 L 0,20 L -18,0 Z';
    case 'delegate': // Arrow shape - function pointer
      return 'M -18,0 L 0,-14 L 0,-6 L 18,-6 L 18,6 L 0,6 L 0,14 Z';
    case 'table': // Cylinder top - database (C4 spec)
      return 'M -16,-14 L 16,-14 A 16,6 0 0,1 16,-8 L 16,14 A 16,6 0 0,1 -16,14 L -16,-8 A 16,6 0 0,0 -16,-14 Z';
    case 'storedprocedure': // Terminal prompt - procedure
      return 'M -16,-14 L 16,-14 L 16,14 L -16,14 Z M -12,-4 L -4,2 L -12,8';
    default: // Circle fallback
      return 'M 0,-16 A 16,16 0 1,1 0,16 A 16,16 0 1,1 0,-16 Z';
  }
}

// Dynamic node sizing based on type/consumer count
function getNodeSize(node: Node): number {
  if (node.type === 'repository') return 40;
  if (node.type === 'container') return 30; // Between Repo (40) and Namespace (20)
  if (node.type === 'namespace') return 20;
  
  // Default code level or fallback
  const base = 12;
  const metric = (node.consumerCount ?? 0) + 1;
  return Math.min(base + Math.log(metric) * 2, 25);
}

function getBlastRadiusColor(depth: number): string {
  const colors = ['#dc2626', '#f97316', '#fb923c', '#fdba74', '#fed7aa'];
  return colors[Math.min(depth, colors.length - 1)];
}

export function ForceGraph({
  nodes,
  links,
  showLinks = true,
  selectedNodeId,
  onNodeSelect,
  onNodeDrillDown,
  blastRadiusAtoms,
  blastRadiusDepths,
  width = 800,
  height = 600,
  onViewportChange,
  diffMode = false,
  governanceMode = false,
  // Default physics values
  chargeStrength = -300,
  linkDistance = 80,
  collisionRadiusMultiplier = 1.1
}: ForceGraphProps) {
  const svgRef = useRef<SVGSVGElement>(null);
  const clickTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const transformRef = useRef({ x: 0, y: 0, k: 1 });
  
  // Store counts in refs to avoid callback recreation on every filter change
  const nodesCountRef = useRef(nodes.length);
  const linksCountRef = useRef(links.length);
  nodesCountRef.current = nodes.length;
  linksCountRef.current = links.length;
  
  // Store onViewportChange in a ref to prevent callback identity issues
  const onViewportChangeRef = useRef(onViewportChange);
  onViewportChangeRef.current = onViewportChange;
  
  // Create stable signature to prevent re-render from array reference changes
  const nodesSignature = JSON.stringify(nodes.map(n => n.id).sort());
  const linksSignature = JSON.stringify(links.map(l => `${l.source}-${l.target}`).sort());

  // Completely stable callback - uses only refs, no dependencies
  const reportViewport = useCallback(() => {
    if (onViewportChangeRef.current) {
      const t = transformRef.current;
      onViewportChangeRef.current(
        Math.round(t.x * 10) / 10,
        Math.round(t.y * 10) / 10,
        nodesCountRef.current,
        linksCountRef.current
      );
    }
  }, []);

  useEffect(() => {
    if (!svgRef.current || nodes.length === 0) return;

    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    // Grid Layout Fallback (No Links) - "Constellation Fix"
    if (links.length === 0 && nodes.length > 0) {
       const aspect = width / height;
       // Calculate grid dimensions
       const cols = Math.ceil(Math.sqrt(nodes.length * aspect));
       const rows = Math.ceil(nodes.length / cols);
       const cellWidth = width / (cols + 1);
       const cellHeight = height / (rows + 1);
       
       // Center offset
       const startX = (width - (cols * cellWidth)) / 2 + cellWidth/2;
       const startY = (height - (rows * cellHeight)) / 2 + cellHeight/2;

       nodes.forEach((node, i) => {
          const col = i % cols;
          const row = Math.floor(i / cols);
          // Fix positions to grid
          node.fx = startX + col * cellWidth;
          node.fy = startY + row * cellHeight;
       });
    } else {
       // Reset fixed positions if switching back to force mode
       nodes.forEach(n => {
          if (n.status !== 'modified') { // Don't reset if specifically interacting (basic check)
              n.fx = null;
              n.fy = null;
          }
       });
    }

    // Solar System Physics (KI Research)
    const simulation = d3.forceSimulation<Node>(nodes)
      .force('link', d3.forceLink<Node, Link>(links).id(d => d.id).distance(linkDistance))
      .force('charge', d3.forceManyBody().strength(chargeStrength))
      .force('center', d3.forceCenter(width / 2, height / 2))
      .force('collision', d3.forceCollide<Node>().radius(d => getNodeSize(d) * collisionRadiusMultiplier));

    // Add Group Centroid Force (Cohesion "Islands")
    if (links.length > 0) {
        simulation.force('group', alpha => {
            const centroids: Record<string, {x: number, y: number, count: number}> = {};
            
            // Compute centroids
            nodes.forEach(n => {
               const g = n.group || 'root';
               if (!centroids[g]) centroids[g] = {x: 0, y: 0, count: 0};
               const x = n.x ?? 0;
               const y = n.y ?? 0;
               centroids[g].x += x;
               centroids[g].y += y;
               centroids[g].count++;
            });
            
            // Apply force
            const k = 0.1 * alpha; // Strength factor
            nodes.forEach(n => {
               const g = n.group || 'root';
               const c = centroids[g];
               if (!c) return;
               
               const cx = c.x / c.count;
               const cy = c.y / c.count;
               
               n.vx = (n.vx ?? 0) + (cx - (n.x ?? 0)) * k;
               n.vy = (n.vy ?? 0) + (cy - (n.y ?? 0)) * k;
            });
        });
    }

    simulation
      .alphaDecay(0.05)     // Faster cooling (Stable "Solar System")
      .velocityDecay(0.6);  // High friction (Prevent drift)

    // Add zoom behavior
    const g = svg.append('g');
    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .extent([[0, 0], [width, height]])
      .scaleExtent([0.2, 4])
      .on('zoom', (event) => {
        g.attr('transform', event.transform);
        transformRef.current = { x: event.transform.x, y: event.transform.y, k: event.transform.k };
        g.attr('transform', event.transform);
        transformRef.current = { x: event.transform.x, y: event.transform.y, k: event.transform.k };
        
        // LOD: Hide labels when zoomed out
        if (event.transform.k < 1.2) {
           g.selectAll('.node-label-text').style('opacity', 0);
        } else {
           g.selectAll('.node-label-text').style('opacity', 1);
        }

        reportViewport();
      });
    svg.call(zoom);

    // Arrow marker for links
    svg.append('defs').append('marker')
      .attr('id', 'arrowhead')
      .attr('viewBox', '0 0 10 7')
      .attr('refX', 10)
      .attr('refY', 3.5)
      .attr('markerWidth', 8)
      .attr('markerHeight', 6)
      .attr('orient', 'auto')
      .append('polygon')
      .attr('points', '0 0, 10 3.5, 0 7')
      .attr('fill', '#224249');

    // Corrosion Filter (Turbulence for "Entropy")
    const filter = svg.select('defs').append('filter')
      .attr('id', 'corrosion')
      .attr('x', '-20%')
      .attr('y', '-20%')
      .attr('width', '140%')
      .attr('height', '140%');
    
    filter.append('feTurbulence')
      .attr('type', 'fractalNoise')
      .attr('baseFrequency', '0.05')
      .attr('numOctaves', '3')
      .attr('result', 'noise');
      
    filter.append('feDisplacementMap')
      .attr('in', 'SourceGraphic')
      .attr('in2', 'noise')
      .attr('scale', '3')
      .attr('xChannelSelector', 'R')
      .attr('yChannelSelector', 'G');


    // Draw links
    const link = g.append('g')
      .attr('class', 'links')
      .selectAll('line')
      .data(links)
      .join('line')
      .attr('stroke', d => getLinkColor(d, selectedNodeId))
      .attr('stroke-width', d => getLinkWidth(d, selectedNodeId))
      .attr('stroke-width', d => getLinkWidth(d, selectedNodeId))
      .attr('stroke-opacity', d => {
          if (!showLinks) return 0;
          if (governanceMode) {
             return d.isViolation ? 1 : 0.1; // Dim non-violations
          }
          return 0.6;
      })
      .attr('stroke', d => {
          if (governanceMode && d.isViolation) return '#ef4444'; // Red-500 for violations
          return getLinkColor(d, selectedNodeId);
      })
      .attr('stroke-dasharray', d => {
          if (governanceMode && d.isViolation) return '4,2'; // Dashed for violation
          return getLinkDashArray(d);
      }) 
      .attr('marker-end', 'url(#arrowhead)');
      
      // Animate Violations
      if (governanceMode) {
         link.filter(d => !!d.isViolation)
             .style('animation', 'pulse-edge 1s infinite');
      }

    // Draw node groups
    const node = g.append('g')
      .selectAll<SVGGElement, Node>('g')
      .data(nodes)
      .join('g')
      .attr('class', 'node')
      .style('cursor', 'pointer')
      .call(d3.drag<SVGGElement, Node>()
        .on('start', (event, d) => {
          if (!event.active) simulation.alphaTarget(0.3).restart();
          d.fx = d.x;
          d.fy = d.y;
          if (clickTimerRef.current) {
            clearTimeout(clickTimerRef.current);
            clickTimerRef.current = null;
          }
        })
        .on('drag', (event, d) => {
          d.fx = event.x;
          d.fy = event.y;
        })
        .on('end', (event, d) => {
          if (!event.active) simulation.alphaTarget(0);
          d.fx = null;
          d.fy = null;
        }));

    // Node shapes using paths
    node.append('path')
      .attr('d', d => getNodeShape(d.type))
      .attr('fill', d => {
        // Background: 25% opacity of the border color
        const color = getBorderColor(d, blastRadiusAtoms, blastRadiusDepths);
        return `${color}40`; // 25% opacity
      })
      .attr('stroke', d => getBorderColor(d, blastRadiusAtoms, blastRadiusDepths))
      .attr('stroke-width', d => {
        if (d.id === selectedNodeId) return 3;
        // Thicker border for high inbound dependencies
        const inbound = d.consumerCount ?? 0;
        return Math.min(1.5 + inbound * 0.2, 5);
      })
      .style('filter', d => {
         // Apply corrosion filter to high risk nodes
         if ((d.riskScore || 0) > 0.7) return 'url(#corrosion)';
         return null;
      });

    // Selection glow ring (uses same shape, slightly scaled up)
    node.filter(d => d.id === selectedNodeId)
      .insert('path', ':first-child')
      .attr('d', d => getNodeShape(d.type))
      .attr('transform', 'scale(1.3)')
      .attr('fill', 'none')
      .attr('stroke', '#a0b8c0') // muted cyan
      .attr('stroke-width', 2)
      .attr('stroke-opacity', 0.8) // Less vague opacity
      .attr('stroke-dasharray', '2,2'); // Tactical dashed ring instead of glow

    // Apply Diff Mode Styles
    if (diffMode) {
      node.each(function(d) {
         const g = d3.select(this);
         const shape = g.select('path'); // Select the path element
         
         if (d.status === 'added') {
            shape.attr('stroke', '#4ade80') // green-400
                 .attr('stroke-width', 3)
                 .attr('stroke-dasharray', null)
                 .style('filter', 'drop-shadow(0 0 4px rgba(74, 222, 128, 0.5))');
         } else if (d.status === 'removed') {
            shape.attr('stroke', '#ef4444') // red-500
                 .attr('stroke-width', 3)
                 .attr('stroke-dasharray', '4,2')
                 .style('opacity', 0.6);
         } else if (d.status === 'modified') {
            shape.attr('stroke', '#eab308') // yellow-500
                 .attr('stroke-width', 3);
         }
      });
    } else {
      // Reset styles if not in diff mode
       node.each(function(d) {
         const g = d3.select(this);
         const shape = g.select('path');
         // Re-apply original logic (simplified reset here, ideally would re-run full style logic)
         // But actually D3's enter/update cycle handles this if we re-render.
         // However, if we just toggle the prop, we need to update attributes
         
         // Re-apply border thickness based on selection/importance
          if (d.id === selectedNodeId) {
              shape.attr('stroke-width', 3);
          } else {
              const inbound = d.consumerCount ?? 0;
              shape.attr('stroke-width', Math.min(1.5 + inbound * 0.2, 5));
          }
           shape.attr('stroke', (d: unknown) => getBorderColor(d as Node, blastRadiusAtoms, blastRadiusDepths)); // Re-apply original color
          shape.attr('stroke-dasharray', null); // Remove dash array
          shape.style('filter', null).style('opacity', 1);
       });
    }

    // Material icon (using foreignObject for proper rendering)
    node.append('foreignObject')
      .attr('width', (d: unknown) => getNodeSize(d as Node))
      .attr('height', (d: unknown) => getNodeSize(d as Node))
      .attr('x', d => -getNodeSize(d) / 2)
      .attr('y', d => -getNodeSize(d) / 2)
      .append('xhtml:div')
      .style('width', '100%')
      .style('height', '100%')
      .style('display', 'flex')
      .style('align-items', 'center')
      .style('justify-content', 'center')
      .style('color', (d: Node) => getBorderColor(d, blastRadiusAtoms, blastRadiusDepths))
      .style('font-size', d => `${Math.round(getNodeSize(d) * 0.45)}px`)
      .attr('class', 'material-symbols-outlined')
      .text((d: Node) => TYPE_ICONS[d.type?.toLowerCase()] || 'circle');

    // Node labels
    node.append('text')
      .attr('dy', d => getNodeSize(d) / 2 + 14)
      .attr('text-anchor', 'middle')
      .attr('fill', '#e2e8f0') // slate-200 for better visibility on dark
      .attr('font-size', '10px')
      .attr('font-weight', '400')
      .attr('font-family', 'Inter, sans-serif')
      .attr('letter-spacing', '0.02em')
      // "Outline" effect for readability using paint-order
      .attr('stroke', '#0f172a') // slate-900 background color
      .attr('stroke-width', 3)
      .attr('paint-order', 'stroke')
      .attr('stroke-linejoin', 'round')
      .attr('class', 'node-label-text') // Add class for LOD selection
      .style('opacity', 0) // Start hidden, let zoom handler reveal if needed (or default to visible if k=1)
      .text(d => truncate(d.name, 18));


    // Click handlers
    node.on('click', function(event: MouseEvent, d: Node) {
      event.stopPropagation();
      event.preventDefault();
      
      if (clickTimerRef.current) {
        clearTimeout(clickTimerRef.current);
        clickTimerRef.current = null;
      }
      
      clickTimerRef.current = setTimeout(() => {
        onNodeSelect?.(d);
        clickTimerRef.current = null;
      }, 250);
    });

    node.on('dblclick', function(event: MouseEvent, d: Node) {
      event.stopPropagation();
      event.preventDefault();
      
      if (clickTimerRef.current) {
        clearTimeout(clickTimerRef.current);
        clickTimerRef.current = null;
      }
      
      onNodeDrillDown?.(d);
    });

    // Click background to deselect
    svg.on('click', () => {
      if (clickTimerRef.current) {
        clearTimeout(clickTimerRef.current);
        clickTimerRef.current = null;
      }
      onNodeSelect?.(undefined as unknown as Node);
    });

    // Hover effect - subtle brighten updates, no glow
    node.on('mouseenter', function() {
      d3.select(this).select('path') // Changed from 'rect' to 'path'
        .transition()
        .duration(150)
        .attr('stroke-width', 4)
        .style('opacity', 1);
    });

    node.on('mouseleave', function(_event, d) {
       // Reset if not selected
       if (d.id !== selectedNodeId) {
          d3.select(this).select('path') // Changed from 'rect' to 'path'
            .transition()
            .duration(150)
            .attr('stroke-width', (d: unknown) => {
               const inbound = (d as Node).consumerCount ?? 0;
               return Math.min(1.5 + inbound * 0.2, 5);
            });
       }
    });

    // Update link highlighting
    if (selectedNodeId) {
      link
        .attr('stroke', d => getLinkColor(d, selectedNodeId))
        .attr('stroke-width', d => getLinkWidth(d, selectedNodeId))
        .attr('stroke-opacity', d => {
          if (!showLinks) return 0;
          if (isConnectedLink(d, selectedNodeId)) return 1;
          return 0.15;
        });
    }

    // Simulation tick
    simulation.on('tick', () => {
      link
        .attr('x1', d => getNodeX(d.source))
        .attr('y1', d => getNodeY(d.source))
        .attr('x2', d => getNodeX(d.target))
        .attr('y2', d => getNodeY(d.target));

      node.attr('transform', d => `translate(${d.x ?? 0},${d.y ?? 0})`);
    });

    // Initial viewport report
    // Initial viewport report and LOD check
    // Ensure labels are visible initially if zoom is default
    g.selectAll('.node-label-text').style('opacity', 1);
    
    reportViewport();

    return () => {
      simulation.stop();
      if (clickTimerRef.current) {
        clearTimeout(clickTimerRef.current);
      }
    };
    // Use signatures instead of array references to prevent re-render cascade
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [nodesSignature, linksSignature, showLinks, selectedNodeId, blastRadiusAtoms, blastRadiusDepths, width, height, onNodeSelect, onNodeDrillDown, reportViewport, diffMode, governanceMode]);

  // Pan to Selected Node
  useEffect(() => {
     if (!selectedNodeId || !svgRef.current) return;
     
     const node = nodes.find(n => n.id === selectedNodeId);
     if (!node || node.x === undefined || node.y === undefined) return;

     const svg = d3.select(svgRef.current);
     // Calculate target transform
     // Standard zoom level 1.5 for focus
     const scale = 1.5; // Fixed zoom level for consistency
     
     // Center of screen
     const cy = height / 2;
     
     // Valid panel offset (assuming 320px panel on right)
     // We shift the center point to the LEFT by half the panel width
     // so the node appears in the "visual center" of the remaining space.
     const panelWidth = 320;
     const visualCenterX = (width - panelWidth) / 2;
     
     // We want the node to be at (visualCenterX, cy)
     // transform.x = visualCenterX - node.x * scale
     // transform.y = cy - node.y * scale
     const tx = visualCenterX - node.x * scale;
     const ty = cy - node.y * scale;

     const zoomIdentity = d3.zoomIdentity.translate(tx, ty).scale(scale);
     
     // Use transition for smooth movement
     svg.transition()
        .duration(750)
        .ease(d3.easeCubicOut) // Smooth ease out
        .call(
             d3.zoom<SVGSVGElement, unknown>().transform, zoomIdentity
        );

  }, [selectedNodeId, nodes, width, height]);

  return (
    <svg 
      ref={svgRef} 
      width={width} 
      height={height}
      style={{ background: 'transparent' }}
    />
  );
}

// Helper functions
function getBorderColor(node: Node, blastRadiusAtoms?: Set<string>, blastRadiusDepths?: Map<string, number>): string {
  if (blastRadiusAtoms?.has(node.id)) {
    const depth = blastRadiusDepths?.get(node.id) ?? 0;
    return getBlastRadiusColor(depth);
  }
  if (node.riskScore !== undefined && node.riskScore > 0) {
    return getRiskColor(node.riskScore);
  }
  return getTypeColor(node.type);
}

function getNodeX(node: string | Node): number {
  return typeof node === 'object' ? (node.x ?? 0) : 0;
}

function getNodeY(node: string | Node): number {
  return typeof node === 'object' ? (node.y ?? 0) : 0;
}

function truncate(str: string, len: number): string {
  return str.length > len ? str.substring(0, len) + '...' : str;
}

function isConnectedLink(link: Link, nodeId: string): boolean {
  const sourceId = typeof link.source === 'object' ? link.source.id : link.source;
  const targetId = typeof link.target === 'object' ? link.target.id : link.target;
  return sourceId === nodeId || targetId === nodeId;
}

function getLinkColor(link: Link, selectedNodeId: string | null | undefined): string {
  if (selectedNodeId && isConnectedLink(link, selectedNodeId)) {
    return '#a0b8c0'; // Muted cyan (Tactical)
  }
  if (link.crossRepo) return '#a855f7'; // Purple
  return '#224249'; // Border dark
}

function getLinkWidth(link: Link, selectedNodeId: string | null | undefined): number {
  if (selectedNodeId && isConnectedLink(link, selectedNodeId)) return 2.5;
  return link.crossRepo ? 2 : 1.5;
}

// C4 edge styling: solid=inheritance, dashed=implements, dotted=uses/references
function getLinkDashArray(link: Link): string {
  const linkType = link.type?.toLowerCase();
  switch (linkType) {
    case 'inherits':
      return '0'; // solid line
    case 'implements':
      return '6,4'; // dashed line
    case 'uses':
    case 'references':
    case 'calls':
      return '2,2'; // dotted line
    default:
      return '0'; // solid fallback
  }
}
