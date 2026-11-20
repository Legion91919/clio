import type { DocumentStatus, Field, FieldName, FieldStatus, TermDocument } from './types';

export const FIELD_LABELS: Record<FieldName, string> = { LoanAmount: 'Loan amount', Margin: 'Margin', Maturity: 'Maturity' };

/** "ZAR 48500000.00" -> "ZAR 48,500,000.00"; "2.85" -> "2.85%"; dates stay ISO. */
export function formatValue(field: FieldName, value: string | null): string {
  if (value === null) return '—';
  if (field === 'LoanAmount') {
    const [currency, amount] = value.split(' ');
    const n = Number(amount);
    if (!currency || Number.isNaN(n)) return value;
    return `${currency} ${new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(n)}`;
  }
  if (field === 'Margin') return `${value}%`;
  return value;
}

export const formatConfidence = (c: number): string => c.toFixed(2);

export type Tone = 'ok' | 'rev' | 'bad' | 'appr' | 'wait';

export function statusTone(status: FieldStatus | DocumentStatus): { label: string; tone: Tone } {
  switch (status) {
    case 'AutoAccepted': return { label: 'Auto-accepted', tone: 'ok' };
    case 'Approved': return { label: 'Approved', tone: 'appr' };
    case 'NeedsReview': return { label: 'Needs review', tone: 'rev' };
    case 'Complete': return { label: 'Complete', tone: 'ok' };
    case 'Unreadable': return { label: 'Unreadable', tone: 'bad' };
    case 'Failed': return { label: 'Failed', tone: 'bad' };
    default: return { label: 'Reading…', tone: 'wait' };
  }
}

export const isBusy = (d: TermDocument): boolean => d.status === 'Received' || d.status === 'Processing';

/** "1 of 3 terms saved · 2 waiting for review" */
export function summarise(fields: Field[]): string {
  const saved = fields.filter(f => f.status !== 'NeedsReview').length;
  const waiting = fields.length - saved;
  return `${saved} of ${fields.length} terms saved${waiting ? ` · ${waiting} waiting for review` : ''}`;
}

/** Problem Details -> readable message. */
export function problemMessage(body: unknown, fallback: string): string {
  if (body && typeof body === 'object') {
    const p = body as { detail?: string; title?: string; errors?: Record<string, string[]> };
    const firstError = p.errors ? Object.values(p.errors)[0]?.[0] : undefined;
    return firstError ?? p.detail ?? p.title ?? fallback;
  }
  return fallback;
}
