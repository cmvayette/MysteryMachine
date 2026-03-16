import { SourceFile, SyntaxKind, CallExpression, Node } from 'ts-morph';
import type { CodeAtom, AtomLink } from '../models/scan-result.js';
import { generateAtomId, generateLinkId } from '../utils/id-generator.js';
import { filePathToNamespace } from '../utils/path-utils.js';

/**
 * Result from GraphQL extraction — produces both atoms (types/resolvers) and links.
 */
export interface GraphQLExtractionResult {
  atoms: CodeAtom[];
  links: AtomLink[];
}

/**
 * Extracts GraphQL schema definitions from Pothos SchemaBuilder patterns.
 *
 * Detects:
 * - `builder.objectRef<T>('Name').implement({...})` → Interface atom
 * - `builder.queryField('name', ...)` / `builder.queryFields(...)` → Method atom
 * - `builder.mutationField('name', ...)` / `builder.mutationFields(...)` → Method atom
 * - `builder.subscriptionField('name', ...)` → Method atom
 * - `builder.scalarType('Name', ...)` → TypeAlias atom
 */
export function extractGraphQL(
  sourceFile: SourceFile,
  workspacePath: string,
  workspaceName: string
): GraphQLExtractionResult {
  const atoms: CodeAtom[] = [];
  const links: AtomLink[] = [];
  const filePath = sourceFile.getFilePath();
  const namespace = filePathToNamespace(filePath, workspacePath, workspaceName);

  // Walk all call expressions looking for builder.* patterns
  sourceFile.forEachDescendant((node) => {
    if (!Node.isCallExpression(node)) return;

    const expr = node.getExpression();
    const exprText = expr.getText();

    // ── Object Type: builder.objectRef<T>('Name').implement({...}) ──
    if (exprText.endsWith('.implement') && isObjectRefImplement(node)) {
      const typeName = extractObjectRefName(node);
      if (typeName) {
        atoms.push({
          id: generateAtomId(namespace, `GQL.${typeName}`, 'interface'),
          name: `GQL.${typeName}`,
          type: 'Interface',
          namespace,
          filePath,
          lineNumber: node.getStartLineNumber(),
          linesOfCode: node.getEndLineNumber() - node.getStartLineNumber() + 1,
          language: 'TypeScript',
          isPublic: true,
          signature: `type ${typeName} (GraphQL Object)`,
        });

        // Check for interface implementations (e.g., interfaces: [HolonObj])
        const implInterfaces = extractImplementedInterfaces(node);
        for (const iface of implInterfaces) {
          links.push({
            id: generateLinkId(`GQL.${typeName}`, `GQL.${iface}`, 'implements'),
            sourceId: generateAtomId(namespace, `GQL.${typeName}`, 'interface'),
            targetId: generateAtomId(namespace, `GQL.${iface}`, 'interface'),
            type: 'Implements',
            confidence: 0.9,
            evidence: `${typeName} implements ${iface} (Pothos interfaces)`,
          });
        }
      }
    }

    // ── Single resolver: builder.queryField('name', ...) ──
    if (exprText === 'builder.queryField') {
      const resolverInfo = extractSingleResolver(node, 'Query');
      if (resolverInfo) {
        atoms.push(makeResolverAtom(resolverInfo, namespace, filePath, node));
        links.push(...extractResolverLinks(node, resolverInfo, namespace));
      }
    }

    // ── Batch resolvers: builder.queryFields(...) ──
    if (exprText === 'builder.queryFields') {
      for (const info of extractBatchResolvers(node, 'Query')) {
        atoms.push(makeResolverAtom(info, namespace, filePath, node));
        // links from batch resolvers are harder to trace; skip for now
      }
    }

    // ── Single mutation: builder.mutationField('name', ...) ──
    if (exprText === 'builder.mutationField') {
      const resolverInfo = extractSingleResolver(node, 'Mutation');
      if (resolverInfo) {
        atoms.push(makeResolverAtom(resolverInfo, namespace, filePath, node));
        links.push(...extractResolverLinks(node, resolverInfo, namespace));
      }
    }

    // ── Batch mutations: builder.mutationFields(...) ──
    if (exprText === 'builder.mutationFields') {
      for (const info of extractBatchResolvers(node, 'Mutation')) {
        atoms.push(makeResolverAtom(info, namespace, filePath, node));
      }
    }

    // ── Subscription: builder.subscriptionField('name', ...) ──
    if (exprText === 'builder.subscriptionField') {
      const resolverInfo = extractSingleResolver(node, 'Subscription');
      if (resolverInfo) {
        atoms.push(makeResolverAtom(resolverInfo, namespace, filePath, node));
      }
    }

    // ── Scalar type: builder.scalarType('Name', ...) ──
    if (exprText === 'builder.scalarType') {
      const args = node.getArguments();
      if (args.length > 0) {
        const name = extractStringLiteral(args[0]);
        if (name) {
          atoms.push({
            id: generateAtomId(namespace, `GQL.${name}`, 'typealias'),
            name: `GQL.${name}`,
            type: 'TypeAlias',
            namespace,
            filePath,
            lineNumber: node.getStartLineNumber(),
            language: 'TypeScript',
            isPublic: true,
            signature: `scalar ${name} (GraphQL Scalar)`,
          });
        }
      }
    }
  });

  return { atoms, links };
}

