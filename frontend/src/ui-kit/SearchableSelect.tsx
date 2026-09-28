import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import type { CSSProperties, FocusEvent, KeyboardEvent } from 'react';

export interface SearchableSelectOption {
  value: string | number;
  label: string;
}

interface SearchableSelectProps {
  value: string | number | undefined;
  onChange: (value: string) => void;
  options: SearchableSelectOption[];
  placeholder?: string;
  disabled?: boolean;
  style?: CSSProperties;
  error?: boolean;
  name?: string;
  id?: string;
}

const LIST_MAX_HEIGHT = 240;

/** Where the open list sits on screen — below the input, or above it when there is no room below. */
interface ListPosition { left: number; width: number; top?: number; bottom?: number; maxHeight: number }

function positionFor(input: HTMLElement): ListPosition {
  const rect = input.getBoundingClientRect();
  const below = window.innerHeight - rect.bottom - 8;
  const above = rect.top - 8;
  const openUp = below < 160 && above > below;
  return openUp
    ? { left: rect.left, width: rect.width, bottom: window.innerHeight - rect.top + 4, maxHeight: Math.min(LIST_MAX_HEIGHT, above) }
    : { left: rect.left, width: rect.width, top: rect.bottom + 4, maxHeight: Math.min(LIST_MAX_HEIGHT, Math.max(below, 120)) };
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
 * The open list is drawn in a portal on <body> at a fixed screen position, so no scrolling table,
 * card or `overflow: hidden` box around the input can clip it (Remarks3, items 3/6/8/13/18/21 —
 * item pickers inside line tables). It follows the input while the page scrolls or resizes.
 *
 * Every dropdown in the system is searchable (00-System-Wide-Corrections-02.md, section 1, item
 * 3) — this replaces the plain native `<select>` everywhere. Renders as a text input; typing
 * filters the option list shown below it. Emits string values like a native select's onChange
 * would, so callers that used to do `Number(e.target.value)` just do `Number(value)` instead.
 */
export function SearchableSelect({ value, onChange, options, placeholder, disabled, style, error, name, id }: SearchableSelectProps) {
  const [query, setQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(0);
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const [position, setPosition] = useState<ListPosition | null>(null);

  const selectedOption = useMemo(
    () => options.find((o) => String(o.value) === String(value)),
    [options, value]
  );

  // Reflect the selected option's label when not actively editing.
  useEffect(() => {
    if (!isOpen) {
      setQuery(selectedOption?.label ?? '');
    }
  }, [selectedOption, isOpen]);

  const filteredOptions = useMemo(() => {
    const term = query.trim().toLowerCase();
    if (!term || query === selectedOption?.label) return options;
    return options.filter((o) => o.label.toLowerCase().includes(term));
  }, [options, query, selectedOption]);

  useEffect(() => {
    if (highlightedIndex >= filteredOptions.length) {
      setHighlightedIndex(0);
    }
  }, [filteredOptions, highlightedIndex]);

  // Keep the list glued to the input: any scroll (in any ancestor — capture phase) or resize moves it.
  useLayoutEffect(() => {
    if (!isOpen || !inputRef.current) return;
    const update = () => inputRef.current && setPosition(positionFor(inputRef.current));
    update();
    window.addEventListener('scroll', update, true);
    window.addEventListener('resize', update);
    return () => {
      window.removeEventListener('scroll', update, true);
      window.removeEventListener('resize', update);
    };
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) return;
    const handleClickOutside = (e: MouseEvent) => {
      const target = e.target as Node;
      if (containerRef.current && !containerRef.current.contains(target) && !listRef.current?.contains(target)) {
        setIsOpen(false);
        setQuery(selectedOption?.label ?? '');
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isOpen, selectedOption]);

  const selectOption = (option: SearchableSelectOption) => {
    onChange(String(option.value));
    setQuery(option.label);
    setIsOpen(false);
    inputRef.current?.blur();
  };

  const handleFocus = (e: FocusEvent<HTMLInputElement>) => {
    e.currentTarget.style.borderColor = 'var(--color-gold-500)';
    e.currentTarget.style.background = 'var(--color-surface)';
    setIsOpen(true);
    setHighlightedIndex(0);
  };

  const handleBlurStyle = (e: FocusEvent<HTMLInputElement>) => {
    if (!e.currentTarget.classList.contains('field-error')) {
      e.currentTarget.style.borderColor = 'transparent';
    }
    e.currentTarget.style.background = 'var(--color-surface-2)';
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      if (!isOpen) { setIsOpen(true); return; }
      setHighlightedIndex((i) => Math.min(i + 1, filteredOptions.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setHighlightedIndex((i) => Math.max(i - 1, 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const option = filteredOptions[highlightedIndex];
      if (option) selectOption(option);
    } else if (e.key === 'Escape') {
      setIsOpen(false);
      setQuery(selectedOption?.label ?? '');
      inputRef.current?.blur();
    }
  };

  return (
    <div ref={containerRef} style={{ position: 'relative', ...style }}>
      <input
        ref={inputRef}
        name={name}
        id={id}
        role="combobox"
        aria-expanded={isOpen}
        aria-autocomplete="list"
        autoComplete="off"
        disabled={disabled}
        placeholder={placeholder}
        className={error ? 'field-error' : undefined}
        value={query}
        onChange={(e) => { setQuery(e.target.value); setIsOpen(true); }}
        onFocus={handleFocus}
        onBlur={(e) => { handleBlurStyle(e); setTimeout(() => setIsOpen(false), 120); }}
        onKeyDown={handleKeyDown}
        style={{
          ...inputStyle,
          borderColor: error ? 'var(--color-error)' : 'transparent',
          cursor: disabled ? 'not-allowed' : 'text',
          opacity: disabled ? 0.6 : 1
        }}
      />
      {isOpen && !disabled && position && createPortal(
        <ul
          ref={listRef}
          role="listbox"
          style={{
            position: 'fixed',
            // Above modals (1000): a select inside a modal opens over it.
            zIndex: 1100,
            left: position.left,
            width: position.width,
            top: position.top,
            bottom: position.bottom,
            minWidth: 160,
            margin: 0,
            maxHeight: position.maxHeight,
            overflowY: 'auto',
            boxSizing: 'border-box',
            color: 'var(--color-text)',
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
          {filteredOptions.map((option, index) => (
            <li
              key={option.value}
              role="option"
              aria-selected={String(option.value) === String(value)}
              onMouseDown={(e) => { e.preventDefault(); selectOption(option); }}
              onMouseEnter={() => setHighlightedIndex(index)}
              style={{
                padding: '7px 10px',
                borderRadius: 6,
                cursor: 'pointer',
                fontSize: 13.5,
                background: index === highlightedIndex ? 'var(--color-surface-2)' : 'transparent',
                fontWeight: String(option.value) === String(value) ? 700 : 400
              }}
            >
              {option.label}
            </li>
          ))}
        </ul>,
        document.body
      )}
    </div>
  );
}
