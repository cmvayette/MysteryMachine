import { Breadcrumb } from './Breadcrumb';

interface HeaderProps {
  activeTab: string;
  onTabChange: (tab: string) => void;
  searchQuery: string;
  onSearchChange: (query: string) => void;
  activeHeatmap: string | null;
  onHeatmapChange: (mode: string | null) => void;
  showLinks: boolean;
  onToggleLinks: () => void;
}

export function Header({
  activeTab,
  onTabChange,
  searchQuery,
  onSearchChange,
  activeHeatmap,
  onHeatmapChange,
  showLinks,
  onToggleLinks,
}: HeaderProps) {
  return (
    <header className="h-16 border-b border-slate-700 bg-[#1e2026] flex items-center justify-between px-6 sticky top-0 z-30">
      <div className="flex items-center gap-4">
        <div className="flex items-center gap-2">
          <div className="w-8 h-8 rounded-[3px] bg-[#a09078] flex items-center justify-center">
            <span className="material-symbols-outlined text-white text-xl">hub</span>
          </div>
          <h1 className="text-xl font-bold text-slate-100 hidden md:block">
            Diagnostic Structural Lens
          </h1>
        </div>
        
        <div className="h-6 w-px bg-slate-700 mx-2" />
        
        {/* Navigation Tabs */}
        <div className="flex bg-[#18191f] rounded-[3px] p-1 border border-slate-700">
           {['Explorer', 'Architecture', 'Governance'].map(tab => (
              <button
                key={tab}
                onClick={() => onTabChange(tab)}
                className={`px-3 py-1 rounded-[3px] text-xs font-medium transition-all ${
                   activeTab === tab 
                   ? 'bg-slate-700 text-white' 
                   : 'text-slate-400 hover:text-slate-200'
                }`}
              >
                {tab}
              </button>
           ))}
        </div>

        <div className="h-6 w-px bg-slate-700 mx-2" />
        <Breadcrumb />
      </div>

      <div className="flex items-center gap-4">
        {/* Heatmap Toggles */}
        <div className="flex items-center gap-2 mr-4">
            <span className="text-xs text-slate-500 uppercase tracking-wider">Lens</span>
            <div className="flex bg-[#18191f] rounded-[3px] p-0.5 border border-slate-700">
                {[
                    { id: null, icon: 'layers', label: 'Structure' },
                    { id: 'churn', icon: 'local_fire_department', label: 'Churn' },
                    { id: 'cost', icon: 'monetization_on', label: 'Cost' },
                    { id: 'risk', icon: 'shield', label: 'Risk' }
                ].map(mode => (
                    <button
                        key={mode.id || 'default'}
                        onClick={() => onHeatmapChange(mode.id)}
                        title={mode.label}
                        className={`w-8 h-8 flex items-center justify-center rounded-[3px] transition-colors ${
                            activeHeatmap === mode.id
                            ? 'bg-[#a09078] text-white'
                            : 'text-slate-400 hover:text-white hover:bg-slate-700'
                        }`}
                    >
                        <span className="material-symbols-outlined text-sm">{mode.icon}</span>
                    </button>
                ))}
            </div>
        </div>

        {/* View Options */}
        <button
            onClick={onToggleLinks}
            className={`p-2 rounded-[3px] transition-colors ${
              showLinks ? 'text-[#c4a882] bg-[#2a2520]' : 'text-slate-400 hover:bg-slate-800'
            }`}
            title="Toggle Links"
          >
            <span className="material-symbols-outlined">dataset_linked</span>
        </button>

        {/* Search */}
        <div className="relative">
          <span className="material-symbols-outlined absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-400 text-sm">search</span>
          <input 
            type="text" 
            placeholder="Search..." 
            value={searchQuery}
            onChange={(e) => onSearchChange(e.target.value)}
            className="bg-[#18191f] border border-slate-700 rounded-[3px] pl-9 pr-4 py-1.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-[#a09078] focus:ring-1 focus:ring-[#a09078] w-48 transition-all focus:w-64"
          />
        </div>
      </div>
    </header>
  );
}
