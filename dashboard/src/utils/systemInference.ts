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
    const parts = repo.name.split('.');
    // Use first part as system name, or full name if no dots
    // Special case: if it starts with "DiagnosticStructuralLens", treat that as the system
    const systemName = parts[0];
    
    // Heuristic: If there are diverse repos, group them.
    // If a repo has no dots, it's its own system? Or "Other"? 
    // Let's use the first segment for now.
    
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
