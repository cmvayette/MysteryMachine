import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { DetailsPanel } from './DetailsPanel';
import type { DetailsPanelData } from '../types';

// Mock child panels
vi.mock('./AtomInfoPanel', () => ({
  AtomInfoPanel: ({ atom }: { atom: { name: string } }) => <div data-testid="atom-panel">{atom.name}</div>
}));

vi.mock('./MemberPanel', () => ({
  MemberPanel: ({ atomName }: { atomName: string }) => <div data-testid="member-panel">{atomName}</div>
}));

describe('DetailsPanel Component', () => {
  const mockClose = vi.fn();
  const mockDrillDown = vi.fn();
  const mockSelectAtom = vi.fn();

  const defaultProps = {
    selectedNodeId: 'test-node',
    level: 'context',
    data: {} as DetailsPanelData,
    onClose: mockClose,
    onDrillDown: mockDrillDown,
    onSelectAtom: mockSelectAtom
  };

  it('renders close button', () => {
    render(<DetailsPanel {...defaultProps} />);
    const closeBtn = screen.getByTitle('Close');
    expect(closeBtn).toBeInTheDocument();
  });

  it('renders Context Level content', () => {
    const contextData: DetailsPanelData = {
      federation: {
        id: 'fed-1',
        name: 'MyFederation',
        stats: { totalRepos: 10, totalModules: 50, totalAtoms: 200 },
        repositories: [],
        crossRepoLinks: [],
        federatedAt: '2023-01-01'
      } as unknown as DetailsPanelData['federation']
    };

    render(<DetailsPanel {...defaultProps} level="context" data={contextData} selectedNodeId="fed-1" />);
    
    expect(screen.getByText('System Context')).toBeInTheDocument();
    expect(screen.getByText('System Purpose')).toBeInTheDocument();
  });

  it('renders Repository Level content', () => {
    const repoData: DetailsPanelData = {
      repository: {
        id: 'repo-1',
        name: 'MyRepo',
        branch: 'main',
        atomCount: 100,
        namesapces: [],
        owner: {
            name: 'Test Owner',
            email: 'test@owner.com',
            teamName: 'Test Team',
            avatarUrl: 'http://avatar.url'
        },
        qualityMetrics: {
            coveragePercent: 85,
            sonarRating: 'A',
            cyclomaticComplexity: 10
        },
        namespaces: [],
        namespaceLinks: []
      } as unknown as DetailsPanelData['repository']
    };

    render(<DetailsPanel {...defaultProps} level="system" data={{ federation: { repositories: [repoData.repository] } } as unknown as DetailsPanelData} selectedNodeId="repo-1" />);
    
    // In system level, it looks up repo in federation data
    expect(screen.getByText('Repository')).toBeInTheDocument();
    expect(screen.getByText('MyRepo')).toBeInTheDocument();
    expect(screen.getByText('Test Owner')).toBeInTheDocument();
    expect(screen.getByText('Test Team')).toBeInTheDocument();
    expect(screen.getByText('85%')).toBeInTheDocument();
  });

  it('renders Component (Atom) Level content', () => {
    const atomData: DetailsPanelData = {
      atom: {
        id: 'atom-1',
        name: 'MyComponent',
        type: 'class',
        repository: 'MyRepo',
        module: 'MyModule',
        file: 'file.cs',
        line: 1,
        sourceCode: '',
        members: [],
        inboundLinks: [],
        outboundLinks: []
      } as unknown as DetailsPanelData['atom']
    };

    render(<DetailsPanel {...defaultProps} level="component" data={atomData} selectedNodeId="atom-1" />);
    
    expect(screen.getByTestId('atom-panel')).toHaveTextContent('MyComponent');
  });

  it('calls onClose when close button is clicked', () => {
    render(<DetailsPanel {...defaultProps} />);
    fireEvent.click(screen.getByTitle('Close'));
    expect(mockClose).toHaveBeenCalled();
  });
});
