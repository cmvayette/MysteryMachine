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
        namespaces
      }
      crossRepoLinks {
        sourceAtomId
        targetAtomId
        sourceRepo
        targetRepo
        linkType
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
      }
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
        consumerCount
        linesOfCode
        language
        isPublic
      }
      internalLinks {
        sourceAtomId
        targetAtomId
        linkType
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
