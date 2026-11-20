import { FIELD_LABELS, formatConfidence, formatValue } from '../format';
import type { ReviewItem } from '../types';

interface Props { items: ReviewItem[] | null; error: string | null; onOpen: (documentId: string) => void; onRetry: () => void; }

export function ReviewQueue({ items, error, onOpen, onRetry }: Props) {
  if (error) return <div className="alert" role="alert">Could not load the review queue. <button onClick={onRetry}>Retry</button></div>;
  if (!items) return <div aria-busy="true"><div className="skel" /></div>;
  if (items.length === 0) return <p className="muted">Nothing to review.</p>;
  return (
    <section aria-label="Review queue">
      <h2>Review queue <span className="pill rev num">{items.length}</span></h2>
      <ul style={{ paddingLeft: 18, margin: 0 }}>
        {items.map(i => (
          <li key={`${i.documentId}-${i.field}`}>
            <button className="link" onClick={() => onOpen(i.documentId)}>{i.fileName}</button>{' '}
            <span className="num">{FIELD_LABELS[i.field]}: {formatValue(i.field, i.value)} ({formatConfidence(i.confidence)})</span>
          </li>
        ))}
      </ul>
    </section>
  );
}
