import { formatDistanceToNow } from 'date-fns';

interface StatusFooterProps {
  x: number;
  y: number;
  nodeCount: number;
  edgeCount: number;
  scannedAt?: string;
}

export function StatusFooter({ x, y, nodeCount, edgeCount, scannedAt }: StatusFooterProps) {
  const analyzedText = scannedAt 
    ? formatDistanceToNow(new Date(scannedAt), { addSuffix: true })
    : 'unknown';

  return (
    <footer 
      className="h-8 px-6 flex items-center justify-between text-xs"
      style={{ 
        backgroundColor: 'var(--color-bg-dark)', 
        borderTop: '1px solid var(--color-border-dark)',
        color: '#64748b'
      }}
    >
      <div className="flex items-center gap-6">
        <span>
          <span className="text-slate-500">X:</span> 
          <span className="ml-1 tabular-nums">{x.toFixed(1)}</span>
          <span className="text-slate-500 ml-2">Y:</span> 
          <span className="ml-1 tabular-nums">{y.toFixed(1)}</span>
        </span>
        <span>
          <span style={{ color: 'var(--color-primary)' }}>●</span>
          <span className="ml-1">Nodes:</span> 
          <span className="ml-1 font-medium text-white">{nodeCount}</span>
        </span>
        <span>
          <span style={{ color: 'var(--color-primary)' }}>—</span>
          <span className="ml-1">Edges:</span> 
          <span className="ml-1 font-medium text-white">{edgeCount}</span>
        </span>
      </div>
      
      <div className="flex items-center gap-4">
        <span className="flex items-center gap-1.5">
          <span className="w-1.5 h-1.5 rounded-full bg-green-500"></span>
          <span>ANALYZED: {analyzedText.toUpperCase()}</span>
        </span>
        <span className="text-slate-600">|</span>
        <span>Cartographer v1.0.0</span>
      </div>
    </footer>
  );
}
