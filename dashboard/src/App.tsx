import { ApolloProvider, useQuery } from '@apollo/client';
import { client, FEDERATION_QUERY, REPOSITORY_QUERY, NAMESPACE_QUERY, ATOM_QUERY, BLAST_RADIUS_QUERY } from './graphql/client';
import { useNavigationStore } from './store/navigationStore';
import { Breadcrumb } from './components/Breadcrumb';
import { ForceGraph } from './components/ForceGraph';
import { AtomInfoPanel } from './components/AtomInfoPanel';
import { MemberPanel } from './components/MemberPanel';
import { StatusFooter } from './components/StatusFooter';
import { ZoomControls } from './components/ZoomControls';
import { useState, useMemo, useCallback, useEffect } from 'react';
import './index.css';

function Dashboard() {
  const { level, path, selectedAtomId, blastRadiusMode, diffMode, drillDown, selectAtom, toggleBlastRadiusMode, toggleDiffMode } = useNavigationStore();
  const [searchQuery, setSearchQuery] = useState('');
  const [showLinks, setShowLinks] = useState(true);


  // Zoom handlers (placeholder - actual implementation needs ref to D3 zoom)
  const handleZoomIn = useCallback(() => {
    // TODO: Wire to D3 zoom
    console.log('Zoom in');
  }, []);
  const handleZoomOut = useCallback(() => {
    console.log('Zoom out');
  }, []);
  const handleFitToScreen = useCallback(() => {
    console.log('Fit to screen');
  }, []);


  // GraphQL queries based on current navigation level
  const { data: federationData, loading: fedLoading } = useQuery(FEDERATION_QUERY, {
    skip: level !== 'context'
  });

  const { data: repoData, loading: repoLoading } = useQuery(REPOSITORY_QUERY, {
    variables: { id: path[0] },
    skip: level !== 'container' || !path[0]
  });

  const { data: nsData, loading: nsLoading } = useQuery(NAMESPACE_QUERY, {
    variables: { repoId: path[0], path: path[1] },
    skip: level !== 'component' || path.length < 2
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

  // Auto-drill into first repository on initial load
  useEffect(() => {
    if (level === 'context' && federationData?.federation?.repositories?.length > 0) {
      const firstRepo = federationData.federation.repositories[0];
      drillDown(firstRepo.id);
    }
  }, [level, federationData, drillDown]);

  // Build graph data based on navigation level
  const graphData = useMemo(() => {
    const nodes: { id: string; name: string; type: string; riskScore?: number; group?: string; category?: string; consumerCount?: number }[] = [];
    const links: { source: string; target: string; type?: string; crossRepo?: boolean }[] = [];

    if (level === 'context' && federationData?.federation) {
      const fed = federationData.federation;
      fed.repositories.forEach((repo: { id: string; name: string; riskScore?: number }) => {
        nodes.push({
          id: repo.id,
          name: repo.name,
          type: 'repository',
          riskScore: repo.riskScore
        });
      });
      fed.crossRepoLinks.forEach((link: { sourceRepo: string; targetRepo: string; linkType?: string }) => {
        links.push({
          source: link.sourceRepo,
          target: link.targetRepo,
          type: link.linkType,
          crossRepo: true
        });
      });
    } else if (level === 'container' && repoData?.repository) {
      const repo = repoData.repository;
      repo.namespaces.forEach((ns: { path: string }) => {
        nodes.push({
          id: ns.path,
          name: ns.path.split('.').pop() || ns.path,
          type: 'namespace',
          group: ns.path
        });
      });
      // Add links between namespaces if available
      if (repo.namespaceLinks) {
        repo.namespaceLinks.forEach((link: { sourceNamespace: string; targetNamespace: string; linkType?: string }) => {
          links.push({
            source: link.sourceNamespace,
            target: link.targetNamespace,
            type: link.linkType
          });
        });
      }
    } else if (level === 'component' && nsData?.namespace) {
      const ns = nsData.namespace;
      ns.atoms.forEach((atom: { id: string; name: string; type: string; riskScore?: number }) => {
        nodes.push({
          id: atom.id,
          name: atom.name,
          type: atom.type,
          riskScore: atom.riskScore
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
      // L4: Code Level - Show atom and its members
      const rootAtom = atomData.atom;
      
      // Central node (the class/interface)
      nodes.push({
         id: rootAtom.id,
         name: rootAtom.name,
         type: rootAtom.type,
         riskScore: rootAtom.riskScore,
         group: 'root'
      });

      // Member nodes
      if (rootAtom.members) {
         rootAtom.members.forEach((member: { id: string; name: string; type: string; isPublic: boolean }) => {
            nodes.push({
               id: member.id,
               name: member.name,
               type: member.type, // Method, Property, Field
               group: 'member',
               category: member.isPublic ? 'public' : 'private'
            });

            // Link from root to member
            links.push({
               source: rootAtom.id,
               target: member.id,
               type: 'Contains'
            });
         });
      }
    }

    return { nodes, links };
  }, [level, federationData, repoData, nsData, atomData]);


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

  // Single click = select node (shows details panel)
  const handleNodeSelect = (node: { id: string } | null) => {
    if (!node) {
      selectAtom(null);
      return;
    }
    selectAtom(node.id);
  };

  // Double click = drill down into node
  const handleNodeDrillDown = (node: { id: string }) => {
    // Prevent drilling down further if at code level
    if (level !== 'code') {
      drillDown(node.id);
    }
  };

  const isLoading = fedLoading || repoLoading || nsLoading;

  return (
    <div className="h-screen flex flex-col" style={{ backgroundColor: 'var(--color-bg-dark)' }}>
      {/* Header */}
      <header className="h-16 px-6 flex items-center gap-6" style={{ backgroundColor: 'var(--color-bg-dark)', borderBottom: '1px solid var(--color-border-dark)' }}>
        <div className="flex items-center gap-3">
          <span className="text-3xl" style={{ color: 'var(--color-primary)' }}>⬡</span>
          <h1 className="text-xl font-bold text-white tracking-tight">Mystery Machine</h1>
        </div>
        <Breadcrumb className="flex-1" />
        
        {/* Search */}
        <div className="relative">
          <input
            type="text"
            placeholder="Search architecture..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="rounded-lg pl-4 pr-4 py-2 text-sm text-white w-64 placeholder-slate-400 focus:outline-none focus:ring-2"
            style={{ 
              backgroundColor: 'var(--color-surface-dark)', 
              border: '1px solid var(--color-border-dark)'
            }}
          />
        </div>
      </header>

      {/* Main Content */}
      <div className="flex-1 flex overflow-hidden">
        {/* Graph Area with Grid */}
        <main className="flex-1 relative force-graph-grid" style={{ backgroundColor: 'var(--color-bg-dark)' }}>
          {isLoading ? (
            <div className="absolute inset-0 flex items-center justify-center">
              <div className="text-slate-400">Loading...</div>
            </div>
          ) : graphData.nodes.length === 0 ? (
            <div className="absolute inset-0 flex items-center justify-center">
              <div className="text-center text-slate-400">
                <p className="text-lg">No data available</p>
                <p className="text-sm">Start the API and load a federated snapshot</p>
              </div>
            </div>
          ) : (
            <ForceGraph
              nodes={graphData.nodes}
              links={graphData.links}
              showLinks={showLinks}
              selectedNodeId={selectedAtomId}
              onNodeSelect={handleNodeSelect}
              onNodeDrillDown={handleNodeDrillDown}
              blastRadiusAtoms={blastRadiusMode ? blastRadiusAtoms : undefined}
              blastRadiusDepths={blastRadiusMode ? blastRadiusDepths : undefined}
              diffMode={diffMode}
              width={window.innerWidth - (selectedAtomId ? 320 : 0)}
              height={window.innerHeight - 100}
            />
          )}

          {/* Floating Toolbar */}
          <div className="absolute bottom-4 left-4 flex items-center gap-2">
            {/* Zoom Controls */}
            <ZoomControls
              onZoomIn={handleZoomIn}
              onZoomOut={handleZoomOut}
              onFitToScreen={handleFitToScreen}
            />
            
            {/* Separator */}
            <div className="w-px h-8 bg-slate-600/50" />
            
            {/* Links Toggle */}
            <button
              onClick={() => setShowLinks(!showLinks)}
              className="w-10 h-10 rounded-lg flex items-center justify-center transition-all"
              title={showLinks ? 'Hide Links' : 'Show Links'}
              style={{
                backgroundColor: showLinks ? 'rgba(122, 155, 163, 0.25)' : 'rgba(42, 48, 56, 0.6)',
                border: showLinks ? '1px solid rgba(122, 155, 163, 0.5)' : '1px solid rgba(42, 48, 56, 0.8)',
                color: showLinks ? '#a0b8c0' : '#6b7280'
              }}
            >
              <span className="material-symbols-outlined text-xl">link</span>
            </button>
            
            {/* Blast Radius Toggle */}
            <button
              onClick={toggleBlastRadiusMode}
              className="w-10 h-10 rounded-lg flex items-center justify-center transition-all"
              title={blastRadiusMode ? 'Hide Blast Radius' : 'Show Blast Radius'}
              style={{
                backgroundColor: blastRadiusMode ? 'rgba(180, 84, 84, 0.25)' : 'rgba(42, 48, 56, 0.6)',
                border: blastRadiusMode ? '1px solid rgba(180, 84, 84, 0.5)' : '1px solid rgba(42, 48, 56, 0.8)',
                color: blastRadiusMode ? '#bc9a9a' : '#6b7280'
              }}
            >
              <span className="material-symbols-outlined text-xl">radio_button_checked</span>
            </button>
            
            {/* Diff Mode Toggle */}
            <button
              onClick={toggleDiffMode}
              className="w-10 h-10 rounded-lg flex items-center justify-center transition-all"
              title={diffMode ? 'Hide Diff' : 'Show Diff'}
              style={{
                backgroundColor: diffMode ? 'rgba(34, 197, 94, 0.25)' : 'rgba(42, 48, 56, 0.6)',
                border: diffMode ? '1px solid rgba(34, 197, 94, 0.5)' : '1px solid rgba(42, 48, 56, 0.8)',
                color: diffMode ? '#86efac' : '#6b7280'
              }}
            >
              <span className="material-symbols-outlined text-xl">difference</span>
            </button>
          </div>

          {/* Smart Filters - coming soon: will replace ModuleFilter with opt-in smell detection */}

          {/* Stats Overlay */}
          {federationData?.federation && level === 'context' && (
            <div className="absolute top-4 left-4 panel">
              <h3 className="text-sm font-medium text-slate-300 mb-2">Federation Stats</h3>
              <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
                <dt className="text-slate-400">Repos</dt>
                <dd className="text-white font-medium">{federationData.federation.stats.totalRepos}</dd>
                <dt className="text-slate-400">Code Atoms</dt>
                <dd className="text-white font-medium">{federationData.federation.stats.totalCodeAtoms}</dd>
                <dt className="text-slate-400">SQL Atoms</dt>
                <dd className="text-white font-medium">{federationData.federation.stats.totalSqlAtoms}</dd>
                <dt className="text-slate-400">Links</dt>
                <dd className="text-white font-medium">{federationData.federation.stats.totalLinks}</dd>
                <dt className="text-slate-400">Cross-Repo</dt>
                <dd className="text-white font-medium">{federationData.federation.stats.crossRepoLinkCount}</dd>
              </dl>
            </div>
          )}

          {/* Info Icon with Tooltip */}
          <div className="absolute bottom-4 right-4 group">
            <button
              className="w-10 h-10 rounded-lg flex items-center justify-center transition-all"
              style={{
                backgroundColor: 'rgba(42, 48, 56, 0.6)',
                border: '1px solid rgba(42, 48, 56, 0.8)',
                color: '#6b7280'
              }}
              title="Interactions help"
            >
              <span className="material-symbols-outlined text-xl">help_outline</span>
            </button>
            {/* Tooltip - appears on hover */}
            <div className="absolute bottom-full right-0 mb-2 opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none">
              <div className="panel text-xs whitespace-nowrap">
                <ul className="text-slate-400 space-y-1">
                  <li><span className="text-slate-300">Click</span> to select & view details</li>
                  <li><span className="text-slate-300">Double-click</span> to drill down</li>
                  <li><span className="text-slate-300">Drag</span> to move nodes</li>
                  <li><span className="text-slate-300">Scroll</span> to zoom</li>
                </ul>
              </div>
            </div>
          </div>

          {/* Blast Radius Summary */}
          {blastRadiusMode && blastData?.blastRadius && (
            <div className="absolute bottom-4 left-4 panel">
              <h3 className="text-sm font-medium text-red-400 mb-2">💥 Blast Radius</h3>
              <p className="text-2xl font-bold text-white mb-2">
                {blastData.blastRadius.totalAffected} atoms affected
              </p>
              <div className="flex gap-2 flex-wrap">
                {blastData.blastRadius.byDepth.map((d: { depth: number; count: number }) => (
                  <span key={d.depth} className="text-xs px-2 py-1 rounded bg-slate-700">
                    Depth {d.depth}: {d.count}
                  </span>
                ))}
              </div>
            </div>
          )}
        </main>

        {/* Details Panel - Level Aware */}
        {selectedAtomId && (
          <aside className="w-80 border-l border-slate-700 p-4 overflow-y-auto bg-slate-800/50">
            <div className="flex justify-between items-start mb-4">
              <h2 className="text-lg font-semibold text-white">Details</h2>
              <button 
                onClick={() => selectAtom(null)}
                className="text-slate-400 hover:text-white text-xl"
              >
                ×
              </button>
            </div>
            
            {/* Context level - show repo info */}
            {level === 'context' && federationData?.federation && (
              <div className="space-y-3">
                {(() => {
                  const repo = federationData.federation.repositories.find(
                    (r: { id: string }) => r.id === selectedAtomId
                  );
                  if (!repo) return <p className="text-slate-400">Repository not found</p>;
                  return (
                    <>
                      <div>
                        <span className="text-xs text-slate-400">Repository</span>
                        <p className="text-cyan-400 font-medium">📦 {repo.name}</p>
                      </div>
                      <div>
                        <span className="text-xs text-slate-400">Atoms</span>
                        <p className="text-white">{repo.atomCount}</p>
                      </div>
                      <div>
                        <span className="text-xs text-slate-400">Namespaces</span>
                        <p className="text-white">{repo.namespaces?.length ?? 0}</p>
                      </div>
                      <button
                        onClick={() => drillDown(repo.id)}
                        className="mt-4 w-full py-2 px-3 bg-cyan-600 hover:bg-cyan-500 text-white rounded text-sm"
                      >
                        Explore Repository →
                      </button>
                    </>
                  );
                })()}
              </div>
            )}
            
            {/* Container level - show namespace info */}
            {level === 'container' && repoData?.repository && (
              <div className="space-y-3">
                {(() => {
                  const ns = repoData.repository.namespaces.find(
                    (n: { path: string }) => n.path === selectedAtomId
                  );
                  if (!ns) return <p className="text-slate-400">Namespace not found</p>;
                  return (
                    <>
                      <div>
                        <span className="text-xs text-slate-400">Namespace</span>
                        <p className="text-cyan-400 font-medium">📁 {ns.path}</p>
                      </div>
                      <div>
                        <span className="text-xs text-slate-400">Total Atoms</span>
                        <p className="text-white">{ns.atomCount}</p>
                      </div>
                      <div className="flex gap-4">
                        <div>
                          <span className="text-xs text-slate-400">DTOs</span>
                          <p className="text-green-400">{ns.dtoCount}</p>
                        </div>
                        <div>
                          <span className="text-xs text-slate-400">Interfaces</span>
                          <p className="text-blue-400">{ns.interfaceCount}</p>
                        </div>
                      </div>
                      <button
                        onClick={() => drillDown(ns.path)}
                        className="mt-4 w-full py-2 px-3 bg-cyan-600 hover:bg-cyan-500 text-white rounded text-sm"
                      >
                        Explore Namespace →
                      </button>
                    </>
                  );
                })()}
              </div>
            )}
            
            {/* Component level - show atom details */}
            {level === 'component' && atomData?.atom && (
              <AtomInfoPanel
                atom={atomData.atom}
                onClose={() => selectAtom(null)}
                onAtomClick={(id) => selectAtom(id)}
              />
            )}
            
            {/* Code level - show member panel */}
            {level === 'code' && atomData?.atom && (
               <MemberPanel 
                  atomName={atomData.atom.name}
                  members={atomData.atom.members || []}
                  onClose={() => selectAtom(null)}
               />
            )}
          </aside>
        )}
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
