import { SourceFile } from 'ts-morph';
import type { AtomLink } from '../models/scan-result.js';
import { generateAtomId, generateLinkId } from '../utils/id-generator.js';
import { filePathToNamespace, isBarrelFile } from '../utils/path-utils.js';
import * as path from 'node:path';

/**
 * Extracts import/export relationships from a source file as AtomLinks.
 */
export function extractImports(
  sourceFile: SourceFile,
  workspacePath: string,
  workspaceName: string,
  /** Map of module specifier → resolved file path (for relative imports) */
  resolveModule?: (specifier: string, fromFile: string) => string | undefined
): AtomLink[] {
  const links: AtomLink[] = [];
  const filePath = sourceFile.getFilePath();
  const sourceNamespace = filePathToNamespace(filePath, workspacePath, workspaceName);

  // Process import declarations
  for (const imp of sourceFile.getImportDeclarations()) {
    const moduleSpecifier = imp.getModuleSpecifierValue();

    // Skip node_modules / external packages (not workspace deps; those are handled separately)
    if (!moduleSpecifier.startsWith('.') && !moduleSpecifier.startsWith('/')) {
      continue;
    }

    // Resolve the import to an actual file path
    const resolvedPath = resolveModule
      ? resolveModule(moduleSpecifier, filePath)
      : resolveRelativeImport(moduleSpecifier, filePath);

    if (!resolvedPath) continue;

    const targetNamespace = filePathToNamespace(resolvedPath, workspacePath, workspaceName);

    // Create a link for each named import
    const namedImports = imp.getNamedImports();
    if (namedImports.length > 0) {
      for (const named of namedImports) {
        const importedName = named.getName();
        // We don't know the exact type of the imported symbol, so use a generic approach
        const sourceId = generateAtomId(sourceNamespace, path.basename(filePath, path.extname(filePath)), 'module');
        const targetId = generateAtomId(targetNamespace, importedName, 'class'); // Best guess

        links.push({
          id: generateLinkId(sourceId, targetId, 'Imports'),
          sourceId,
          targetId,
          type: 'Imports',
          confidence: 0.8,
          evidence: `import { ${importedName} } from '${moduleSpecifier}'`,
        });
      }
    }

    // Default import
    const defaultImport = imp.getDefaultImport();
    if (defaultImport) {
      const sourceId = generateAtomId(sourceNamespace, path.basename(filePath, path.extname(filePath)), 'module');
      const targetId = generateAtomId(targetNamespace, defaultImport.getText(), 'class');

      links.push({
        id: generateLinkId(sourceId, targetId, 'Imports'),
        sourceId,
        targetId,
        type: 'Imports',
        confidence: 0.7,
        evidence: `import ${defaultImport.getText()} from '${moduleSpecifier}'`,
      });
    }

    // Namespace import (import * as X from ...)
    const namespaceImport = imp.getNamespaceImport();
    if (namespaceImport) {
      const sourceId = generateAtomId(sourceNamespace, path.basename(filePath, path.extname(filePath)), 'module');
      const targetId = generateAtomId(targetNamespace, path.basename(resolvedPath, path.extname(resolvedPath)), 'module');

      links.push({
        id: generateLinkId(sourceId, targetId, 'Imports'),
        sourceId,
        targetId,
        type: 'Imports',
        confidence: 0.9,
        evidence: `import * as ${namespaceImport.getText()} from '${moduleSpecifier}'`,
      });
    }
  }

  // Process re-exports (barrel files)
  for (const exp of sourceFile.getExportDeclarations()) {
    const moduleSpecifier = exp.getModuleSpecifierValue();
    if (!moduleSpecifier) continue;

    const resolvedPath = resolveModule
      ? resolveModule(moduleSpecifier, filePath)
      : resolveRelativeImport(moduleSpecifier, filePath);

    if (!resolvedPath) continue;

    const targetNamespace = filePathToNamespace(resolvedPath, workspacePath, workspaceName);

    const namedExports = exp.getNamedExports();
    if (namedExports.length > 0) {
      for (const named of namedExports) {
        const exportedName = named.getName();
        const sourceId = generateAtomId(sourceNamespace, path.basename(filePath, path.extname(filePath)), 'module');
        const targetId = generateAtomId(targetNamespace, exportedName, 'class');

        links.push({
          id: generateLinkId(sourceId, targetId, 'ReExports'),
          sourceId,
          targetId,
          type: 'ReExports',
          confidence: 1.0,
          evidence: `export { ${exportedName} } from '${moduleSpecifier}'`,
        });
      }
    } else {
      // export * from '...'
      const sourceId = generateAtomId(sourceNamespace, path.basename(filePath, path.extname(filePath)), 'module');
      const targetId = generateAtomId(targetNamespace, path.basename(resolvedPath, path.extname(resolvedPath)), 'module');

      links.push({
        id: generateLinkId(sourceId, targetId, 'ReExports'),
        sourceId,
        targetId,
        type: 'ReExports',
        confidence: 1.0,
        evidence: `export * from '${moduleSpecifier}'`,
      });
    }
  }

  return links;
}

/**
 * Simple relative import resolution.
 */
function resolveRelativeImport(
  specifier: string,
  fromFile: string
): string | undefined {
  const dir = path.dirname(fromFile);
  const candidates = [
    path.resolve(dir, `${specifier}.ts`),
    path.resolve(dir, `${specifier}.tsx`),
    path.resolve(dir, specifier, 'index.ts'),
    path.resolve(dir, specifier, 'index.tsx'),
  ];

  // We don't check fs here — in production the ts-morph project resolves these
  // Return the first candidate (ts-morph will validate existence)
  return candidates[0];
}
