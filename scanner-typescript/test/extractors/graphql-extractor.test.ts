import { describe, it, expect } from 'vitest';
import { Project } from 'ts-morph';
import { extractGraphQL } from '../../src/extractors/graphql-extractor.js';

/**
 * Creates a ts-morph SourceFile from inline code for testing.
 */
function createSourceFile(code: string, filename = 'test.ts') {
  const project = new Project({ useInMemoryFileSystem: true });
  return project.createSourceFile(filename, code);
}

describe('graphql-extractor', () => {
  describe('object type detection', () => {
    it('detects builder.objectRef().implement() — inline chained', () => {
      const sf = createSourceFile(`
        const builder = { objectRef: () => ({ implement: () => {} }) } as any;
        builder.objectRef<any>('Task').implement({
          fields: (t: any) => ({
            title: t.string({ resolve: (p: any) => p.title }),
          }),
        });
      `);

      const result = extractGraphQL(sf, '/workspace', '@test/pkg');
      const taskType = result.atoms.find((a) => a.name === 'GQL.Task');
      expect(taskType).toBeDefined();
      expect(taskType!.type).toBe('Interface');
      expect(taskType!.signature).toContain('GraphQL Object');
    });

    it('detects variable-based objectRef().implement()', () => {
      const sf = createSourceFile(`
        const builder = { objectRef: () => ({ implement: () => {} }) } as any;
        const TaskObj = builder.objectRef<any>('Task');
        TaskObj.implement({
          fields: (t: any) => ({}),
        });
      `);

      const result = extractGraphQL(sf, '/workspace', '@test/pkg');
      expect(result.atoms.find((a) => a.name === 'GQL.Task')).toBeDefined();
    });

    it('extracts interfaces from implement({ interfaces: [...] })', () => {
      const sf = createSourceFile(`
        const builder = { objectRef: () => ({ implement: () => {} }) } as any;
        const HolonObj = {} as any;
        const TaskObj = builder.objectRef<any>('Task');
        TaskObj.implement({
          interfaces: [HolonObj],
          fields: (t: any) => ({}),
        });
      `);

      const result = extractGraphQL(sf, '/workspace', '@test/pkg');
      const implLink = result.links.find((l) => l.type === 'Implements');
      expect(implLink).toBeDefined();
      expect(implLink!.evidence).toContain('Task implements Holon');
    });
  });

  describe('resolver detection', () => {
    it('detects builder.queryField()', () => {
      const sf = createSourceFile(`
        const builder = { queryField: () => {} } as any;
        builder.queryField('organizations', (t: any) => t.field({
          type: {},
          resolve: async (_root: any, _args: any, ctx: any) => {
            return [];
          },
        }));
      `);

      const result = extractGraphQL(sf, '/workspace', '@test/pkg');
      const resolver = result.atoms.find((a) => a.name === 'GQL.Query.organizations');
      expect(resolver).toBeDefined();
      expect(resolver!.type).toBe('Method');
      expect(resolver!.signature).toContain('query organizations');
    });

    it('detects builder.mutationField()', () => {
      const sf = createSourceFile(`
        const builder = { mutationField: () => {} } as any;
        builder.mutationField('createTask', (t: any) => t.field({
          resolve: async () => {},
        }));
      `);

      const result = extractGraphQL(sf, '/workspace', '@test/pkg');
      const resolver = result.atoms.find((a) => a.name === 'GQL.Mutation.createTask');
      expect(resolver).toBeDefined();
      expect(resolver!.signature).toContain('mutation createTask');
    });

    it('detects builder.subscriptionField()', () => {
      const sf = createSourceFile(`
        const builder = { subscriptionField: () => {} } as any;
        builder.subscriptionField('activityStream', (t: any) => t.field({
          resolve: async () => {},
        }));
      `);

      const result = extractGraphQL(sf, '/workspace', '@test/pkg');
      const resolver = result.atoms.find((a) => a.name === 'GQL.Subscription.activityStream');
      expect(resolver).toBeDefined();
    });

    it('detects builder.queryFields() batch pattern', () => {
      const sf = createSourceFile(`
        const builder = { queryFields: () => {} } as any;
        builder.queryFields((t: any) => ({
          infrastructure: t.field({ resolve: () => {} }),
          assets: t.field({ resolve: () => {} }),
        }));
      `);

      const result = extractGraphQL(sf, '/workspace', '@test/pkg');
      expect(result.atoms.find((a) => a.name === 'GQL.Query.infrastructure')).toBeDefined();
      expect(result.atoms.find((a) => a.name === 'GQL.Query.assets')).toBeDefined();
    });

    it('detects builder.mutationFields() batch pattern', () => {
      const sf = createSourceFile(`
        const builder = { mutationFields: () => {} } as any;
        builder.mutationFields((t: any) => ({
          createPerson: t.field({ resolve: () => {} }),
          deletePerson: t.field({ resolve: () => {} }),
        }));
      `);

      const result = extractGraphQL(sf, '/workspace', '@test/pkg');
      expect(result.atoms.find((a) => a.name === 'GQL.Mutation.createPerson')).toBeDefined();
      expect(result.atoms.find((a) => a.name === 'GQL.Mutation.deletePerson')).toBeDefined();
    });
  });

  describe('scalar type detection', () => {
    it('detects builder.scalarType()', () => {
      const sf = createSourceFile(`
        const builder = { scalarType: () => {} } as any;
        builder.scalarType('Date', {
          serialize: (date: any) => date.toISOString(),
          parseValue: (date: any) => new Date(date),
        });
      `);

      const result = extractGraphQL(sf, '/workspace', '@test/pkg');
      const scalar = result.atoms.find((a) => a.name === 'GQL.Date');
      expect(scalar).toBeDefined();
      expect(scalar!.type).toBe('TypeAlias');
      expect(scalar!.signature).toContain('scalar Date');
    });
  });

  describe('resolver-to-service links', () => {
    it('extracts ctx.loaders links', () => {
      const sf = createSourceFile(`
        const builder = { queryField: () => {} } as any;
        builder.queryField('organization', (t: any) => t.field({
          resolve: async (_root: any, args: any, ctx: any) => {
            return ctx.loaders.holon.load(args.id);
          },
        }));
      `);

      const result = extractGraphQL(sf, '/workspace', '@test/pkg');
      const callLink = result.links.find(
        (l) => l.type === 'Calls' && l.evidence?.includes('ctx.loaders.holon')
      );
      expect(callLink).toBeDefined();
    });
  });

  describe('non-graphql files', () => {
    it('returns empty for files without Pothos patterns', () => {
      const sf = createSourceFile(`
        export class UserService {
          getUsers() { return []; }
        }
      `);

      const result = extractGraphQL(sf, '/workspace', '@test/pkg');
      expect(result.atoms).toHaveLength(0);
      expect(result.links).toHaveLength(0);
    });
  });
});
