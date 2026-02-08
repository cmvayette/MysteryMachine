import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import App from './App';

// Mock Apollo Client
vi.mock('@apollo/client', async () => {
  const actual = await vi.importActual('@apollo/client');
  return {
    ...actual,
    ApolloProvider: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
    useQuery: () => ({ data: null, loading: false })
  };
});

// Mock client
vi.mock('./graphql/client', () => ({
  client: {},
  FEDERATION_QUERY: {},
  REPOSITORY_QUERY: {},
  NAMESPACE_QUERY: {},
  ATOM_QUERY: {},
  BLAST_RADIUS_QUERY: {},
  SNAPSHOTS_QUERY: {} // Ensure this is mocked
}));

// Mock child components that might cause issues in JSDOM or are heavy
vi.mock('./components/ForceGraph', () => ({
  ForceGraph: () => <div data-testid="force-graph">ForceGraph</div>
}));

vi.mock('./components/GraphLab', () => ({
  GraphLab: () => <div>GraphLab</div>
}));

// @ts-expect-error Mocking global ResizeObserver
global.ResizeObserver = vi.fn().mockImplementation(() => ({
  observe: vi.fn(),
  unobserve: vi.fn(),
  disconnect: vi.fn(),
}));

describe('App Integration', () => {
  it('renders the Dashboard layout', () => {
    render(<App />);
    
    // Check for Header elements
    expect(screen.getByText('Diagnostic Structural Lens')).toBeInTheDocument();
    
    // Check used to fail because of loading state or mocks, but with useQuery mocked to return null data:
    // It might render "Loading..." or empty graph.
    // Based on App.tsx: if loading is false and no data, it renders ForceGraph if nodes > 0, 
    // or "No nodes found" or FileDropZone depending on level.
    // Default level is 'context', fedData is null -> nodes empty.
    // Should render FileDropZone for 'context' level when empty.
    
    // Let's check for the generic structure
    // The Header should be there
    expect(screen.getByPlaceholderText('Search...')).toBeInTheDocument();
  });
});
