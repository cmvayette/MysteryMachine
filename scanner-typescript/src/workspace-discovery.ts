import * as fs from 'node:fs';
import * as path from 'node:path';
import type { WorkspaceInfo } from './models/scan-result.js';

/**
 * Discovers npm workspaces from a monorepo root's package.json.
 * Handles both explicit workspace arrays and glob patterns.
 */
export function discoverWorkspaces(rootPath: string): WorkspaceInfo[] {
  const pkgPath = path.join(rootPath, 'package.json');
  if (!fs.existsSync(pkgPath)) {
    return [];
  }

  const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf-8'));
  const workspacePatterns: string[] = pkg.workspaces ?? [];

  if (workspacePatterns.length === 0) {
    // Single project, not a monorepo — treat root as the only workspace
    return [
      {
        name: pkg.name ?? path.basename(rootPath),
        path: rootPath,
        relativePath: '.',
        workspaceDeps: [],
      },
    ];
  }

  // Resolve glob patterns to actual workspace directories
  const workspaceDirs: string[] = [];
  for (const pattern of workspacePatterns) {
    // npm workspace patterns like "apps/*" or "packages/*"
    const globPattern = path.join(rootPath, pattern);
    const resolved = resolveWorkspaceGlob(rootPath, pattern);
    workspaceDirs.push(...resolved);
  }

  // Read each workspace's package.json
  const allWorkspaceNames = new Set<string>();
  const workspaces: WorkspaceInfo[] = [];

  for (const wsDir of workspaceDirs) {
    const wsPkgPath = path.join(wsDir, 'package.json');
    if (!fs.existsSync(wsPkgPath)) continue;

    const wsPkg = JSON.parse(fs.readFileSync(wsPkgPath, 'utf-8'));
    const wsName = wsPkg.name ?? path.basename(wsDir);
    allWorkspaceNames.add(wsName);

    workspaces.push({
      name: wsName,
      path: wsDir,
      relativePath: path.relative(rootPath, wsDir),
      workspaceDeps: [], // Populated below
    });
  }

  // Resolve cross-workspace dependencies
  for (const ws of workspaces) {
    const wsPkgPath = path.join(ws.path, 'package.json');
    const wsPkg = JSON.parse(fs.readFileSync(wsPkgPath, 'utf-8'));
    const allDeps = {
      ...wsPkg.dependencies,
      ...wsPkg.devDependencies,
      ...wsPkg.peerDependencies,
    };
    ws.workspaceDeps = Object.keys(allDeps).filter((dep) =>
      allWorkspaceNames.has(dep)
    );
  }

  return workspaces;
}

/**
 * Resolves a workspace glob pattern (e.g., "apps/*") to actual directories.
 */
function resolveWorkspaceGlob(rootPath: string, pattern: string): string[] {
  // If pattern ends with /*, list subdirectories
  if (pattern.endsWith('/*') || pattern.endsWith('\\*')) {
    const parentDir = path.join(rootPath, pattern.slice(0, -2));
    if (!fs.existsSync(parentDir)) return [];
    return fs
      .readdirSync(parentDir, { withFileTypes: true })
      .filter((d) => d.isDirectory() && !d.name.startsWith('.'))
      .map((d) => path.join(parentDir, d.name))
      .filter((d) => fs.existsSync(path.join(d, 'package.json')));
  }

  // Exact path
  const exactPath = path.join(rootPath, pattern);
  if (fs.existsSync(exactPath) && fs.existsSync(path.join(exactPath, 'package.json'))) {
    return [exactPath];
  }
  return [];
}

/**
 * Detects domain directories within a workspace (e.g., src/domains/*).
 * These map to L3 (Component) in the C4 model.
 */
export function detectDomains(workspacePath: string): string[] {
  const candidates = [
    path.join(workspacePath, 'src', 'domains'),
    path.join(workspacePath, 'src', 'modules'),
    path.join(workspacePath, 'src', 'features'),
    path.join(workspacePath, 'src', 'lib'),
  ];

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) {
      return fs
        .readdirSync(candidate, { withFileTypes: true })
        .filter((d) => d.isDirectory() && !d.name.startsWith('.'))
        .map((d) => d.name);
    }
  }

  return [];
}
