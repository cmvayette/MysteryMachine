import { SourceFile, SyntaxKind, Node } from 'ts-morph';
import type { CodeAtom, AtomType } from '../models/scan-result.js';
import { generateAtomId } from '../utils/id-generator.js';
import { filePathToNamespace } from '../utils/path-utils.js';

/**
 * Extracts classes, interfaces, enums, and type aliases from a source file.
 */
export function extractTypes(
  sourceFile: SourceFile,
  workspacePath: string,
  workspaceName: string
): CodeAtom[] {
  const atoms: CodeAtom[] = [];
  const filePath = sourceFile.getFilePath();
  const namespace = filePathToNamespace(filePath, workspacePath, workspaceName);

  // Classes
  for (const cls of sourceFile.getClasses()) {
    const name = cls.getName();
    if (!name) continue;

    const isExported = cls.isExported();
    atoms.push({
      id: generateAtomId(namespace, name, 'class'),
      name,
      type: classifyClass(cls),
      namespace,
      filePath,
      lineNumber: cls.getStartLineNumber(),
      linesOfCode: cls.getEndLineNumber() - cls.getStartLineNumber() + 1,
      language: 'TypeScript',
      isPublic: isExported,
      signature: buildClassSignature(cls),
    });

    // Extract methods as child atoms
    for (const method of cls.getMethods()) {
      const methodName = method.getName();
      const methodId = generateAtomId(namespace, `${name}.${methodName}`, 'method');
      atoms.push({
        id: methodId,
        name: `${name}.${methodName}`,
        type: 'Method',
        namespace,
        filePath,
        lineNumber: method.getStartLineNumber(),
        linesOfCode: method.getEndLineNumber() - method.getStartLineNumber() + 1,
        language: 'TypeScript',
        isPublic: isExported,
        signature: buildMethodSignature(method),
      });
    }

    // Extract properties
    for (const prop of cls.getProperties()) {
      const propName = prop.getName();
      atoms.push({
        id: generateAtomId(namespace, `${name}.${propName}`, 'property'),
        name: `${name}.${propName}`,
        type: 'Property',
        namespace,
        filePath,
        lineNumber: prop.getStartLineNumber(),
        language: 'TypeScript',
        isPublic: isExported,
      });
    }
  }

  // Interfaces
  for (const iface of sourceFile.getInterfaces()) {
    const name = iface.getName();
    atoms.push({
      id: generateAtomId(namespace, name, 'interface'),
      name,
      type: 'Interface',
      namespace,
      filePath,
      lineNumber: iface.getStartLineNumber(),
      linesOfCode: iface.getEndLineNumber() - iface.getStartLineNumber() + 1,
      language: 'TypeScript',
      isPublic: iface.isExported(),
      signature: `interface ${name}`,
    });
  }

  // Enums
  for (const en of sourceFile.getEnums()) {
    const name = en.getName();
    atoms.push({
      id: generateAtomId(namespace, name, 'enum'),
      name,
      type: 'Enum',
      namespace,
      filePath,
      lineNumber: en.getStartLineNumber(),
      linesOfCode: en.getEndLineNumber() - en.getStartLineNumber() + 1,
      language: 'TypeScript',
      isPublic: en.isExported(),
    });
  }

  // Type aliases
  for (const ta of sourceFile.getTypeAliases()) {
    const name = ta.getName();
    atoms.push({
      id: generateAtomId(namespace, name, 'typealias'),
      name,
      type: 'TypeAlias',
      namespace,
      filePath,
      lineNumber: ta.getStartLineNumber(),
      language: 'TypeScript',
      isPublic: ta.isExported(),
      signature: `type ${name} = ${truncateType(ta.getType().getText())}`,
    });
  }

  return atoms;
}

function classifyClass(cls: Node): AtomType {
  // Check for React component patterns
  const text = cls.getText();
  if (text.includes('React.Component') || text.includes('extends Component')) {
    return 'Component';
  }
  return 'Class';
}

function buildClassSignature(cls: { getName(): string | undefined; getExtends(): { getText(): string } | undefined; getImplements(): { getText(): string }[] }): string {
  const name = cls.getName() ?? 'Anonymous';
  const ext = cls.getExtends();
  const impls = cls.getImplements();

  let sig = `class ${name}`;
  if (ext) sig += ` extends ${ext.getText()}`;
  if (impls.length > 0) {
    sig += ` implements ${impls.map((i) => i.getText()).join(', ')}`;
  }
  return sig;
}

function buildMethodSignature(method: { getName(): string; getReturnType(): { getText(): string }; getParameters(): { getName(): string; getType(): { getText(): string } }[] }): string {
  const params = method
    .getParameters()
    .map((p) => `${p.getName()}: ${truncateType(p.getType().getText())}`)
    .join(', ');
  return `${method.getName()}(${params}): ${truncateType(method.getReturnType().getText())}`;
}

function truncateType(typeText: string): string {
  return typeText.length > 100 ? typeText.substring(0, 97) + '...' : typeText;
}
