import { Project, SourceFile } from 'ts-morph';
import * as path from 'node:path';
import * as fs from 'node:fs';
import type {
  CodeAtom,
  AtomLink,
  ScanDiagnostic,
  ScanResult,
  Snapshot,
  WorkspaceInfo,
} from './models/scan-result.js';
import { discoverWorkspaces, detectDomains } from './workspace-discovery.js';
import { extractTypes } from './extractors/type-extractor.js';
import { extractFunctions } from './extractors/function-extractor.js';
import { extractImports } from './extractors/import-extractor.js';
import { extractGraphQL } from './extractors/graphql-extractor.js';
import { generateAtomId, generateLinkId } from './utils/id-generator.js';
import { isTestFile } from './utils/path-utils.js';

export interface ScanOptions {
  /** Root path of the repository to scan */
  repoPath: string;
  /** Include test files in scan */
  includeTests?: boolean;
  /** File extension patterns to include */
  extensions?: string[];
  /** Directories to exclude */
  excludeDirs?: string[];
}

const DEFAULT_EXTENSIONS = ['.ts', '.tsx'];
const DEFAULT_EXCLUDE = [
  'node_modules',
  'dist',
  'build',
  '.git',
  'coverage',
  '.next',
  '.nuxt',
];

/**
 * Main scanner — orchestrates workspace discovery, AST extraction, and link generation.
 */
export async function scan(options: ScanOptions): Promise<ScanResult> {
  const startTime = Date.now();
  const allAtoms: CodeAtom[] = [];
  const allLinks: AtomLink[] = [];
  const diagnostics: ScanDiagnostic[] = [];

  const extensions = options.extensions ?? DEFAULT_EXTENSIONS;
  const excludeDirs = options.excludeDirs ?? DEFAULT_EXCLUDE;

  console.log(`🔍 Scanning: ${options.repoPath}`);

  // 1. Discover workspaces
  const workspaces = discoverWorkspaces(options.repoPath);
  console.log(`📦 Found ${workspaces.length} workspace(s)`);

  for (const ws of workspaces) {
    console.log(`   - ${ws.name} (${ws.relativePath})`);
  }

  // 2. Scan each workspace
  for (const ws of workspaces) {
    try {
      const wsResult = await scanWorkspace(ws, extensions, excludeDirs, options.includeTests ?? false);
      allAtoms.push(...wsResult.atoms);
      allLinks.push(...wsResult.links);
      console.log(
        `   ✅ ${ws.name}: ${wsResult.atoms.length} atoms, ${wsResult.links.length} links`
      );
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      diagnostics.push({
        severity: 'Warning',
        message: `Failed to scan workspace ${ws.name}: ${msg}`,
      });
      console.error(`   ❌ ${ws.name}: ${msg}`);
    }
  }

  // 3. Generate cross-workspace dependency links
  const wsDepLinks = generateWorkspaceDependencyLinks(workspaces);
  allLinks.push(...wsDepLinks);

  const durationMs = Date.now() - startTime;
  const duration = formatDuration(durationMs);

  console.log(`\n📊 Scan complete: ${allAtoms.length} atoms, ${allLinks.length} links in ${durationMs}ms`);

  return {
    codeAtoms: allAtoms,
    sqlAtoms: [], // TS scanner doesn't produce SQL atoms
    links: allLinks,
    diagnostics,
    duration,
  };
}

/**
 * Scans a single workspace using ts-morph.
 */
async function scanWorkspace(
  workspace: WorkspaceInfo,
  extensions: string[],
  excludeDirs: string[],
  includeTests: boolean
): Promise<{ atoms: CodeAtom[]; links: AtomLink[] }> {
  const atoms: CodeAtom[] = [];
  const links: AtomLink[] = [];

  // Try to use the workspace's tsconfig if it exists
  const tsconfigPath = findTsConfig(workspace.path);

  let project: Project;
  if (tsconfigPath) {
    project = new Project({
      tsConfigFilePath: tsconfigPath,
      skipAddingFilesFromTsConfig: true, // We'll add files manually to control scope
    });
  } else {
    project = new Project({
      compilerOptions: {
        target: 99, // ESNext
        module: 99, // ESNext
        strict: true,
        skipLibCheck: true,
      },
    });
  }

  // Find all TypeScript files in the workspace
  const files = findTypeScriptFiles(workspace.path, extensions, excludeDirs, includeTests);

  // Add files to the project
  for (const file of files) {
    project.addSourceFileAtPath(file);
  }

  // Extract atoms and links from each source file
  for (const sourceFile of project.getSourceFiles()) {
    const filePath = sourceFile.getFilePath();

    // Skip files outside our workspace
    if (!filePath.startsWith(workspace.path)) continue;

    try {
      // Extract types (classes, interfaces, enums, type aliases)
      const typeAtoms = extractTypes(sourceFile, workspace.path, workspace.name);
      atoms.push(...typeAtoms);

      // Extract functions
      const fnAtoms = extractFunctions(sourceFile, workspace.path, workspace.name);
      atoms.push(...fnAtoms);

      // Extract import/export relationships
      const importLinks = extractImports(sourceFile, workspace.path, workspace.name);
      links.push(...importLinks);

      // Extract GraphQL schema definitions (Pothos patterns)
      const gqlResult = extractGraphQL(sourceFile, workspace.path, workspace.name);
      atoms.push(...gqlResult.atoms);
      links.push(...gqlResult.links);
    } catch (error) {
      // Skip files that cause extraction errors
      const msg = error instanceof Error ? error.message : String(error);
      console.warn(`   ⚠️ Skipping ${path.basename(filePath)}: ${msg}`);
    }
  }

  // Detect domains and create module atoms for them
  const domains = detectDomains(workspace.path);
  for (const domain of domains) {
    const namespace = `${workspace.name}::domains.${domain}`;
    atoms.push({
      id: generateAtomId(namespace, domain, 'module'),
      name: domain,
      type: 'Module',
      namespace,
      language: 'TypeScript',
      isPublic: true,
    });
  }

  return { atoms, links };
}

