
import { CATEGORIES } from '../utils/categories';

interface ModuleFilterProps {
  level: string;
  visibleCategories: Set<string>;
  onToggleCategory: (categoryId: string) => void;
  onSelectAll: () => void;
  onSelectNone: () => void;
  categoryCounts?: Map<string, number>;
}

export function ModuleFilter({ 
  level,
  visibleCategories, 
  onToggleCategory, 
  onSelectAll, 
  onSelectNone,
  categoryCounts 
}: ModuleFilterProps) {
  // Only show at namespace level (when viewing atoms)
  if (level !== 'container' && level !== 'component') {
    return null;
  }

  return (
    <div 
      className="panel"
      style={{ 
        position: 'absolute',
        top: '16px',
        right: '16px',
        zIndex: 10,
        minWidth: '180px'
      }}
    >
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-medium text-slate-300">Filter Modules</h3>
        <div className="flex gap-1">
          <button
            onClick={onSelectAll}
            className="text-xs px-1.5 py-0.5 rounded hover:bg-slate-700"
            style={{ color: 'var(--color-primary)' }}
            title="Select All"
          >
            All
          </button>
          <button
            onClick={onSelectNone}
            className="text-xs px-1.5 py-0.5 rounded hover:bg-slate-700 text-slate-400"
            title="Select None"
          >
            None
          </button>
        </div>
      </div>
      
      <div className="space-y-2">
        {CATEGORIES.map(cat => {
          const count = categoryCounts?.get(cat.id) ?? 0;
          const isVisible = visibleCategories.has(cat.id);
          
          return (
            <label
              key={cat.id}
              className="flex items-center gap-2 cursor-pointer group"
            >
              <input
                type="checkbox"
                checked={isVisible}
                onChange={() => onToggleCategory(cat.id)}
                className="sr-only"
              />
              <span 
                className="w-3 h-3 rounded-sm border-2 flex items-center justify-center transition-all"
                style={{ 
                  borderColor: cat.color,
                  backgroundColor: isVisible ? cat.color : 'transparent'
                }}
              >
                {isVisible && (
                  <span className="material-symbols-outlined text-xs text-white" style={{ fontSize: '10px' }}>
                    check
                  </span>
                )}
              </span>
              <span 
                className={`text-xs flex-1 ${isVisible ? 'text-white' : 'text-slate-500'}`}
              >
                {cat.label}
              </span>
              {count > 0 && (
                <span 
                  className="text-xs tabular-nums"
                  style={{ color: isVisible ? cat.color : '#475569' }}
                >
                  {count}
                </span>
              )}
            </label>
          );
        })}
        
        {/* "Other" category for unmatched */}
        <label className="flex items-center gap-2 cursor-pointer group">
          <input
            type="checkbox"
            checked={visibleCategories.has('other')}
            onChange={() => onToggleCategory('other')}
            className="sr-only"
          />
          <span 
            className="w-3 h-3 rounded-sm border-2 flex items-center justify-center transition-all"
            style={{ 
              borderColor: '#475569',
              backgroundColor: visibleCategories.has('other') ? '#475569' : 'transparent'
            }}
          >
            {visibleCategories.has('other') && (
              <span className="material-symbols-outlined text-white" style={{ fontSize: '10px' }}>
                check
              </span>
            )}
          </span>
          <span 
            className={`text-xs flex-1 ${visibleCategories.has('other') ? 'text-white' : 'text-slate-500'}`}
          >
            Other
          </span>
          {(categoryCounts?.get('other') ?? 0) > 0 && (
            <span 
              className="text-xs tabular-nums"
              style={{ color: visibleCategories.has('other') ? '#475569' : '#334155' }}
            >
              {categoryCounts?.get('other') ?? 0}
            </span>
          )}
        </label>
      </div>
    </div>
  );
}
