import { useEffect, useRef } from 'react';
import type { Page } from '../types';

interface Props { page: Page | null; highlightLine: number | null; loading: boolean; error: string | null; }

export function SourcePage({ page, highlightLine, loading, error }: Props) {
  const hit = useRef<HTMLLIElement>(null);
  useEffect(() => { hit.current?.scrollIntoView?.({ block: 'center' }); }, [page, highlightLine]);
  if (error) return <div className="alert" role="alert">Page not available: {error}</div>;
  if (loading) return <p className="muted" aria-busy="true">Loading page…</p>;
  if (!page) return <p className="muted">Pick a source link to see the page.</p>;
  return (
    <section aria-label={`Page ${page.number}`}>
      <h2>Page {page.number}{highlightLine ? ` · line ${highlightLine}` : ''}</h2>
      <div className="page"><ol>
        {page.lines.map((l, i) => (
          <li key={i} ref={i + 1 === highlightLine ? hit : undefined} className={i + 1 === highlightLine ? 'hit' : ''}>{l || ' '}</li>
        ))}
      </ol></div>
    </section>
  );
}
