import { statusTone } from '../format';
import type { DocumentStatus, FieldStatus } from '../types';

/** Status as text plus tone: colour is never the only signal. */
export function StatusPill({ status, label }: { status: FieldStatus | DocumentStatus; label?: string }) {
  const s = statusTone(status);
  return <span className={`pill ${s.tone}`}>{label ?? s.label}</span>;
}
