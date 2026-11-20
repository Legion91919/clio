import type { TermDocument } from '../types';
import { StatusPill } from './StatusPill';

interface Props { documents: TermDocument[] | null; error: string | null; selectedId: string | null; onSelect: (id: string) => void; onRetry: () => void; }

export function DocumentList({ documents, error, selectedId, onSelect, onRetry }: Props) {
  if (error) return <div className="alert" role="alert">Could not load documents: {error} <button onClick={onRetry}>Retry</button></div>;
  if (!documents) return <div aria-busy="true"><div className="skel" /><div className="skel" /><div className="skel" /></div>;
  if (documents.length === 0) return <p className="muted">No term sheets yet. Upload one below.</p>;
  return (
    <table>
      <caption className="muted" style={{ textAlign: 'left' }}>Documents</caption>
      <tbody>
        {documents.map(d => {
          const waiting = d.fields.filter(f => f.status === 'NeedsReview').length;
          return (
            <tr key={d.id} className={`clickable ${d.id === selectedId ? 'sel' : ''}`} onClick={() => onSelect(d.id)}>
              <td><button className="link" onClick={() => onSelect(d.id)} aria-current={d.id === selectedId}>{d.fileName}</button></td>
              <td><StatusPill status={d.status} label={d.status === 'NeedsReview' ? `${waiting} to review` : undefined} /></td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
