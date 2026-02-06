/**
 * Returns an HSL color string based on a risk score.
 * Scores map to: Critical (>=75) red, High (>=50) orange, Medium (>=25) yellow, Low (<25) green
 */
export function getRiskColor(score: number): string {
  if (score >= 75) return '#DC2626'; // red-600
  if (score >= 50) return '#EA580C'; // orange-600  
  if (score >= 25) return '#CA8A04'; // yellow-600
  return '#16A34A'; // green-600
}
