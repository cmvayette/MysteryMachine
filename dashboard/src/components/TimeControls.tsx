import { useEffect } from 'react';

// Snapshot summary type from GraphQL
export interface SnapshotSummary {
  id: string;
  scannedAt: string;
  branch: string | null;
  atomCount: number;
}

interface TimeControlsProps {
  snapshots: SnapshotSummary[];
  currentSnapshotId: string | null;
  isPlaying: boolean;
  onSnapshotChange: (id: string | null) => void;
  onTogglePlay: () => void;
}

export function TimeControls({ 
  snapshots, 
  currentSnapshotId, 
  isPlaying, 
  onSnapshotChange, 
  onTogglePlay 
}: TimeControlsProps) {
  // Sort snapshots by date
  const sortedSnapshots = [...snapshots].sort((a, b) => 
    new Date(a.scannedAt).getTime() - new Date(b.scannedAt).getTime()
  );

  const currentIndex = sortedSnapshots.findIndex(s => s.id === currentSnapshotId);
  const total = sortedSnapshots.length;

  // Auto-play logic
  useEffect(() => {
    let interval: number;
    if (isPlaying && total > 0) {
      interval = window.setInterval(() => {
        const nextIndex = (currentIndex + 1) % total;
        onSnapshotChange(sortedSnapshots[nextIndex].id);
      }, 2000); // 2 seconds per frame
    }
    return () => clearInterval(interval);
  }, [isPlaying, currentIndex, total, sortedSnapshots, onSnapshotChange]);

  if (snapshots.length === 0) return null;

  const currentSnapshot = sortedSnapshots[currentIndex];
  
  return (
    <div className="fixed bottom-8 left-1/2 -translate-x-1/2 bg-[#1e2026] border border-slate-700 rounded-[3px] p-4 z-50 flex flex-col gap-3 w-[600px]">
      
      {/* Header Info */}
      <div className="flex justify-between items-center text-xs text-slate-400 font-mono">
        <div>
           {currentIndex + 1} / {total}
        </div>
        <div className="text-center">
            {currentSnapshot ? (
                <>
                  <span className="text-white font-bold">
                    {new Date(currentSnapshot.scannedAt).toLocaleDateString()} 
                  </span>
                  <span className="mx-2 text-slate-600">|</span>
                  <span className="text-[#c4a882]">{currentSnapshot.branch || 'HEAD'}</span>
                  <span className="mx-2 text-slate-600">|</span>
                  <span>{currentSnapshot.atomCount} atoms</span>
                </>
            ) : 'Select Snapshot'}
        </div>
        <div>
           {/* Placeholder for speed control */}
           1x
        </div>
      </div>

      {/* Controls & Scrubber */}
      <div className="flex items-center gap-4">
        <button 
          onClick={onTogglePlay}
          className={`w-10 h-10 flex items-center justify-center rounded-full transition-all ${
            isPlaying ? 'bg-[#302020] text-[#b45454] hover:bg-[#3a2525]' : 'bg-[#2a2520] text-[#c4a882] hover:bg-[#3a3530]'
          }`}
        >
          <span className="material-symbols-outlined text-xl">
            {isPlaying ? 'pause' : 'play_arrow'}
          </span>
        </button>

        {/* Scrubber Range */}
        <div className="flex-1 relative group">
           {/* Track */}
           <div className="absolute top-1/2 left-0 right-0 h-1 bg-slate-700 rounded-full -translate-y-1/2"></div>
           
           {/* Progress */}
           <div
              className="absolute top-1/2 left-0 h-1 bg-[#a09078] rounded-full -translate-y-1/2 transition-all duration-300"
             style={{ width: `${total > 1 ? (currentIndex / (total - 1)) * 100 : 0}%` }}
           ></div>

           <input 
             type="range" 
             min="0" 
             max={total - 1} 
             value={currentIndex === -1 ? 0 : currentIndex}
             onChange={(e) => {
               const idx = parseInt(e.target.value);
               onSnapshotChange(sortedSnapshots[idx].id);
             }}
             className="w-full h-8 opacity-0 cursor-pointer relative z-10"
           />
           
           {/* Thumb (Visual Only, follows logic) */}
           <div
              className="absolute top-1/2 w-4 h-4 bg-[#c4a882] rounded-full -translate-y-1/2 -ml-2 pointer-events-none transition-all duration-300 border-2 border-[#18191f]"
              style={{ left: `${total > 1 ? (currentIndex / (total - 1)) * 100 : 0}%` }}
           ></div>
        </div>
      </div>
    </div>
  );
}
