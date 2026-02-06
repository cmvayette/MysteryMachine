import { RiskBadge } from './RiskBadge';

interface LinkInfo {
  atomId: string;
  linkType: string;
}

interface AtomData {
  id: string;
  name: string;
  type: string;
  namespace?: string;
  filePath?: string;
  repository: string;
  riskScore?: number;
  linesOfCode?: number | null;
  language?: string | null;
  isPublic?: boolean;
  inboundLinks: LinkInfo[];
  outboundLinks: LinkInfo[];
}

interface AtomInfoPanelProps {
  atom: AtomData;
  onClose: () => void;
  onAtomClick?: (atomId: string) => void;
}

export function AtomInfoPanel({ atom, onClose, onAtomClick }: AtomInfoPanelProps) {
  return (
    <div className="panel w-80 max-h-[500px] overflow-y-auto">
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-lg font-semibold text-white truncate flex items-center gap-2" title={atom.name}>
          {getTypeIcon(atom.type)} {atom.name}
          {atom.isPublic !== undefined && (
            <span 
              className={`text-[10px] px-1.5 py-0.5 rounded-full border ${
                atom.isPublic 
                  ? 'border-cyan-500/50 text-cyan-400 bg-cyan-950/30' 
                  : 'border-amber-500/50 text-amber-400 bg-amber-950/30'
              }`}
            >
              {atom.isPublic ? 'PUB' : 'INT'}
            </span>
          )}
        </h3>
        <div className="flex items-center gap-2">
          {atom.riskScore && atom.riskScore > 0.5 && (
            <span className="text-amber-500 animate-pulse" title="High Risk Detected">⚠️</span>
          )}
          <button 
            onClick={onClose}
            className="text-slate-400 hover:text-white text-xl leading-none"
          >
            ×
          </button>
        </div>
      </div>
      
      <dl className="space-y-2 text-sm">
        <div className="flex justify-between">
          <dt className="text-slate-400">Type</dt>
          <dd className="text-white">{atom.type}</dd>
        </div>
        
        {atom.namespace && (
          <div className="flex justify-between">
            <dt className="text-slate-400">Namespace</dt>
            <dd className="text-white truncate max-w-[180px]" title={atom.namespace}>
              {atom.namespace}
            </dd>
          </div>
        )}
        
        <div className="flex justify-between">
          <dt className="text-slate-400">Repository</dt>
          <dd className="text-white">{atom.repository}</dd>
        </div>
        
        {atom.riskScore !== undefined && (
          <div className="flex justify-between items-center">
            <dt className="text-slate-400">Risk Score</dt>
            <dd><RiskBadge score={atom.riskScore} size="sm" /></dd>
          </div>
        )}
        
        {atom.filePath && (
          <div>
            <dt className="text-slate-400 mb-1">File</dt>
            <dd className="text-xs text-slate-300 break-all bg-slate-700 p-1 rounded">
              {atom.filePath}
            </dd>
          </div>
        )}

        {/* Risk Assessment Section used to be here, moved to badge/header */}
        {atom.riskScore && atom.riskScore > 0.3 && (
           <div className="mt-2 pt-2 border-t border-slate-700">
             <dt className="text-slate-400 text-xs mb-1">Risk Factors</dt>
             <dd className="space-y-1">
               {atom.riskScore > 0.7 && (
                 <div className="flex items-center gap-2 text-red-400 text-xs">
                   <span className="material-symbols-outlined text-[14px]">warning</span>
                   <span>Critical Complexity</span>
                 </div>
               )}
               {atom.inboundLinks.length > 5 && (
                 <div className="flex items-center gap-2 text-amber-400 text-xs">
                    <span className="material-symbols-outlined text-[14px]">hub</span>
                    <span>High Coupling ({atom.inboundLinks.length} consumers)</span>
                 </div>
               )}
             </dd>
           </div>
        )}
      </dl>
      
      {/* Metrics Grid */}
      {(atom.linesOfCode || atom.language) && (
        <div className="mt-4 grid grid-cols-2 gap-2">
          {atom.linesOfCode && (
            <div className="bg-slate-700/50 rounded-lg p-3 text-center">
              <div className="text-2xl font-bold text-cyan-400">
                {atom.linesOfCode.toLocaleString()}
              </div>
              <div className="text-xs text-slate-400 mt-1">Lines of Code</div>
            </div>
          )}
          {atom.language && (
            <div className="bg-slate-700/50 rounded-lg p-3 text-center">
              <div className="text-2xl font-bold text-purple-400">
                {getLanguageIcon(atom.language)}
              </div>
              <div className="text-xs text-slate-400 mt-1">{atom.language}</div>
            </div>
          )}
        </div>
      )}
      
      {/* Inbound Links */}
      {atom.inboundLinks.length > 0 && (
        <div className="mt-4">
          <h4 className="text-sm font-medium text-slate-300 mb-2">
            ← Inbound ({atom.inboundLinks.length})
          </h4>
          <ul className="space-y-1 text-xs">
            {atom.inboundLinks.slice(0, 10).map((link, i) => (
              <li 
                key={i}
                onClick={() => onAtomClick?.(link.atomId)}
                className="flex justify-between text-slate-400 hover:text-white cursor-pointer p-1 rounded hover:bg-slate-700"
              >
                <span className="truncate">{link.atomId}</span>
                <span className="text-slate-500">{link.linkType}</span>
              </li>
            ))}
            {atom.inboundLinks.length > 10 && (
              <li className="text-slate-500">...and {atom.inboundLinks.length - 10} more</li>
            )}
          </ul>
        </div>
      )}
      
      {/* Outbound Links */}
      {atom.outboundLinks.length > 0 && (
        <div className="mt-4">
          <h4 className="text-sm font-medium text-slate-300 mb-2">
            → Outbound ({atom.outboundLinks.length})
          </h4>
          <ul className="space-y-1 text-xs">
            {atom.outboundLinks.slice(0, 10).map((link, i) => (
              <li 
                key={i}
                onClick={() => onAtomClick?.(link.atomId)}
                className="flex justify-between text-slate-400 hover:text-white cursor-pointer p-1 rounded hover:bg-slate-700"
              >
                <span className="truncate">{link.atomId}</span>
                <span className="text-slate-500">{link.linkType}</span>
              </li>
            ))}
            {atom.outboundLinks.length > 10 && (
              <li className="text-slate-500">...and {atom.outboundLinks.length - 10} more</li>
            )}
          </ul>
        </div>
      )}
    </div>
  );
}

function getTypeIcon(type: string): string {
  switch (type?.toLowerCase()) {
    case 'dto': return '📋';
    case 'interface': return '🔌';
    case 'class': return '🧱';
    case 'table': return '🗃️';
    case 'column': return '📊';
    case 'storedprocedure': return '⚙️';
    default: return '●';
  }
}

function getLanguageIcon(language: string): string {
  switch (language?.toLowerCase()) {
    case 'csharp':
    case 'c#': return '🔷';
    case 'sql': return '🗄️';
    case 'typescript':
    case 'ts': return '🔶';
    case 'javascript':
    case 'js': return '🟡';
    default: return '📄';
  }
}
