export interface LinkInfo {
  atomId: string;
  linkType: string;
}

export interface Owner {
  name: string;
  email: string;
  teamName: string;
  avatarUrl: string;
}

export interface QualityMetrics {
  coveragePercent: number;
  sonarRating: string;
  cyclomaticComplexity: number;
}

export interface ViolationDetails {
  ruleId: string;
  severity: string;
  message: string;
  remediationSuggestion: string;
}

export interface Member {
  id: string;
  name: string;
  type: string;
  signature: string;
  isPublic?: boolean;
}

export interface Atom {
  id: string;
  name: string;
  type: string;
  namespace?: string;
  filePath?: string;
  repository?: string;
  linesOfCode?: number;
  language?: string;
  isPublic?: boolean;
  churnScore?: number;
  maintenanceCost?: number;
  members?: Member[];
  riskScore?: number;
  consumerCount?: number;
  inboundLinks?: LinkInfo[];
  outboundLinks?: LinkInfo[];
}

export interface Namespace {
  path: string;
  atomCount?: number;
  dtoCount?: number;
  interfaceCount?: number;
  atoms?: Atom[];
}

export interface Repository {
  id: string;
  name: string;
  branch?: string;
  atomCount?: number;
  riskScore?: number;
  namespaces?: Namespace[];
  owner?: Owner;
  qualityMetrics?: QualityMetrics;
  churnScore?: number;
  maintenanceCost?: number;
}

export interface FederationStats {
  totalRepos: number;
  totalCodeAtoms: number;
  totalLinks: number;
}

export interface Federation {
  id: string;
  federatedAt: string;
  repositories: Repository[];
  stats?: FederationStats;
}

export interface DetailsPanelData {
  federation?: Federation;
  repository?: Repository;
  namespace?: Namespace;
  atom?: Atom;
}
