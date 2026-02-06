

interface DiffOverlayProps {
  mode: 'added' | 'removed' | 'modified' | 'breaking';
  label?: string;
}

export function DiffOverlay({ mode, label }: DiffOverlayProps) {
  const getStyles = () => {
    switch (mode) {
      case 'added':
        return 'border-green-500/50 bg-green-500/10 shadow-[0_0_15px_rgba(34,197,94,0.3)]';
      case 'removed':
        return 'border-red-500/50 bg-red-500/10 opacity-50 border-dashed';
      case 'modified':
        return 'border-yellow-500/50 bg-yellow-500/10 animate-pulse';
      case 'breaking':
        return 'border-red-600 bg-red-900/20 shadow-[0_0_15px_rgba(220,38,38,0.5)]';
      default:
        return '';
    }
  };

  return (
    <div className={`absolute inset-[-4px] rounded-lg border-2 pointer-events-none transition-all duration-300 ${getStyles()}`}>
      {mode === 'breaking' && (
        <div className="absolute -top-2 -right-2 bg-red-600 text-white rounded-full w-5 h-5 flex items-center justify-center text-xs font-bold shadow-sm">
          !
        </div>
      )}
      {label && (
        <div className="absolute -bottom-6 left-1/2 -translate-x-1/2 bg-slate-900/90 text-[10px] px-2 py-0.5 rounded text-white whitespace-nowrap border border-slate-700">
          {label}
        </div>
      )}
    </div>
  );
}
