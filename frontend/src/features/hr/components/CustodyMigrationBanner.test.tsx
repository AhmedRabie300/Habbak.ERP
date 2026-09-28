import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { CustodyMigrationBanner } from './CustodyMigrationBanner';

describe('CustodyMigrationBanner', () => {
  it('renders nothing when there is nothing to migrate', () => {
    const { container } = render(<CustodyMigrationBanner count={0} onMigrate={vi.fn()} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('shows the count and calls onMigrate when the action is clicked', () => {
    const onMigrate = vi.fn();
    render(<CustodyMigrationBanner count={4} onMigrate={onMigrate} />);

    expect(screen.getByText(/4/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button'));
    expect(onMigrate).toHaveBeenCalledTimes(1);
  });
});
