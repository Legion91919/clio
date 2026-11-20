import { useState } from 'react';
import { FIELD_LABELS, formatConfidence, formatValue } from '../format';
import type { Field, FieldName, Source } from '../types';
import { StatusPill } from './StatusPill';

interface Props {
  fields: Field[];
  meId?: string;
  canApprove: boolean;
  onApprove: (field: FieldName, value: string | null) => Promise<void>;
  onShowSource: (source: Source) => void;
}

export function FieldTable({ fields, meId, canApprove, onApprove, onShowSource }: Props) {
  const [edits, setEdits] = useState<Partial<Record<FieldName, string>>>({});
  const [errors, setErrors] = useState<Partial<Record<FieldName, string>>>({});
  const [busy, setBusy] = useState<FieldName | null>(null);

  if (fields.length === 0) return <p className="muted">No fields extracted.</p>;

  async function approve(f: Field) {
    const edited = edits[f.field];
    setBusy(f.field); setErrors(e => ({ ...e, [f.field]: undefined }));
    try { await onApprove(f.field, edited !== undefined && edited !== (f.value ?? '') ? edited : null); }
    catch (err) { setErrors(e => ({ ...e, [f.field]: (err as Error).message })); }
    finally { setBusy(null); }
  }

  return (
    <table>
      <thead><tr><th>Field</th><th className="r">Value</th><th className="r">Confidence</th><th>Source</th><th>Status</th><th><span className="muted">Action</span></th></tr></thead>
      <tbody>
        {fields.map(f => {
          const review = f.status === 'NeedsReview';
          return (
            <tr key={f.field} className={review ? 'sel' : ''}>
              <td>{FIELD_LABELS[f.field]}{f.note && <div className="note">{f.note}</div>}</td>
              <td className="r num">
                {review && canApprove
                  ? <input className="edit num" aria-label={`${FIELD_LABELS[f.field]} value`} placeholder={f.field === 'Maturity' ? 'yyyy-mm-dd' : f.field === 'LoanAmount' ? 'ZAR 1000000.00' : '2.50'}
                      value={edits[f.field] ?? f.value ?? ''} onChange={e => setEdits(x => ({ ...x, [f.field]: e.target.value }))} />
                  : formatValue(f.field, f.value)}
                {errors[f.field] && <div className="note" role="alert" style={{ color: 'var(--color-danger)' }}>{errors[f.field]}</div>}
              </td>
              <td className="r num">{formatConfidence(f.confidence)}</td>
              <td>{f.source ? <button className="link" onClick={() => onShowSource(f.source!)}>p{f.source.page} · line {f.source.line}</button> : <span className="muted">—</span>}</td>
              <td><StatusPill status={f.status} label={f.status === 'Approved' && f.approvedBy === meId ? 'Approved by you' : undefined} /></td>
              <td>{review && canApprove && <button className="primary" disabled={busy === f.field} onClick={() => approve(f)}>{busy === f.field ? 'Saving…' : 'Approve'}</button>}</td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
