import { create } from 'zustand';

// Define locally or move to types file
interface SnapshotSummary {
  id: string;
  scannedAt: string;
  branch: string | null;
  atomCount: number;
}

export type C4Level = 'federation' | 'repository' | 'namespace' | 'component' | 'code';

export interface NavigationState {
  level: C4Level;
  path: string[]; // e.g., ["RepoA", "Company.Orders", "OrderDTO"]
  selectedAtomId: string | null;
  blastRadiusMode: boolean;
  diffMode: boolean;
  governanceMode: boolean;
  baselineSnapshot: string | null;
  snapshots: SnapshotSummary[]; // Using any to avoid circular deps for now, or define in type
  currentSnapshotId: string | null;
  isPlaying: boolean;
  
  // Actions
  drillDown: (target: string) => void;
  drillUp: () => void;
  selectAtom: (atomId: string | null) => void;
  toggleBlastRadiusMode: () => void;
  toggleDiffMode: () => void;
  toggleGovernanceMode: () => void;
  setBaseline: (snapshotId: string | null) => void;
  setSnapshots: (snapshots: SnapshotSummary[]) => void;
  setCurrentSnapshot: (id: string | null) => void;
  setIsPlaying: (isPlaying: boolean) => void;
  reset: () => void;
}

const levelProgression: C4Level[] = ['federation', 'repository', 'namespace', 'component', 'code'];

export const useNavigationStore = create<NavigationState>((set) => ({
  level: 'federation',
  path: [],
  selectedAtomId: null,
  blastRadiusMode: false,
  diffMode: false,
  governanceMode: false,
  baselineSnapshot: null,
  snapshots: [],
  currentSnapshotId: null,
  isPlaying: false,

  drillDown: (target) => set((state) => {
    const currentIndex = levelProgression.indexOf(state.level);
    if (currentIndex === levelProgression.length - 1) return state;
    
    return {
      level: levelProgression[currentIndex + 1],
      path: [...state.path, target],
      selectedAtomId: null
    };
  }),

  drillUp: () => set((state) => {
    const currentIndex = levelProgression.indexOf(state.level);
    if (currentIndex === 0) return state;
    
    return {
      level: levelProgression[currentIndex - 1],
      path: state.path.slice(0, -1),
      selectedAtomId: null
    };
  }),

  selectAtom: (atomId) => set({ selectedAtomId: atomId }),
  
  toggleBlastRadiusMode: () => set((state) => ({ 
    blastRadiusMode: !state.blastRadiusMode 
  })),

  toggleDiffMode: () => set(state => ({ diffMode: !state.diffMode })),
  toggleGovernanceMode: () => set((state) => ({ governanceMode: !state.governanceMode })),
  
  setBaseline: (id) => set({ baselineSnapshot: id }),
  setSnapshots: (snapshots) => set({ snapshots }),
  setCurrentSnapshot: (id) => set({ currentSnapshotId: id }),
  setIsPlaying: (isPlaying) => set({ isPlaying }),
  
  reset: () => set({
    level: 'federation',
    path: [],
    selectedAtomId: null,
    blastRadiusMode: false,
    diffMode: false,
    baselineSnapshot: null
  })
}));

