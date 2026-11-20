import { isBusy, summarise } from '../format';
import type { FieldName, Source, TermDocument } from '../types';
import { FieldTable } from './FieldTable';

interface Props {
  document: TermDocument;
  meId?: string;
  canApprove: boolean;
  threshold: number;
  onApprove: (field: FieldName, value: string | null) => Promise<void>;
  onShowSource: (source: Source) => void;
  onDownloadOriginal: () => void;
}

export function DocumentDetail({ document: d, meId, canApprove, threshold, onApprove, onShowSource, onDownloadOriginal }: Props) {
  if (isBusy(d)) {
    return (
      <div aria-busy="true"><p className="muted">Reading {d.fileName}…</p><div className="skel" /><div className="skel" style={{ width: '80%' }} /><div className="skel" style={{ width: '60%' }} /></div>
    );
  }
  if (d.status === 'Unreadable' || d.status === 'Failed') {
    return (
      <div className="alert" role="alert">
        <strong>We could not read {d.fileName}.</strong> {d.error?.message}{' '}
        <button className="link" onClick={onDownloadOriginal}>Download original</button>
      </div>
    );
  }
  return (
    <div>
      <div className="summary"><strong>{d.fileName}</strong> · <span className="num">{summarise(d.fields)}</span> · threshold {threshold.toFixed(2)}
        {' '}· <button className="link" onClick={onDownloadOriginal}>original</button></div>
      <FieldTable fields={d.fields} meId={meId} canApprove={canApprove} onApprove={onApprove} onShowSource={onShowSource} />
    </div>
  );
}
