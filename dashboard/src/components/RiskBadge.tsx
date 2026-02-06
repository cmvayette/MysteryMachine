interface RiskBadgeProps {
  score: number;
  size?: 'sm' | 'md' | 'lg';
}

export function RiskBadge({ score, size = 'md' }: RiskBadgeProps) {
  const getLevel = (score: number) => {
    if (score >= 75) return { label: 'Critical', class: 'bg-red-600', emoji: '🔴' };
    if (score >= 50) return { label: 'High', class: 'bg-orange-600', emoji: '🟠' };
    if (score >= 25) return { label: 'Medium', class: 'bg-yellow-600 text-black', emoji: '🟡' };
    return { label: 'Low', class: 'bg-green-600', emoji: '🟢' };
  };

  const level = getLevel(score);
  
  const sizeClasses = {
    sm: 'text-xs px-1.5 py-0.5',
    md: 'text-sm px-2 py-1',
    lg: 'text-base px-3 py-1.5'
  };

  return (
    <span className={`inline-flex items-center gap-1 rounded font-medium ${level.class} ${sizeClasses[size]}`}>
      <span>{level.emoji}</span>
      <span>{Math.round(score)}</span>
    </span>
  );
}
