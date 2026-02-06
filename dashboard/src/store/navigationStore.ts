import { create } from 'zustand';

export type C4Level = 'context' | 'container' | 'component' | 'code';

export interface NavigationState {
  level: C4Level;
  path: string[]; // e.g., ["RepoA", "Company.Orders", "OrderDTO"]
  selectedAtomId: string | null;
  blastRadiusMode: boolean;
  diffMode: boolean;
  baselineSnapshot: string | null;
  
  // Actions
  drillDown: (target: string) => void;
  drillUp: () => void;
  selectAtom: (atomId: string | null) => void;
  toggleBlastRadiusMode: () => void;
  toggleDiffMode: () => void;
  setBaseline: (snapshotId: string | null) => void;
  reset: () => void;
}

const levelProgression: C4Level[] = ['context', 'container', 'component', 'code'];

export const useNavigationStore = create<NavigationState>((set) => ({
  level: 'context',
  path: [],
  selectedAtomId: null,
  blastRadiusMode: false,
  diffMode: false,
  baselineSnapshot: null,

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
  setBaseline: (id) => set({ baselineSnapshot: id }),
  
  reset: () => set({
    level: 'context',
    path: [],
    selectedAtomId: null,
    blastRadiusMode: false,
    diffMode: false,
    baselineSnapshot: null
  })
}));

