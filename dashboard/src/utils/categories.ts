// Category definitions with detection patterns
export interface ModuleCategory {
  id: string;
  label: string;
  color: string;
  pattern: RegExp;
}

export const CATEGORIES: ModuleCategory[] = [
  { id: 'api', label: 'API Services', color: '#f97316', pattern: /\.(api|controllers?|endpoints?)\./i },
  { id: 'data', label: 'Data Access', color: '#3b82f6', pattern: /\.(data|repositor(y|ies)|persistence)\./i },
  { id: 'core', label: 'Core Logic', color: '#0dccf2', pattern: /\.(core|domain|engine|services?)\./i },
  { id: 'shared', label: 'Shared Models', color: '#a855f7', pattern: /\.(dto|models?|shared|common|contracts?)\./i },
  { id: 'infra', label: 'Infrastructure', color: '#64748b', pattern: /\.(infra|infrastructure|config|logging)\./i },
];

// Detect category from namespace path
export function detectCategory(namespacePath: string): string {
  for (const cat of CATEGORIES) {
    if (cat.pattern.test(namespacePath)) {
      return cat.id;
    }
  }
  return 'other';
}
