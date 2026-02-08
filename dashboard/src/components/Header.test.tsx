import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { Header } from './Header';

// Mock child components to isolate Header logic
vi.mock('./Breadcrumb', () => ({
  Breadcrumb: () => <div data-testid="breadcrumb">Breadcrumb</div>
}));

vi.mock('./TimeControls', () => ({
  TimeControls: () => <div data-testid="time-controls">TimeControls</div>
}));

describe('Header Component', () => {
  const defaultProps = {
    activeTab: 'Explorer',
    onTabChange: vi.fn(),
    searchQuery: '',
    onSearchChange: vi.fn(),
    activeHeatmap: null,
    onHeatmapChange: vi.fn(),
    showLinks: true,
    onToggleLinks: vi.fn(),
    snapshots: [],
    currentSnapshotId: null,
    isPlaying: false,
    onSnapshotChange: vi.fn(),
    onTogglePlay: vi.fn()
  };

  it('renders title and tabs', () => {
    render(<Header {...defaultProps} />);
    expect(screen.getByText('Diagnostic Structural Lens')).toBeInTheDocument();
    expect(screen.getByText('Explorer')).toBeInTheDocument();
    expect(screen.getByText('Architecture')).toBeInTheDocument();
    expect(screen.getByText('Governance')).toBeInTheDocument();
  });

  it('calls onTabChange when a tab is clicked', () => {
    render(<Header {...defaultProps} />);
    fireEvent.click(screen.getByText('Architecture'));
    expect(defaultProps.onTabChange).toHaveBeenCalledWith('Architecture');
  });

  it('renders heatmap toggles', () => {
    render(<Header {...defaultProps} />);
    // Heatmap buttons have titles
    expect(screen.getByTitle('Structure')).toBeInTheDocument();
    expect(screen.getByTitle('Churn')).toBeInTheDocument();
    expect(screen.getByTitle('Cost')).toBeInTheDocument();
  });

  it('calls onHeatmapChange when heatmap button is clicked', () => {
    render(<Header {...defaultProps} />);
    const churnBtn = screen.getByTitle('Churn');
    fireEvent.click(churnBtn);
    expect(defaultProps.onHeatmapChange).toHaveBeenCalledWith('churn');
  });

  it('handles search input', () => {
    render(<Header {...defaultProps} />);
    const input = screen.getByPlaceholderText('Search...');
    fireEvent.change(input, { target: { value: 'API' } });
    expect(defaultProps.onSearchChange).toHaveBeenCalledWith('API');
  });

  it('renders child components', () => {
    render(<Header {...defaultProps} />);
    expect(screen.getByTestId('breadcrumb')).toBeInTheDocument();
    expect(screen.getByTestId('time-controls')).toBeInTheDocument();
  });
});
