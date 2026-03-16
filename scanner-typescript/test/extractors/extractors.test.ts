import { describe, it, expect } from 'vitest';
import { Project } from 'ts-morph';
import { extractTypes } from '../../src/extractors/type-extractor.js';
import { extractFunctions } from '../../src/extractors/function-extractor.js';
import { extractImports } from '../../src/extractors/import-extractor.js';
import { SIMPLE_CLASS, ARROW_FUNCTIONS, BARREL_FILE, IMPORTS_FILE } from '../fixtures/index.js';

function createSourceFile(content: string, fileName = 'test.ts') {
  const project = new Project({ useInMemoryFileSystem: true });
  return project.createSourceFile(fileName, content);
}

describe('type-extractor', () => {
  it('should extract classes', () => {
    const sf = createSourceFile(SIMPLE_CLASS);
    const atoms = extractTypes(sf, '/workspace', 'test-ws');

    const classes = atoms.filter((a) => a.type === 'Class');
    expect(classes.length).toBeGreaterThanOrEqual(1);
    expect(classes[0].name).toBe('UserService');
    expect(classes[0].isPublic).toBe(true);
    expect(classes[0].language).toBe('TypeScript');
  });

  it('should extract interfaces', () => {
    const sf = createSourceFile(SIMPLE_CLASS);
    const atoms = extractTypes(sf, '/workspace', 'test-ws');

    const interfaces = atoms.filter((a) => a.type === 'Interface');
    expect(interfaces.length).toBe(1);
    expect(interfaces[0].name).toBe('IUserService');
  });

  it('should extract type aliases', () => {
    const sf = createSourceFile(SIMPLE_CLASS);
    const atoms = extractTypes(sf, '/workspace', 'test-ws');

    const typeAliases = atoms.filter((a) => a.type === 'TypeAlias');
    expect(typeAliases.length).toBe(1);
    expect(typeAliases[0].name).toBe('UserId');
  });

  it('should extract enums', () => {
    const sf = createSourceFile(SIMPLE_CLASS);
    const atoms = extractTypes(sf, '/workspace', 'test-ws');

    const enums = atoms.filter((a) => a.type === 'Enum');
    expect(enums.length).toBe(1);
    expect(enums[0].name).toBe('UserRole');
  });

  it('should extract class methods as child atoms', () => {
    const sf = createSourceFile(SIMPLE_CLASS);
    const atoms = extractTypes(sf, '/workspace', 'test-ws');

    const methods = atoms.filter((a) => a.type === 'Method');
    expect(methods.length).toBeGreaterThanOrEqual(2);
    expect(methods.some((m) => m.name === 'UserService.getName')).toBe(true);
    expect(methods.some((m) => m.name === 'UserService.setName')).toBe(true);
  });

  it('should set namespace from workspace', () => {
    // In-memory files live at '/' so workspace path must match
    const sf = createSourceFile(SIMPLE_CLASS);
    const atoms = extractTypes(sf, '/', 'my-app');

    expect(atoms[0].namespace).toBe('my-app');
  });
});

describe('function-extractor', () => {
  it('should extract arrow functions', () => {
    const sf = createSourceFile(ARROW_FUNCTIONS);
    const atoms = extractFunctions(sf, '/workspace', 'test-ws');

    expect(atoms.length).toBe(3);
    expect(atoms.some((a) => a.name === 'greet')).toBe(true);
    expect(atoms.some((a) => a.name === 'add')).toBe(true);
  });

  it('should classify PascalCase functions as Components', () => {
    const sf = createSourceFile(ARROW_FUNCTIONS);
    const atoms = extractFunctions(sf, '/workspace', 'test-ws');

    const component = atoms.find((a) => a.name === 'ProfileCard');
    expect(component).toBeDefined();
    expect(component!.type).toBe('Component');
  });

  it('should classify camelCase functions as Method', () => {
    const sf = createSourceFile(ARROW_FUNCTIONS);
    const atoms = extractFunctions(sf, '/workspace', 'test-ws');

    const method = atoms.find((a) => a.name === 'greet');
    expect(method).toBeDefined();
    expect(method!.type).toBe('Method');
  });
});

describe('import-extractor', () => {
  it('should extract named imports', () => {
    const sf = createSourceFile(IMPORTS_FILE);
    const links = extractImports(sf, '/workspace', 'test-ws');

    const importLinks = links.filter((l) => l.type === 'Imports');
    expect(importLinks.length).toBeGreaterThanOrEqual(2);
  });

  it('should extract namespace imports', () => {
    const sf = createSourceFile(IMPORTS_FILE);
    const links = extractImports(sf, '/workspace', 'test-ws');

    const namespaceImport = links.find((l) => l.evidence?.includes('import * as'));
    expect(namespaceImport).toBeDefined();
    expect(namespaceImport!.type).toBe('Imports');
  });

  it('should extract re-exports from barrel files', () => {
    const sf = createSourceFile(BARREL_FILE);
    const links = extractImports(sf, '/workspace', 'test-ws');

    const reExports = links.filter((l) => l.type === 'ReExports');
    expect(reExports.length).toBeGreaterThanOrEqual(2);
  });

  it('should set confidence on import links', () => {
    const sf = createSourceFile(IMPORTS_FILE);
    const links = extractImports(sf, '/workspace', 'test-ws');

    for (const link of links) {
      expect(link.confidence).toBeGreaterThan(0);
      expect(link.confidence).toBeLessThanOrEqual(1);
    }
  });
});
