import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SectionTabs } from './SectionTabs';

afterEach(cleanup);

describe('SectionTabs', () => {
  const items = [
    { id: 'basic', label: 'بيانات' },
    { id: 'documents', label: 'مستندات', badge: 3 }
  ];

  it('marks the active tab as selected and the others as not', () => {
    render(<SectionTabs items={items} activeId="basic" onChange={vi.fn()} />);

    expect(screen.getByRole('tab', { name: /بيانات/ })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: /مستندات/ })).toHaveAttribute('aria-selected', 'false');
  });

  it('shows the badge count when provided and calls onChange with the clicked tab id', () => {
    const onChange = vi.fn();
    render(<SectionTabs items={items} activeId="basic" onChange={onChange} />);

    expect(screen.getByText('3')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('tab', { name: /مستندات/ }));
    expect(onChange).toHaveBeenCalledWith('documents');
  });

  it('hides the badge when it is zero', () => {
    render(<SectionTabs items={[{ id: 'basic', label: 'بيانات', badge: 0 }]} activeId="basic" onChange={vi.fn()} />);
    expect(screen.queryByText('0')).not.toBeInTheDocument();
  });
});