/**
 * Generates WorkspaceDependency links from workspace cross-references.
 */
function generateWorkspaceDependencyLinks(workspaces: WorkspaceInfo[]): AtomLink[] {
  const links: AtomLink[] = [];

  for (const ws of workspaces) {
    for (const depName of ws.workspaceDeps) {
      const dep = workspaces.find((w) => w.name === depName);
      if (!dep) continue;

      const sourceId = generateAtomId(ws.name, ws.name, 'module');
      const targetId = generateAtomId(dep.name, dep.name, 'module');

      links.push({
        id: generateLinkId(sourceId, targetId, 'WorkspaceDependency'),
        sourceId,
        targetId,
        type: 'WorkspaceDependency',
        confidence: 1.0,
        evidence: `${ws.name} depends on ${dep.name} via package.json`,
      });
    }
  }

  return links;
}

/**
 * Recursively finds TypeScript files in a directory.
 */
function findTypeScriptFiles(
  dir: string,
  extensions: string[],
  excludeDirs: string[],
  includeTests: boolean
): string[] {
  const files: string[] = [];

  function walk(currentDir: string) {
    let entries: fs.Dirent[];
    try {
      entries = fs.readdirSync(currentDir, { withFileTypes: true });
    } catch {
      return;
    }

    for (const entry of entries) {
      if (entry.name.startsWith('.')) continue;
      const fullPath = path.join(currentDir, entry.name);

      if (entry.isDirectory()) {
        if (excludeDirs.includes(entry.name)) continue;
        walk(fullPath);
      } else if (entry.isFile()) {
        const ext = path.extname(entry.name);
        if (!extensions.includes(ext)) continue;
        if (!includeTests && isTestFile(fullPath)) continue;

        // Skip declaration files
        if (entry.name.endsWith('.d.ts')) continue;

        files.push(fullPath);
      }
    }
  }

  walk(dir);
  return files;
}

/**
 * Find tsconfig.json in a workspace directory.
 */
function findTsConfig(dir: string): string | undefined {
  const candidates = ['tsconfig.json', 'tsconfig.app.json'];
  for (const name of candidates) {
    const p = path.join(dir, name);
    if (fs.existsSync(p)) return p;
  }
  return undefined;
}

/**
 * Formats milliseconds as a TimeSpan-compatible string.
 */
function formatDuration(ms: number): string {
  const seconds = Math.floor(ms / 1000);
  const minutes = Math.floor(seconds / 60);
  const hours = Math.floor(minutes / 60);
  const remainMs = ms % 1000;
  return `${String(hours).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}.${String(remainMs).padStart(3, '0')}`;
}

/**
 * Builds a full Snapshot from a ScanResult (convenience for CLI).
 */
export function buildSnapshot(
  result: ScanResult,
  repoPath: string,
  branch?: string,
  commitSha?: string
): Snapshot {
  return {
    id: crypto.randomUUID().replace(/-/g, '').substring(0, 8),
    repository: path.basename(repoPath),
    scannedAt: new Date().toISOString(),
    branch,
    commitSha,
    codeAtoms: result.codeAtoms,
    sqlAtoms: result.sqlAtoms,
    links: result.links,
    metadata: {
      totalCodeAtoms: result.codeAtoms.length,
      totalSqlAtoms: result.sqlAtoms.length,
      totalLinks: result.links.length,
      dtoCount: 0,
      interfaceCount: result.codeAtoms.filter((a) => a.type === 'Interface').length,
      tableCount: 0,
      storedProcedureCount: 0,
      typeAliasCount: result.codeAtoms.filter((a) => a.type === 'TypeAlias').length,
      moduleCount: result.codeAtoms.filter((a) => a.type === 'Module').length,
      componentCount: result.codeAtoms.filter((a) => a.type === 'Component').length,
      scanDuration: result.duration,
    },
  };
}
