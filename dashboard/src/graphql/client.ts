import { ApolloClient, InMemoryCache, HttpLink, gql } from '@apollo/client';

export const client = new ApolloClient({
  link: new HttpLink({ uri: '/graphql' }),
  cache: new InMemoryCache()
});

// GraphQL Queries
export const FEDERATION_QUERY = gql`
  query GetFederation {
    federation {
      id
      federatedAt
      repositories {
        id
        name
        branch
        atomCount
        riskScore
        riskScore
        namespaces
        owner {
          name
          email
          teamName
          avatarUrl
        }
        qualityMetrics {
          coveragePercent
          sonarRating
          cyclomaticComplexity
        }
        churnScore
        maintenanceCost
      }
      crossRepoLinks {
        sourceAtomId
        targetAtomId
        sourceRepo
        targetRepo
        linkType
        isViolation
      }
      stats {
        totalRepos
        totalCodeAtoms
        totalSqlAtoms
        totalLinks
        crossRepoLinkCount
      }
    }
  }
`;

export const REPOSITORY_QUERY = gql`
  query GetRepository($id: String!) {
    repository(id: $id) {
      id
      name
      branch
      namespaces {
        path
        atomCount
        dtoCount
        interfaceCount
      }
      namespaceLinks {
        sourceNamespace
        targetNamespace
        linkCount
      }
      sqlSchemas {
        schema
        tableCount
      }
      inboundLinks {
        sourceAtomId
        sourceRepo
      }
      outboundLinks {
        targetAtomId
        targetRepo
        isViolation
        violationDetails {
          ruleId
          severity
          message
          remediationSuggestion
        }
      }
    }
  }
`;

export const SNAPSHOTS_QUERY = gql`
  query GetSnapshots {
    snapshots {
      id
      scannedAt
      branch
      atomCount
    }
  }
`;

export const NAMESPACE_QUERY = gql`
  query GetNamespace($repoId: String!, $path: String!) {
    namespace(repoId: $repoId, path: $path) {
      path
      atoms {
        id
        name
        type
        riskScore
        consumerCount
        linesOfCode
        language
        isPublic
        churnScore
        maintenanceCost
      }
      internalLinks {
        sourceAtomId
        targetAtomId
        linkType
        isViolation
        violationDetails {
          ruleId
          severity
          message
          remediationSuggestion
        }
      }
    }
  }
`;

export const ATOM_QUERY = gql`
  query GetAtom($id: String!) {
    atom(id: $id) {
      id
      name
      type
      namespace
      filePath
      repository
      linesOfCode
      language
      isPublic
      members {
        id
        name
        type
        signature
        isPublic
      }
      inboundLinks {
        atomId
        linkType
      }
      outboundLinks {
        atomId
        linkType
      }
    }
  }
`;

export const BLAST_RADIUS_QUERY = gql`
  query GetBlastRadius($atomId: String!, $maxDepth: Int) {
    blastRadius(atomId: $atomId, maxDepth: $maxDepth) {
      sourceAtomId
      totalAffected
      affectedAtoms {
        atomId
        depth
      }
      byDepth {
        depth
        count
      }
    }
  }
`;

export const SEARCH_QUERY = gql`
  query Search($query: String!) {
    search(query: $query) {
      atomId
      name
      type
      repository
    }
  }
`;

export const LAYOUT_HINT_QUERY = gql`
  query GetLayoutHint($scopeId: String) {
    layoutHint(scopeId: $scopeId) {
      pattern
      confidence
      hubNodeId
      pipelineOrder
      layerAssignments {
        nodeId
        layer
      }
    }
  }
`;