// ── Helpers ──────────────────────────────────────────────────────────────────

interface ResolverInfo {
  name: string;
  operation: 'Query' | 'Mutation' | 'Subscription';
}

function makeResolverAtom(
  info: ResolverInfo,
  namespace: string,
  filePath: string,
  node: CallExpression
): CodeAtom {
  return {
    id: generateAtomId(namespace, `GQL.${info.operation}.${info.name}`, 'method'),
    name: `GQL.${info.operation}.${info.name}`,
    type: 'Method',
    namespace,
    filePath,
    lineNumber: node.getStartLineNumber(),
    linesOfCode: node.getEndLineNumber() - node.getStartLineNumber() + 1,
    language: 'TypeScript',
    isPublic: true,
    signature: `${info.operation.toLowerCase()} ${info.name} (GraphQL)`,
  };
}

/**
 * Extracts the type name from `builder.objectRef<T>('Name')`.
 * The name is the first string argument to `objectRef`.
 */
function extractObjectRefName(implementCall: CallExpression): string | null {
  // implementCall is `.implement(...)`, so we need the callee's object: `builder.objectRef('Name')`
  const callee = implementCall.getExpression();
  // callee is `<something>.implement` — get the property access
  if (!Node.isPropertyAccessExpression(callee)) return null;
  const objectExpr = callee.getExpression();

  // objectExpr might be builder.objectRef('Name') directly, or a variable like TaskObj
  if (Node.isCallExpression(objectExpr)) {
    // Direct: builder.objectRef<T>('Name').implement(...)
    const args = objectExpr.getArguments();
    if (args.length > 0) {
      return extractStringLiteral(args[0]);
    }
  }

  // Variable: TaskObj.implement(...) — harder to resolve, use variable name
  if (Node.isIdentifier(objectExpr)) {
    // Try to find the variable declaration with objectRef call
    const varName = objectExpr.getText();
    const sourceFile = implementCall.getSourceFile();
    for (const decl of sourceFile.getVariableDeclarations()) {
      if (decl.getName() === varName) {
        const init = decl.getInitializer();
        if (init && Node.isCallExpression(init)) {
          const initExpr = init.getExpression().getText();
          if (initExpr.includes('objectRef')) {
            const args = init.getArguments();
            if (args.length > 0) {
              return extractStringLiteral(args[0]);
            }
          }
        }
      }
    }
    // Fallback: strip "Obj" suffix from variable name
    return varName.replace(/Obj$/, '');
  }

  return null;
}

/**
 * Check if this is a `.implement()` call on an objectRef result.
 */
function isObjectRefImplement(node: CallExpression): boolean {
  const callee = node.getExpression();
  if (!Node.isPropertyAccessExpression(callee)) return false;
  if (callee.getName() !== 'implement') return false;

  // Check if the object is an objectRef call or a known Obj variable
  const obj = callee.getExpression();
  if (Node.isCallExpression(obj)) {
    return obj.getExpression().getText().includes('objectRef');
  }
  if (Node.isIdentifier(obj)) {
    // Heuristic: variable ending in Obj (e.g., TaskObj, PersonObj)
    const name = obj.getText();
    // Check if this var was assigned from an objectRef call
    const sourceFile = node.getSourceFile();
    for (const decl of sourceFile.getVariableDeclarations()) {
      if (decl.getName() === name) {
        const init = decl.getInitializer();
        if (init) {
          const initText = init.getText();
          return initText.includes('objectRef');
        }
      }
    }
  }
  return false;
}

