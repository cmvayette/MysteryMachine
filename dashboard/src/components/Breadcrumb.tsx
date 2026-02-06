import { useNavigationStore } from '../store/navigationStore';

interface BreadcrumbProps {
  className?: string;
}

export function Breadcrumb({ className = '' }: BreadcrumbProps) {
  const { level, path, drillUp, reset } = useNavigationStore();

  const levelLabels = {
  federation: 'Federation',
  repository: 'Repository',
  namespace: 'Namespace',
  component: 'Component',
  code: 'Code'
};

  return (
    <nav className={`flex items-center gap-2 text-sm ${className}`}>
      <button 
        onClick={reset}
        className="text-slate-400 hover:text-white transition-colors"
      >
        {levelLabels.federation}
      </button>
      
      {path.map((segment, index) => (
        <span key={index} className="flex items-center gap-2">
          <span className="text-slate-600">/</span>
          <button
            onClick={() => {
              // Drill up to this level
              const stepsBack = path.length - index - 1;
              for (let i = 0; i < stepsBack; i++) drillUp();
            }}
            className="text-slate-400 hover:text-white transition-colors truncate max-w-[200px]"
            title={segment}
          >
            {segment}
          </button>
        </span>
      ))}
      
      <span className="ml-auto px-2 py-0.5 rounded bg-slate-700 text-xs text-slate-300">
        {levelLabels[level]}
      </span>
    </nav>
  );
}
