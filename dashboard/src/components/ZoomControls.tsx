interface ZoomControlsProps {
  onZoomIn: () => void;
  onZoomOut: () => void;
  onFitToScreen: () => void;
}

export function ZoomControls({ onZoomIn, onZoomOut, onFitToScreen }: ZoomControlsProps) {
  return (
    <div className="flex items-center gap-2">
      <button
        onClick={onZoomIn}
        className="w-10 h-10 rounded-lg flex items-center justify-center transition-all hover:brightness-125"
        style={{ 
          backgroundColor: 'rgba(42, 48, 56, 0.6)',
          border: '1px solid rgba(42, 48, 56, 0.8)',
          color: '#6b7280'
        }}
        title="Zoom In"
      >
        <span className="material-symbols-outlined text-xl">add</span>
      </button>
      
      <button
        onClick={onZoomOut}
        className="w-10 h-10 rounded-lg flex items-center justify-center transition-all hover:brightness-125"
        style={{ 
          backgroundColor: 'rgba(42, 48, 56, 0.6)',
          border: '1px solid rgba(42, 48, 56, 0.8)',
          color: '#6b7280'
        }}
        title="Zoom Out"
      >
        <span className="material-symbols-outlined text-xl">remove</span>
      </button>
      
      <button
        onClick={onFitToScreen}
        className="w-10 h-10 rounded-lg flex items-center justify-center transition-all hover:brightness-125"
        style={{ 
          backgroundColor: 'rgba(42, 48, 56, 0.6)',
          border: '1px solid rgba(42, 48, 56, 0.8)',
          color: '#6b7280'
        }}
        title="Fit to Screen"
      >
        <span className="material-symbols-outlined text-xl">fit_screen</span>
      </button>
    </div>
  );
}
