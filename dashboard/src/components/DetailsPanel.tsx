import { AtomInfoPanel } from './AtomInfoPanel';
import { MemberPanel } from './MemberPanel';
import type { DetailsPanelData, Repository, Namespace } from '../types';

interface DetailsPanelProps {
  selectedNodeId: string | null;
  level: string;
  data: DetailsPanelData;
  onClose: () => void;
  onDrillDown: (id: string) => void;
  onSelectAtom: (id: string | null) => void;
}

export function DetailsPanel({
  selectedNodeId,
  level,
  data,
  onClose,
  onDrillDown,
  onSelectAtom
}: DetailsPanelProps) {
  if (!selectedNodeId) return null;

  // Render content based on hierarchical level
  const renderContent = () => {
    // 1. Context Level (System Context)
    if (level === 'context') {
      return (
        <div className="space-y-4">
          <div>
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">System Context</span>
            <h3 className="text-2xl font-bold text-blue-400 mt-1">{selectedNodeId}</h3>
          </div>
          
          <div className="p-4 bg-[#18191f] rounded-[3px] border border-slate-700">
             <div className="flex items-center gap-2 mb-2">
                <span className="material-symbols-outlined text-blue-400">hub</span>
                <span className="font-medium text-slate-200">System Purpose</span>
             </div>
             <p className="text-sm text-slate-400 leading-relaxed">
               Core aggregator for business capabilities. Handles cross-domain orchestration and state synchronization.
             </p>
          </div>

          <button
            onClick={() => onDrillDown(selectedNodeId)}
            className="w-full py-2.5 px-4 bg-[#a09078] hover:bg-[#b8a48c] text-white rounded-[3px] text-sm font-medium transition-colors flex items-center justify-center gap-2"
          >
            Explore System <span className="material-symbols-outlined text-sm">arrow_forward</span>
          </button>
          
          <div>
             <span className="text-xs text-slate-500 block mb-2">Top Owners</span>
             <div className="flex -space-x-2">
                {[1,2,3].map(i => (
                    <div key={i} className="w-8 h-8 rounded-full bg-slate-700 border-2 border-slate-900 flex items-center justify-center text-xs">
                        U{i}
                    </div>
                ))}
             </div>
          </div>
        </div>
      );
    }

    // 2. System Level (Repositories)
    if (level === 'system' && data?.federation) {
      const repo = data.federation.repositories.find(
        (r: Repository) => r.id === selectedNodeId
      );
      if (!repo) return <p className="text-slate-400">Repository not found</p>;

      return (
        <div className="space-y-6">
          <div>
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">Repository</span>
             <div className="flex items-center gap-2 mt-1">
                <h3 className="text-xl font-bold text-[#c4a882] break-all">{repo.name}</h3>
                <a href="#" className="text-slate-500 hover:text-white"><span className="material-symbols-outlined text-sm">open_in_new</span></a>
             </div>
          </div>

          {/* Owner Info */}
          {repo.owner && (
             <div className="flex items-center gap-3 p-3 bg-[#18191f] rounded-[3px] border border-slate-700">
                <img src={repo.owner.avatarUrl} alt={repo.owner.name} className="w-10 h-10 rounded-full border border-slate-600" />
                <div>
                   <div className="text-sm font-bold text-slate-200">{repo.owner.name}</div>
                   <div className="text-xs text-slate-400">{repo.owner.teamName}</div>
                </div>
             </div>
          )}

          {/* Quality Metrics */}
          {repo.qualityMetrics && (
          <div className="grid grid-cols-2 gap-3">
             <div className="p-3 bg-[#18191f] rounded-[3px] border border-slate-700">
                <span className="text-[10px] text-slate-500 uppercase">Coverage</span>
                <div className="flex items-center gap-2 mt-1">
                    <span className={`w-2 h-2 rounded-full ${repo.qualityMetrics.coveragePercent > 80 ? 'bg-green-500' : 'bg-amber-500'}`}></span>
                    <span className="text-sm font-medium text-slate-200">{repo.qualityMetrics.coveragePercent}%</span>
                </div>
             </div>
             <div className="p-3 bg-[#18191f] rounded-[3px] border border-slate-700">
                <span className="text-[10px] text-slate-500 uppercase">SonarQube</span>
                <div className="flex items-center gap-2 mt-1">
                    <div className="px-1.5 py-0.5 bg-[#2a2520] text-[#c4a882] border border-[#3a3530] rounded text-xs font-bold">{repo.qualityMetrics.sonarRating}</div>
                    <span className="text-sm font-medium text-slate-300">Rating</span>
                </div>
             </div>
             <div className="col-span-2 p-3 bg-[#18191f] rounded-[3px] border border-slate-700 flex justify-between items-center">
                <span className="text-[10px] text-slate-500 uppercase">Complexity</span>
                <span className="text-sm font-mono text-slate-200">{repo.qualityMetrics.cyclomaticComplexity}</span>
             </div>
          </div>
          )}

          {/* Commit Velocity (Mock) */}
          <div className="p-4 bg-[#18191f] rounded-[3px] border border-slate-700">
             <div className="flex justify-between items-end mb-2">
                <span className="text-xs text-slate-500 uppercase">Commit Velocity</span>
                <span className="text-xs text-slate-500">Last 30 Days</span>
             </div>
             <div className="h-12 flex items-end gap-1">
                {/* Mock bar chart */}
                {[4, 7, 3, 8, 12, 5, 9, 14, 6, 8, 10, 15, 8, 4, 7].map((h, i) => (
                    <div key={i} className="flex-1 bg-[#2a2520] hover:bg-[#3a3530] transition-colors rounded-sm" style={{ height: `${(h/15)*100}%` }}></div>
                ))}
             </div>
             <div className="mt-2 text-center text-xs font-medium text-[#c4a882]">+124 commits</div>
          </div>

          <div className="grid grid-cols-2 gap-4 text-sm">
             <div>
                <span className="text-xs text-slate-500">Atoms</span>
                <p className="text-slate-200 font-mono">{repo.atomCount}</p>
             </div>
             <div>
                <span className="text-xs text-slate-500">Namespaces</span>
                <p className="text-slate-200 font-mono">{repo.namespaces?.length ?? 0}</p>
             </div>
          </div>

          <button
            onClick={() => onDrillDown(repo.id)}
            className="w-full py-2.5 px-4 bg-[#a09078] hover:bg-[#b8a48c] text-white rounded-[3px] text-sm font-medium transition-colors"
          >
            Explore Repository
          </button>
        </div>
      );
    }

    // 3. Container Level (Namespaces)
    if (level === 'repository' && data?.repository) {
      const ns = data.repository.namespaces?.find(
        (n: Namespace) => n.path === selectedNodeId
      );
      if (!ns) return <p className="text-slate-400">Namespace not found</p>;

      return (
        <div className="space-y-5">
           <div>
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">Namespace</span>
            <h3 className="text-lg font-bold text-[#8a7a9a] mt-1 break-all">{ns.path}</h3>
          </div>

          <div className="grid grid-cols-3 gap-2">
             <div className="p-2 text-center bg-[#18191f] rounded-[3px] border border-slate-700">
                <span className="block text-[10px] text-slate-500">Atoms</span>
                <span className="text-white font-medium">{ns.atomCount}</span>
             </div>
             <div className="p-2 text-center bg-[#18191f] rounded-[3px] border border-slate-700">
                <span className="block text-[10px] text-slate-500">DTOs</span>
                <span className="text-green-400 font-medium">{ns.dtoCount}</span>
             </div>
             <div className="p-2 text-center bg-[#18191f] rounded-[3px] border border-slate-700">
                <span className="block text-[10px] text-slate-500">Interfaces</span>
                <span className="text-blue-400 font-medium">{ns.interfaceCount}</span>
             </div>
          </div>

          <button
            onClick={() => onDrillDown(ns.path)}
            className="w-full py-2.5 px-4 bg-[#8a7a9a] hover:bg-[#9a8aaa] text-white rounded-[3px] text-sm font-medium transition-colors"
          >
            Explore Namespace
          </button>
        </div>
      );
    }

    // 4. Component Level Details
    if (level === 'component' && data?.atom) {
        // Safe transform for AtomInfoPanel expectations
        const safeAtom = {
            ...data.atom,
            repository: data.atom.repository || 'Unknown',
            inboundLinks: data.atom.inboundLinks || [],
            outboundLinks: data.atom.outboundLinks || []
        };
        return <AtomInfoPanel atom={safeAtom} onClose={onClose} onAtomClick={onSelectAtom} />;
    }

    // 5. Code Level Details
    if (level === 'code' && data?.atom) {
        // Safe transform for MemberPanel expectations
        const safeMembers = (data.atom.members || []).map(m => ({
            ...m,
            isPublic: m.isPublic ?? false
        }));
        
        return <MemberPanel atomName={data.atom.name} members={safeMembers} onClose={onClose} />;
    }

    return <div className="text-slate-500 italic">No details available.</div>;
  };

  return (
    <aside className="w-80 border-l border-slate-700 p-5 overflow-y-auto bg-[#1e2026] absolute right-0 top-16 bottom-16 z-20 transition-transform duration-300 flex flex-col">
      <div className="flex justify-between items-start mb-6">
        <h2 className="text-lg font-semibold text-white tracking-tight">Details</h2>
        <button 
          onClick={onClose}
          className="text-slate-400 hover:text-white transition-colors"
          title="Close"
        >
          <span className="material-symbols-outlined">close</span>
        </button>
      </div>
      
      <div className="flex-1">
        {renderContent()}
      </div>
    </aside>
  );
}