/**
 * Extract interface names from `.implement({ interfaces: [HolonObj, ...] })`.
 */
function extractImplementedInterfaces(implementCall: CallExpression): string[] {
  const args = implementCall.getArguments();
  if (args.length === 0) return [];

  const configText = args[0].getText();
  const match = configText.match(/interfaces:\s*\[([^\]]+)\]/);
  if (!match) return [];

  return match[1]
    .split(',')
    .map((s) => s.trim().replace(/Obj$/, ''))
    .filter((s) => s.length > 0);
}

/**
 * Extract resolver name from single-field calls like `builder.queryField('name', ...)`.
 */
function extractSingleResolver(
  node: CallExpression,
  operation: 'Query' | 'Mutation' | 'Subscription'
): ResolverInfo | null {
  const args = node.getArguments();
  if (args.length === 0) return null;

  const name = extractStringLiteral(args[0]);
  if (!name) return null;

  return { name, operation };
}

/**
 * Extract resolver names from batch calls like `builder.queryFields((t) => ({ name1: ..., name2: ... }))`.
 */
function extractBatchResolvers(
  node: CallExpression,
  operation: 'Query' | 'Mutation' | 'Subscription'
): ResolverInfo[] {
  const args = node.getArguments();
  if (args.length === 0) return [];

  // The argument is a function that returns an object literal
  const funcArg = args[0];
  const resolvers: ResolverInfo[] = [];

  // Walk the function body looking for object literal properties
  funcArg.forEachDescendant((child) => {
    if (Node.isPropertyAssignment(child)) {
      const name = child.getName();
      if (name && !name.startsWith('_')) {
        resolvers.push({ name, operation });
      }
    }
  });

  return resolvers;
}

/**
 * Extract links from resolver to domain services via `ctx.<service>` patterns.
 */
function extractResolverLinks(
  node: CallExpression,
  resolverInfo: ResolverInfo,
  namespace: string
): AtomLink[] {
  const links: AtomLink[] = [];
  const fullText = node.getText();

  // Look for ctx.loaders.<name>.load patterns
  const loaderMatches = fullText.matchAll(/ctx\.loaders\.(\w+)\.load/g);
  for (const match of loaderMatches) {
    const loaderName = match[1];
    links.push({
      id: generateLinkId(
        `GQL.${resolverInfo.operation}.${resolverInfo.name}`,
        `loader.${loaderName}`,
        'calls'
      ),
      sourceId: generateAtomId(namespace, `GQL.${resolverInfo.operation}.${resolverInfo.name}`, 'method'),
      targetId: generateAtomId(namespace, `loader.${loaderName}`, 'method'),
      type: 'Calls',
      confidence: 0.7,
      evidence: `Resolver ${resolverInfo.name} uses ctx.loaders.${loaderName}`,
    });
  }

  // Look for ctx.services.<name> or ctx.<domainManager> patterns
  const serviceMatches = fullText.matchAll(/ctx\.(?:services\.)?(\w+Manager|\w+Service)\b/g);
  for (const match of serviceMatches) {
    const serviceName = match[1];
    links.push({
      id: generateLinkId(
        `GQL.${resolverInfo.operation}.${resolverInfo.name}`,
        serviceName,
        'calls'
      ),
      sourceId: generateAtomId(namespace, `GQL.${resolverInfo.operation}.${resolverInfo.name}`, 'method'),
      targetId: generateAtomId(namespace, serviceName, 'class'),
      type: 'Calls',
      confidence: 0.8,
      evidence: `Resolver ${resolverInfo.name} calls ${serviceName}`,
    });
  }

  return links;
}

function extractStringLiteral(node: Node): string | null {
  if (Node.isStringLiteral(node)) {
    return node.getLiteralValue();
  }
  // Handle template literals or identifiers — skip
  return null;
}
