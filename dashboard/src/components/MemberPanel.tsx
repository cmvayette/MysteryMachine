

interface MemberInfo {
  id: string;
  name: string;
  type: string;
  signature?: string;
  isPublic: boolean;
}

interface MemberPanelProps {
  atomName: string;
  members: MemberInfo[];
  onClose: () => void;
}

export function MemberPanel({ atomName, members, onClose }: MemberPanelProps) {
  // Group members by type
  const methods = members.filter(m => m.type.toLowerCase() === 'method');
  const properties = members.filter(m => m.type.toLowerCase() === 'property' || m.type.toLowerCase() === 'field');
  
  return (
    <div className="panel w-96 max-h-[600px] overflow-y-auto">
      <div className="flex items-center justify-between mb-4 border-b border-slate-700 pb-2">
        <div>
          <h3 className="text-xl font-semibold text-white truncate max-w-[280px]" title={atomName}>
            {atomName}
          </h3>
          <p className="text-xs text-slate-400">Code Structure</p>
        </div>
        <button 
          onClick={onClose}
          className="text-slate-400 hover:text-white text-2xl leading-none"
        >
          ×
        </button>
      </div>
      
      {properties.length > 0 && (
        <div className="mb-6">
          <h4 className="text-sm font-medium text-slate-300 mb-2 flex items-center gap-2">
            <span className="material-symbols-outlined text-sm">data_object</span>
            Properties & Fields
          </h4>
          <ul className="space-y-1">
            {properties.map(prop => (
              <li key={prop.id} className="flex items-center justify-between p-2 bg-slate-800/50 rounded hover:bg-slate-700/50 transition-colors">
                <div className="flex items-center gap-2 overflow-hidden">
                  <span className={`text-xs font-mono px-1 rounded ${prop.isPublic ? 'text-green-400 bg-green-900/30' : 'text-slate-400 bg-slate-700'}`}>
                    {prop.isPublic ? '+' : '-'}
                  </span>
                  <span className="text-sm text-slate-200 truncate font-mono" title={prop.signature || prop.name}>
                    {prop.name}
                  </span>
                </div>
                <span className="text-[10px] text-purple-300 bg-purple-900/20 px-1.5 py-0.5 rounded">
                  {prop.type === 'Field' ? 'fld' : 'prop'}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
      
      {methods.length > 0 && (
        <div>
          <h4 className="text-sm font-medium text-slate-300 mb-2 flex items-center gap-2">
            <span className="material-symbols-outlined text-sm">function</span>
            Methods
          </h4>
          <ul className="space-y-1">
            {methods.map(method => (
              <li key={method.id} className="text-sm p-2 bg-slate-800/50 rounded hover:bg-slate-700/50 transition-colors">
                 <div className="flex items-start gap-2">
                  <span className={`text-xs font-mono px-1 rounded mt-0.5 ${method.isPublic ? 'text-green-400 bg-green-900/30' : 'text-slate-400 bg-slate-700'}`}>
                    {method.isPublic ? '+' : '-'}
                  </span>
                  <div className="overflow-hidden">
                    <div className="text-slate-200 font-mono truncate" title={method.signature || method.name}>
                        {method.name}()
                    </div>
                    {method.signature && (
                        <div className="text-[10px] text-slate-500 truncate mt-0.5 font-mono">
                            {method.signature}
                        </div>
                    )}
                  </div>
                 </div>
              </li>
            ))}
          </ul>
        </div>
      )}

      {members.length === 0 && (
        <div className="text-center py-8 text-slate-500 italic">
          No public members found or scanner not configured for this file.
        </div>
      )}
    </div>
  );
}
