import { SourceFile, SyntaxKind, VariableDeclarationKind } from 'ts-morph';
import type { CodeAtom } from '../models/scan-result.js';
import { generateAtomId } from '../utils/id-generator.js';
import { filePathToNamespace } from '../utils/path-utils.js';

/**
 * Extracts top-level exported functions and arrow functions from a source file.
 */
export function extractFunctions(
  sourceFile: SourceFile,
  workspacePath: string,
  workspaceName: string
): CodeAtom[] {
  const atoms: CodeAtom[] = [];
  const filePath = sourceFile.getFilePath();
  const namespace = filePathToNamespace(filePath, workspacePath, workspaceName);

  // Regular function declarations
  for (const fn of sourceFile.getFunctions()) {
    const name = fn.getName();
    if (!name) continue;

    const params = fn
      .getParameters()
      .map((p) => `${p.getName()}: ${truncate(p.getType().getText())}`)
      .join(', ');
    const returnType = truncate(fn.getReturnType().getText());

    atoms.push({
      id: generateAtomId(namespace, name, 'method'),
      name,
      type: isComponentName(name) ? 'Component' : 'Method',
      namespace,
      filePath,
      lineNumber: fn.getStartLineNumber(),
      linesOfCode: fn.getEndLineNumber() - fn.getStartLineNumber() + 1,
      language: 'TypeScript',
      isPublic: fn.isExported(),
      signature: `function ${name}(${params}): ${returnType}`,
    });
  }

  // Arrow functions assigned to exported const variables
  // e.g., export const myHandler = (req: Request) => { ... }
  for (const stmt of sourceFile.getVariableStatements()) {
    if (!stmt.isExported()) continue;

    for (const decl of stmt.getDeclarations()) {
      const init = decl.getInitializer();
      if (!init) continue;

      // Check if initializer is an arrow function or function expression
      const kind = init.getKind();
      if (
        kind !== SyntaxKind.ArrowFunction &&
        kind !== SyntaxKind.FunctionExpression
      ) {
        continue;
      }

      const name = decl.getName();
      atoms.push({
        id: generateAtomId(namespace, name, 'method'),
        name,
        type: isComponentName(name) ? 'Component' : 'Method',
        namespace,
        filePath,
        lineNumber: decl.getStartLineNumber(),
        linesOfCode: decl.getEndLineNumber() - decl.getStartLineNumber() + 1,
        language: 'TypeScript',
        isPublic: true,
        signature: `const ${name}: ${truncate(decl.getType().getText())}`,
      });
    }
  }

  return atoms;
}

/**
 * Heuristic: PascalCase names returning JSX are likely React components.
 */
function isComponentName(name: string): boolean {
  return /^[A-Z][a-zA-Z0-9]*$/.test(name);
}

function truncate(text: string): string {
  return text.length > 100 ? text.substring(0, 97) + '...' : text;
}
