/** The one default loading pattern (00-Frontend-Specs.md, section 3.3/13). */
export function Skeleton({ width = '100%', height = 16 }: { width?: string | number; height?: number }) {
  return (
    <div
      style={{
        width,
        height,
        borderRadius: 4,
        background: 'linear-gradient(90deg, #eceff3 25%, #f6f7f9 37%, #eceff3 63%)',
        backgroundSize: '400% 100%',
        animation: 'habbak-skeleton 1.4s ease infinite'
      }}
    />
  );
}

export function SkeletonRows({ rows, columns }: { rows: number; columns: number }) {
  return (
    <>
      {Array.from({ length: rows }).map((_, r) => (
        <tr key={r}>
          {Array.from({ length: columns }).map((_, c) => (
            <td key={c} style={{ padding: '10px 12px' }}>
              <Skeleton />
            </td>
          ))}
        </tr>
      ))}
    </>
  );
}
