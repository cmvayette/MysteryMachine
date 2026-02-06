interface ZoomControlsProps {
  onZoomIn: () => void;
  onZoomOut: () => void;
  onFitToScreen: () => void;
  orientation?: 'horizontal' | 'vertical';
}

export function ZoomControls({ onZoomIn, onZoomOut, onFitToScreen, orientation = 'horizontal' }: ZoomControlsProps) {
  const isVertical = orientation === 'vertical';
  
  return (
    <div className={`flex ${isVertical ? 'flex-col' : 'items-center'} gap-1`}>
      <button
        onClick={onZoomIn}
        className="w-8 h-8 rounded-lg flex items-center justify-center transition-all hover:bg-slate-700/50"
        style={{ color: '#6b7280' }}
        title="Zoom In"
      >
        <span className="material-symbols-outlined text-lg">add</span>
      </button>
      
      <button
        onClick={onZoomOut}
        className="w-8 h-8 rounded-lg flex items-center justify-center transition-all hover:bg-slate-700/50"
        style={{ color: '#6b7280' }}
        title="Zoom Out"
      >
        <span className="material-symbols-outlined text-lg">remove</span>
      </button>
      
      <button
        onClick={onFitToScreen}
        className="w-8 h-8 rounded-lg flex items-center justify-center transition-all hover:bg-slate-700/50"
        style={{ color: '#6b7280' }}
        title="Fit to Screen"
      >
        <span className="material-symbols-outlined text-lg">fit_screen</span>
      </button>
    </div>
  );
}
