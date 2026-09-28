/** Modern line-icon set (Feather/Lucide-style) — replaces emoji glyphs in the ActionBar/Toolbar,
 * per the system-wide design pass. Pure inline SVG: no icon font/library dependency. */
const PATHS: Record<string, string> = {
  plus: 'M12 5v14M5 12h14',
  save: 'M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2Z|M17 21v-8H7v8|M7 3v5h8',
  trash: 'M3 6h18|M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2|M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6|M10 11v6M14 11v6',
  chevronsLeft: 'M11 17l-5-5 5-5|M18 17l-5-5 5-5',
  chevronLeft: 'M15 18l-6-6 6-6',
  chevronRight: 'M9 6l6 6-6 6',
  chevronsRight: 'M13 17l5-5-5-5|M6 17l5-5-5-5',
  printer: 'M6 9V3h12v6',
  download: 'M12 3v12|M7 10l5 5 5-5|M5 21h14',
  upload: 'M12 21V9|M7 14l5-5 5 5|M5 3h14',
  paperclip: 'M21.44 11.05l-9.19 9.19a5 5 0 0 1-7.07-7.07l9.19-9.19a3 3 0 0 1 4.24 4.24l-9.2 9.19a1 1 0 0 1-1.41-1.41l8.48-8.49',
  search: 'M21 21l-4.3-4.3',
  check: 'M4 12l5 5L20 6',
  x: 'M18 6 6 18M6 6l12 12',
  reverse: 'M3 12a9 9 0 1 0 3-6.7|M3 3v5h5',
  balance: 'M12 3v18|M4 8l4-4 4 4|M2 8h6l-3 6a3 3 0 0 1-3-6Z|M16 8l4-4 4 4|M14 8h6l-3 6a3 3 0 0 1-3-6Z',
  bell: 'M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9|M13.73 21a2 2 0 0 1-3.46 0'
};

interface IconProps {
  name: keyof typeof PATHS;
  size?: number;
  className?: string;
  style?: React.CSSProperties;
}

export function Icon({ name, size = 16, className, style }: IconProps) {
  const segments = PATHS[name]?.split('|') ?? [];
  return (
    <svg
      viewBox="0 0 24 24"
      width={size}
      height={size}
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      style={{ flexShrink: 0, ...style }}
      aria-hidden="true"
    >
      {name === 'printer' && <rect x={4} y={9} width={16} height={8} rx={1.5} />}
      {segments.map((d, i) => (
        <path key={i} d={d} />
      ))}
      {name === 'printer' && <path d="M6 17v4h12v-4" />}
    </svg>
  );
}
