export interface SystemGroup {
  name: string;
  repoIds: string[];
  repoCount: number;
}

/**
 * Infers "System" groupings from a list of repositories.
 * Uses the first segment of dot-notation as the System name.
 * e.g. "DiagnosticStructuralLens.Api" -> "DiagnosticStructuralLens"
 * "External.PaymentGateway" -> "External"
 * "Monolith" -> "Monolith"
 */
export function inferSystems(repos: { id: string; name: string }[]): SystemGroup[] {
  const groups = new Map<string, string[]>();

  repos.forEach(repo => {
    // Handle both clean names and legacy filesystem paths
    const cleanName = repo.name.includes('/') 
        ? repo.name.split('/').pop()! 
        : repo.name;
    const parts = cleanName.split('.');
    const systemName = parts[0];
    
    if (!groups.has(systemName)) {
      groups.set(systemName, []);
    }
    groups.get(systemName)!.push(repo.id);
  });

  const systems: SystemGroup[] = [];
  groups.forEach((ids, name) => {
    systems.push({
      name,
      repoIds: ids,
      repoCount: ids.length
    });
  });

  return systems.sort((a, b) => b.repoCount - a.repoCount); // Largest systems first
}
