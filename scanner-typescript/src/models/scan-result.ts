/**
 * ScanResult model — matches the C# ScanResult JSON schema exactly.
 * Property names use camelCase to match C#'s JsonNamingPolicy.CamelCase.
 */

export interface CodeAtom {
  id: string;
  name: string;
  type: AtomType;
  namespace: string;
  repository?: string;
  signature?: string;
  targetFramework?: string;
  filePath?: string;
  lineNumber?: number;
  linesOfCode?: number;
  language: string;
  isPublic: boolean;
}

export interface SqlAtom {
  id: string;
  name: string;
  type: SqlAtomType;
  parentTable?: string;
  dataType?: string;
  isNullable: boolean;
  filePath?: string;
}

export interface AtomLink {
  id: string;
  sourceId: string;
  targetId: string;
  type: LinkType;
  confidence: number;
  evidence?: string;
}

export interface ScanDiagnostic {
  severity: 'Info' | 'Warning' | 'Error';
  message: string;
  filePath?: string;
  line?: number;
}

export interface ScanResult {
  codeAtoms: CodeAtom[];
  sqlAtoms: SqlAtom[];
  links: AtomLink[];
  diagnostics: ScanDiagnostic[];
  duration: string; // TimeSpan serialized as "HH:MM:SS.fff"
}

export interface Snapshot {
  id: string;
  repository: string;
  scannedAt: string;
  branch?: string;
  commitSha?: string;
  codeAtoms: CodeAtom[];
  sqlAtoms: SqlAtom[];
  links: AtomLink[];
  metadata: SnapshotMetadata;
}

export interface SnapshotMetadata {
  totalCodeAtoms: number;
  totalSqlAtoms: number;
  totalLinks: number;
  dtoCount: number;
  interfaceCount: number;
  tableCount: number;
  storedProcedureCount: number;
  typeAliasCount: number;
  moduleCount: number;
  componentCount: number;
  scanDuration: string;
}

// Enums matching C# — string values for JSON serialization
export type AtomType =
  | 'Class'
  | 'Interface'
  | 'Record'
  | 'Struct'
  | 'Enum'
  | 'Method'
  | 'Property'
  | 'Field'
  | 'Dto'
  | 'Unknown'
  | 'TypeAlias'
  | 'Module'
  | 'Component';

export type SqlAtomType =
  | 'Table'
  | 'Column'
  | 'StoredProcedure'
  | 'View'
  | 'Function'
  | 'Index';

export type LinkType =
  | 'Inherits'
  | 'Implements'
  | 'Calls'
  | 'References'
  | 'Contains'
  | 'NameMatch'
  | 'AttributeBinding'
  | 'QueryTrace'
  | 'ExactMatch'
  | 'FuzzyMatch'
  | 'PropertyMatch'
  | 'PackageDependency'
  | 'ProjectReference'
  | 'Imports'
  | 'ReExports'
  | 'WorkspaceDependency';

/**
 * Workspace discovered from a monorepo's package.json.
 */
export interface WorkspaceInfo {
  /** Name from the workspace's package.json */
  name: string;
  /** Absolute path to the workspace directory */
  path: string;
  /** Relative path from monorepo root */
  relativePath: string;
  /** Dependencies on other workspaces in this monorepo */
  workspaceDeps: string[];
}
