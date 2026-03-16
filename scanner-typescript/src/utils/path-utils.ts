import * as path from 'node:path';

/**
 * Converts an absolute file path to a namespace-like string.
 * Uses :: to separate workspace name from subdirectory path.
 * e.g., /foo/bar/apps/som-tier0/src/domains/governance/drift-manager.ts
 *   → semantic-operating-model::domains.governance
 *
 * The :: separator lets the dashboard distinguish workspace (container)
 * from subdirectory (component) — unlike C#'s dotted namespaces which
 * use . for both project and namespace levels.
 */
export function filePathToNamespace(
  filePath: string,
  workspacePath: string,
  workspaceName: string
): string {
  const rel = path.relative(workspacePath, filePath);
  const dir = path.dirname(rel);
  if (dir === '.') return workspaceName;

  // Remove src/ prefix if present
  const cleaned = dir.replace(/^src[/\\]?/, '');
  if (!cleaned) return workspaceName;

  return `${workspaceName}::${cleaned.replace(/[/\\]/g, '.')}`;
}

/**
 * Determines if a file is a barrel/index file.
 */
export function isBarrelFile(filePath: string): boolean {
  const base = path.basename(filePath, path.extname(filePath));
  return base === 'index';
}

/**
 * Determines if a file is a test file.
 */
export function isTestFile(filePath: string): boolean {
  const base = path.basename(filePath);
  return (
    base.includes('.test.') ||
    base.includes('.spec.') ||
    base.includes('__tests__')
  );
}
