import { useEffect, useMemo, useRef, useState } from 'react';
import type { CSSProperties, FocusEvent } from 'react';
import type { SearchableSelectOption } from './SearchableSelect';

interface SearchableMultiSelectProps {
  value: (string | number)[];
  onChange: (value: (string | number)[]) => void;
  options: SearchableSelectOption[];
  placeholder?: string;
  disabled?: boolean;
  style?: CSSProperties;
}

const inputStyle: CSSProperties = {
  padding: '9px 12px',
  borderRadius: 'var(--radius-chip)',
  border: '1px solid transparent',
  background: 'var(--color-surface-2)',
  fontSize: 14,
  fontFamily: 'inherit',
  outline: 'none',
  transition: 'border-color 0.15s, background 0.15s',
  width: '100%',
  boxSizing: 'border-box'
};

/**
 * Searchable multi-select (00-System-Wide-Corrections-02.md, section 1, item 3) — selected
 * options render as removable chips above a search box; picking an option keeps the list open so
 * several can be picked in a row.
 */
export function SearchableMultiSelect({ value, onChange, options, placeholder, disabled, style }: SearchableMultiSelectProps) {
  const [query, setQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const selectedOptions = useMemo(
    () => value.map((v) => options.find((o) => String(o.value) === String(v))).filter((o): o is SearchableSelectOption => !!o),
    [value, options]
  );

  const filteredOptions = useMemo(() => {
    const term = query.trim().toLowerCase();
    const available = options.filter((o) => !value.some((v) => String(v) === String(o.value)));
    if (!term) return available;
    return available.filter((o) => o.label.toLowerCase().includes(term));
  }, [options, query, value]);

  useEffect(() => {
    if (!isOpen) return;
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
        setQuery('');
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isOpen]);

  const toggleOption = (option: SearchableSelectOption) => {
    onChange([...value, option.value]);
    setQuery('');
  };

  const removeOption = (option: SearchableSelectOption) => {
    onChange(value.filter((v) => String(v) !== String(option.value)));
  };

  const handleFocus = (e: FocusEvent<HTMLInputElement>) => {
    e.currentTarget.style.borderColor = 'var(--color-gold-500)';
    e.currentTarget.style.background = 'var(--color-surface)';
    setIsOpen(true);
  };

  const handleBlurStyle = (e: FocusEvent<HTMLInputElement>) => {
    e.currentTarget.style.borderColor = 'transparent';
    e.currentTarget.style.background = 'var(--color-surface-2)';
  };

  return (
    <div ref={containerRef} style={{ position: 'relative', ...style }}>
      <div
        style={{
          ...inputStyle,
          display: 'flex',
          flexWrap: 'wrap',
          gap: 6,
          alignItems: 'center',
          cursor: disabled ? 'not-allowed' : 'text',
          opacity: disabled ? 0.6 : 1,
          minHeight: 20
        }}
        onClick={() => !disabled && inputRef.current?.focus()}
      >
        {selectedOptions.map((option) => (
          <span
            key={option.value}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 4,
              background: 'var(--color-navy-500)',
              color: '#fff',
              borderRadius: 'var(--radius-pill)',
              padding: '2px 8px',
              fontSize: 12.5
            }}
          >
            {option.label}
            {!disabled && (
              <button
                type="button"
                onClick={(e) => { e.stopPropagation(); removeOption(option); }}
                style={{ background: 'none', border: 'none', color: '#fff', cursor: 'pointer', padding: 0, fontSize: 13, lineHeight: 1 }}
                aria-label="remove"
              >
                ×
              </button>
            )}
          </span>
        ))}
        <input
          ref={inputRef}
          role="combobox"
          aria-expanded={isOpen}
          autoComplete="off"
          disabled={disabled}
          placeholder={selectedOptions.length === 0 ? placeholder : undefined}
          value={query}
          onChange={(e) => { setQuery(e.target.value); setIsOpen(true); }}
          onFocus={handleFocus}
          onBlur={(e) => { handleBlurStyle(e); setTimeout(() => setIsOpen(false), 120); }}
          style={{ flex: 1, minWidth: 80, border: 'none', outline: 'none', background: 'transparent', fontSize: 14, fontFamily: 'inherit', padding: 0 }}
        />
      </div>
      {isOpen && !disabled && (
        <ul
          role="listbox"
          style={{
            position: 'absolute',
            zIndex: 20,
            top: '100%',
            insetInlineStart: 0,
            insetInlineEnd: 0,
            marginTop: 4,
            maxHeight: 240,
            overflowY: 'auto',
            background: 'var(--color-surface)',
            border: '1px solid var(--color-border)',
            borderRadius: 'var(--radius-md, 8px)',
            boxShadow: 'var(--shadow-2)',
            padding: 4,
            listStyle: 'none'
          }}
        >
          {filteredOptions.length === 0 && (
            <li style={{ padding: '8px 10px', fontSize: 13, color: 'var(--color-text-muted)' }}>—</li>
          )}
          {filteredOptions.map((option) => (
            <li
              key={option.value}
              role="option"
              onMouseDown={(e) => { e.preventDefault(); toggleOption(option); }}
              style={{ padding: '7px 10px', borderRadius: 6, cursor: 'pointer', fontSize: 13.5 }}
              onMouseEnter={(e) => { e.currentTarget.style.background = 'var(--color-surface-2)'; }}
              onMouseLeave={(e) => { e.currentTarget.style.background = 'transparent'; }}
            >
              {option.label}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
