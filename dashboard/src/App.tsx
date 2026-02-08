import { ApolloProvider, useQuery } from '@apollo/client';
import { client, FEDERATION_QUERY, REPOSITORY_QUERY, NAMESPACE_QUERY, ATOM_QUERY, BLAST_RADIUS_QUERY } from './graphql/client';
import { useNavigationStore } from './store/navigationStore';
import { WorkflowGraph } from './components/WorkflowGraph';
import { TreemapView } from './components/TreemapView';
import { StatusFooter } from './components/StatusFooter';
import { FileDropZone } from './components/FileDropZone';
import { useState, useMemo, useCallback } from 'react';
import { inferSystems } from './utils/systemInference';
import { DetailsPanel } from './components/DetailsPanel';
import { Header } from './components/Header';
import './index.css';

function Dashboard() {
  const { 
    level, path, selectedAtomId, blastRadiusMode, diffMode, governanceMode, 
    drillDown, selectAtom, toggleBlastRadiusMode, toggleDiffMode, toggleGovernanceMode,
  } = useNavigationStore();
  const [searchQuery, setSearchQuery] = useState('');
  const [showLinks, setShowLinks] = useState(true);
  const [activeTab, setActiveTab] = useState('Explorer');
  const [activeHeatmap, setActiveHeatmap] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<'graph' | 'treemap'>('graph');

  const handleUploadSuccess = useCallback(() => {
    // Refetch all queries to update the UI
    client.refetchQueries({
        include: [FEDERATION_QUERY]
    });
    // Also trigger a drill down reset if needed
    selectAtom(null);
  }, [selectAtom]);

  // GraphQL queries based on current navigation level
  // GraphQL queries based on current navigation level
  const { data: federationData, loading: fedLoading } = useQuery(FEDERATION_QUERY, {
    skip: level !== 'context' && level !== 'system'
  });

  const { data: repoData, loading: repoLoading } = useQuery(REPOSITORY_QUERY, {
    variables: { id: path[1] },
    skip: (level !== 'repository' && level !== 'project') || !path[1]
  });

  const { data: nsData, loading: nsLoading } = useQuery(NAMESPACE_QUERY, {
    variables: { repoId: path[1], path: path[2] },
    skip: level !== 'component' || path.length < 3
  });

  // Query for L4 code level (using ATOM_QUERY)
  const codeLevelId = level === 'code' ? path[path.length - 1] : selectedAtomId;
  const { data: atomData } = useQuery(ATOM_QUERY, {
    variables: { id: codeLevelId },
    skip: !codeLevelId
  });

  const { data: blastData } = useQuery(BLAST_RADIUS_QUERY, {
    variables: { atomId: selectedAtomId, maxDepth: 5 },
    skip: !selectedAtomId || !blastRadiusMode
  });


  // Snapshot query removed — TimeControls was the only consumer

  // Auto-drill into first repository on initial load
  // Auto-drill disabled to show Context view first
  // useEffect(() => {
  //   if (level === 'context' && federationData?.federation?.repositories?.length > 0) {
  //     // Optional: Auto-select if only 1 system? For now, show context.
  //   }
  // }, [level, federationData, drillDown]);

  // Effective view mode: treemap only works at repository level
  const effectiveViewMode = level === 'repository' ? viewMode : 'graph';

  // Build graph data based on navigation level
  const graphData = useMemo(() => {
    const nodes: { id: string; name: string; type: string; riskScore?: number; churnScore?: number; maintenanceCost?: number; group?: string; category?: string; consumerCount?: number }[] = [];
    const links: { source: string; target: string; type?: string; crossRepo?: boolean }[] = [];

    if (level === 'context' && federationData?.federation) {
        // C4 Level 1: System Context
        const systems = inferSystems(federationData.federation.repositories);
        systems.forEach(sys => {
            nodes.push({
                id: sys.name,
                name: sys.name,
                type: 'system',
                consumerCount: sys.repoCount // Use repo count as size proxy
            });
        });
        
        // TODO: Infer system-to-system links?
        
    } else if (level === 'system' && federationData?.federation) {
         // C4 Level 2: Container (Repository) View
         const systemName = path[0];
         // Filter repos for this system
         // Logic: Name starts with SystemName + "." OR exact match if single repo system
         const fed = federationData.federation;
         
         const systemRepos = fed.repositories.filter((r: { name: string }) => {
             const parts = r.name.split('.');
             return parts[0] === systemName;
         });

         systemRepos.forEach((repo: { id: string; name: string; riskScore?: number; churnScore?: number; maintenanceCost?: number }) => {
            nodes.push({
                id: repo.id,
                name: repo.name,
                type: 'repository',
                riskScore: repo.riskScore,
                churnScore: repo.churnScore,
                maintenanceCost: repo.maintenanceCost
            });
         });
         
         // Add cross-repo links ONLY between repos in this system
         fed.crossRepoLinks.forEach((link: { sourceRepo: string; targetRepo: string; linkType?: string }) => {
            const sourceInSystem = systemRepos.some((r: { id: string }) => r.id === link.sourceRepo);
            const targetInSystem = systemRepos.some((r: { id: string }) => r.id === link.targetRepo);
            
            if (sourceInSystem && targetInSystem) {
                links.push({
                    source: link.sourceRepo,
                    target: link.targetRepo,
                    type: link.linkType,
                    crossRepo: true
                });
            }
         });

    } else if (level === 'repository' && repoData?.repository) {
      const repo = repoData.repository;
      
      // Infer "Containers" (Projects) from Namespace prefixes
      const containers = new Map<string, { id: string; name: string; count: number }>();
      
      repo.namespaces.forEach((ns: { path: string; atomCount?: number }) => {
        // "DiagnosticStructuralLens.Api.Controllers" -> "DiagnosticStructuralLens.Api"
        const parts = ns.path.split('.');
        const containerName = parts.length >= 2 ? `${parts[0]}.${parts[1]}` : parts[0];
        
        if (!containers.has(containerName)) {
           containers.set(containerName, { id: containerName, name: containerName, count: 0 });
        }
        containers.get(containerName)!.count += (ns.atomCount || 0);
      });

      // push inferred containers as nodes
      containers.forEach((c) => {
        nodes.push({
           id: c.id, 
           name: c.name, 
           type: 'container', 
           riskScore: 0,
           consumerCount: c.count 
        });
      });
       
    } else if (level === 'project' && repoData?.repository) {
       // Filter namespaces that belong to the selected "Container" (Project)
       // The container ID is in path[2] because:
       // Context (path[0]) -> System (path[0]) -> Repo (path[1]) -> Project (path[2])
       const containerPrefix = path[2];
       const repo = repoData.repository;
       
       if (containerPrefix) {
           repo.namespaces.forEach((ns: { path: string }) => {
               if (ns.path.startsWith(containerPrefix)) {
                    nodes.push({
                        id: ns.path,
                        name: ns.path.split('.').pop() || ns.path,
                        type: 'namespace',
                        group: containerPrefix // Cluster by project
                    });
               }
           });
           
          // Add links only between these namespaces
          if (repo.namespaceLinks) {
            repo.namespaceLinks.forEach((link: { sourceNamespace: string; targetNamespace: string; linkType?: string }) => {
               if (link.sourceNamespace.startsWith(containerPrefix) && link.targetNamespace.startsWith(containerPrefix)) {
                  links.push({
                    source: link.sourceNamespace,
                    target: link.targetNamespace,
                    type: link.linkType
                  });
               }
            });
          }
       }

    } else if (level === 'component' && nsData?.namespace) {
      const ns = nsData.namespace;
      ns.atoms.forEach((atom: { id: string; name: string; type: string; riskScore?: number; churnScore?: number; maintenanceCost?: number }) => {
        nodes.push({
          id: atom.id,
          name: atom.name,
          type: atom.type,
          riskScore: atom.riskScore,
          churnScore: atom.churnScore,
          maintenanceCost: atom.maintenanceCost
        });
      });
      // Add links between atoms in this namespace
      if (ns.internalLinks) {
        ns.internalLinks.forEach((link: { sourceAtomId: string; targetAtomId: string; linkType?: string }) => {
          links.push({
            source: link.sourceAtomId,
            target: link.targetAtomId,
            type: link.linkType
          });
        });
      }

    } else if (level === 'code' && atomData?.atom) {
      const atom = atomData.atom;
      // Neighbors
      atom.inboundLinks.forEach((l: { atomId: string; linkType: string }) => {
          nodes.push({ id: l.atomId, name: l.atomId, type: 'neighbor' });
          links.push({ source: l.atomId, target: atom.id, type: l.linkType });
      });
      atom.outboundLinks.forEach((l: { atomId: string; linkType: string }) => {
          nodes.push({ id: l.atomId, name: l.atomId, type: 'neighbor' });
          links.push({ source: atom.id, target: l.atomId, type: l.linkType });
      });
    }

    return { nodes, links };
  }, [level, path, federationData, repoData, nsData, atomData]);


  // Blast radius highlighting
  const blastRadiusAtoms = useMemo(() => {
    if (!blastData?.blastRadius) return new Set<string>();
    return new Set<string>(blastData.blastRadius.affectedAtoms.map((a: { atomId: string }) => a.atomId));
  }, [blastData]);

  const blastRadiusDepths = useMemo(() => {
    if (!blastData?.blastRadius) return new Map<string, number>();
    const map = new Map<string, number>();
    blastData.blastRadius.affectedAtoms.forEach((a: { atomId: string; depth: number }) => {
      map.set(a.atomId, a.depth);
    });
    return map;
  }, [blastData]);



  const isLoading = fedLoading || repoLoading || nsLoading;

  return (
    <div className="h-screen flex flex-col" style={{ backgroundColor: 'var(--color-bg-dark)' }}>
      {/* Header */}
      <Header 
        activeTab={activeTab}
        onTabChange={setActiveTab}
        searchQuery={searchQuery}
        onSearchChange={setSearchQuery}
        activeHeatmap={activeHeatmap}
        onHeatmapChange={setActiveHeatmap}
        showLinks={showLinks}
        onToggleLinks={() => setShowLinks(!showLinks)}
      />



      {/* Main Content */}
      <div className="flex-1 flex overflow-hidden">
        {/* Graph Area with Grid */}
        <main className="flex-1 relative force-graph-grid" style={{ backgroundColor: 'var(--color-bg-dark)' }}>
          {isLoading ? (
            <div className="absolute inset-0 flex items-center justify-center">
              <div className="graph-spinner" />
            </div>
          ) : graphData.nodes.length === 0 ? (
            level === 'context' ? (
              <div className="absolute inset-0 flex items-center justify-center p-8">
                <div className="w-full max-w-3xl">
                  <FileDropZone onSuccess={handleUploadSuccess} />
                </div>
              </div>
            ) : (
              <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
                <div className="text-slate-500 bg-[#1e2026] px-4 py-2 rounded-[3px] border border-slate-700">
                  No nodes found in this view
                </div>
              </div>
            )
          ) : effectiveViewMode === 'treemap' && level === 'repository' ? (
            <div className="absolute inset-0">
              <TreemapView nodes={graphData.nodes} />
            </div>
          ) : (
            <WorkflowGraph
              nodes={graphData.nodes}
              links={graphData.links}
              showLinks={showLinks}
              blastRadiusAtoms={blastRadiusMode ? blastRadiusAtoms : undefined}
              blastRadiusDepths={blastRadiusMode ? blastRadiusDepths : undefined}
              diffMode={diffMode}
              governanceMode={activeTab === 'Governance'}
              activeHeatmap={activeHeatmap}
            />
          )}

          {/* View Options Toolbar (Bottom Left - above React Flow controls) */}
          <div className="absolute bottom-16 left-4 z-10 flex flex-col gap-2 p-2 rounded-[3px] bg-[#1e2026] border border-[#2a3038]">
            <div className="flex flex-col gap-2">
                {/* Links Toggle */}
                <button
                onClick={() => setShowLinks(!showLinks)}
                className="w-8 h-8 rounded-[3px] flex items-center justify-center transition-all relative group"
                title={showLinks ? 'Hide Links' : 'Show Links'}
                style={{
                    backgroundColor: showLinks ? '#2a2520' : 'transparent',
                    color: showLinks ? '#c4a882' : '#6b7280'
                }}
                >
                <span className="material-symbols-outlined text-lg">link</span>
                </button>

                {/* Treemap / Graph Toggle (only at L2) */}
                {level === 'repository' && (
                <button
                onClick={() => setViewMode(viewMode === 'graph' ? 'treemap' : 'graph')}
                className="w-8 h-8 rounded-[3px] flex items-center justify-center transition-all relative"
                title={viewMode === 'graph' ? 'Switch to Treemap' : 'Switch to Graph'}
                data-testid="treemap-toggle"
                style={{
                    backgroundColor: viewMode === 'treemap' ? '#1a2a25' : 'transparent',
                    color: viewMode === 'treemap' ? '#5eead4' : '#6b7280'
                }}
                >
                <span className="material-symbols-outlined text-lg">{viewMode === 'graph' ? 'grid_view' : 'hub'}</span>
                </button>
                )}
                
                {/* Blast Radius Toggle */}
                <button
                onClick={toggleBlastRadiusMode}
                className="w-8 h-8 rounded-[3px] flex items-center justify-center transition-all relative"
                title={blastRadiusMode ? 'Hide Blast Radius' : 'Show Blast Radius'}
                style={{
                    backgroundColor: blastRadiusMode ? '#302020' : 'transparent',
                    color: blastRadiusMode ? '#bc9a9a' : '#6b7280'
                }}
                >
                <span className="material-symbols-outlined text-lg">radio_button_checked</span>
                </button>
                
                {/* Diff Mode Toggle */}
                <button
                onClick={toggleDiffMode}
                className="w-8 h-8 rounded-[3px] flex items-center justify-center transition-all relative"
                title={diffMode ? 'Hide Diff' : 'Enable Diff Mode'}
                style={{
                    backgroundColor: diffMode ? '#1a2a1a' : 'transparent',
                    color: diffMode ? '#86efac' : '#6b7280'
                }}
                >
                <span className="material-symbols-outlined text-lg">difference</span>
                </button>

                 {/* Governance Mode Toggle */}
                <button
                onClick={toggleGovernanceMode}
                className="w-8 h-8 rounded-[3px] flex items-center justify-center transition-all relative"
                title={governanceMode ? 'Hide Governance' : 'Show Governance'}
                style={{
                    backgroundColor: governanceMode ? '#2a1a1a' : 'transparent',
                    color: governanceMode ? '#ef4444' : '#6b7280'
                }}
                >
                <span className="material-symbols-outlined text-lg">policy</span>
                </button>
            </div>
          </div>

          {/* Stats Overlay */}
          {federationData?.federation && (level === 'context' || level === 'system') && (
            <div className="absolute top-4 left-4 panel pointer-events-none">
              <h3 className="text-sm font-medium text-slate-300 mb-2">
                {level === 'context' ? 'Global Stats' : 'System Stats'}
              </h3>
              <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
                {level === 'context' ? (
                   <>
                    <dt className="text-slate-400">Systems</dt>
                    <dd className="text-white font-medium">{graphData.nodes.length}</dd>
                    <dt className="text-slate-400">Total Repos</dt>
                    <dd className="text-white font-medium">{federationData.federation.stats.totalRepos}</dd>
                   </>
                ) : (
                   <>
                    <dt className="text-slate-400">Repos</dt>
                    <dd className="text-white font-medium">{graphData.nodes.length}</dd>
                    <dt className="text-slate-400">System</dt>
                    <dd className="text-white font-medium">{path[0]}</dd>
                   </>
                )}
              </dl>
            </div>
          )}



          {/* Info Icon with Tooltip */}
          <div className="absolute bottom-4 right-4 group">
            <button
              className="w-8 h-8 rounded-[3px] flex items-center justify-center transition-all hover:bg-[#252830]"
              style={{ color: '#6b7280' }}
              title="Help"
            >
              <span className="material-symbols-outlined text-lg">help_outline</span>
            </button>
            {/* Tooltip */}
            <div className="absolute bottom-full right-0 mb-2 opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none">
              <div className="panel text-xs whitespace-nowrap">
                <ul className="text-slate-400 space-y-1">
                  <li><span className="text-slate-300">Click</span> select</li>
                  <li><span className="text-slate-300">Double-click</span> drill down</li>
                  <li><span className="text-slate-300">Drag</span> move</li>
                  <li><span className="text-slate-300">Scroll</span> zoom</li>
                </ul>
              </div>
            </div>
          </div>

          {/* Blast Radius Summary */}
          {blastRadiusMode && blastData?.blastRadius && (
            <div className="absolute bottom-4 left-20 panel">
              <h3 className="text-sm font-medium text-red-400 mb-2">💥 Blast Radius</h3>
              <p className="text-xl font-bold text-white mb-2">
                {blastData.blastRadius.totalAffected} atoms
              </p>
              <div className="flex gap-2 flex-wrap">
                {blastData.blastRadius.byDepth.map((d: { depth: number; count: number }) => (
                  <span key={d.depth} className="text-xs px-2 py-1 rounded bg-slate-700">
                    D{d.depth}: {d.count}
                  </span>
                ))}
              </div>
            </div>
          )}
        </main>

        {/* Details Panel - Level Aware */}
        <DetailsPanel
          selectedNodeId={selectedAtomId}
          level={level}
          data={{
             federation: federationData?.federation,
             repository: repoData?.repository,
             namespace: nsData?.namespace,
             atom: atomData?.atom
          }}
          onClose={() => selectAtom(null)}
          onDrillDown={drillDown}
          onSelectAtom={selectAtom}
        />
      </div>

      {/* Status Footer - counts from filtered data, no D3 state sync */}
      <StatusFooter
        x={0}
        y={0}
        nodeCount={graphData.nodes.length}
        edgeCount={graphData.links.length}
        scannedAt={federationData?.federation?.federatedAt}
      />
    </div>
  );
}

function App() {
  return (
    <ApolloProvider client={client}>
      <Dashboard />
    </ApolloProvider>
  );
}

export default App;
