import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import type { Field } from '../types';
import { DocumentDetail } from './DocumentDetail';
import { FieldTable } from './FieldTable';

const fields: Field[] = [
  { field: 'LoanAmount', value: 'ZAR 48500000.00', confidence: 0.97, source: { page: 1, line: 7 }, evidence: null, note: null, status: 'AutoAccepted', approvedBy: null, approvedAt: null },
  { field: 'Margin', value: '2.85', confidence: 0.72, source: { page: 1, line: 12 }, evidence: null, note: 'Found in running text', status: 'NeedsReview', approvedBy: null, approvedAt: null },
  { field: 'Maturity', value: '2031-06-30', confidence: 0.96, source: { page: 2, line: 3 }, evidence: null, note: null, status: 'Approved', approvedBy: 'u1', approvedAt: '2026-11-20T08:00:00Z' },
];
const noop = async () => {};

describe('FieldTable', () => {
  it('shows value, confidence, source link and an approve action only for fields under review', () => {
    const html = renderToStaticMarkup(<FieldTable fields={fields} meId="u1" canApprove onApprove={noop} onShowSource={() => {}} />);
    expect(html).toContain('ZAR 48,500,000.00');
    expect(html).toContain('0.72');
    expect(html).toContain('p1 · line 12');
    expect(html).toContain('Approved by you');
    expect(html.match(/>Approve</g)?.length).toBe(1);
  });

  it('hides approve for read-only committee users', () => {
    const html = renderToStaticMarkup(<FieldTable fields={fields} canApprove={false} onApprove={noop} onShowSource={() => {}} />);
    expect(html).not.toContain('>Approve<');
    expect(html).toContain('2.85%');
  });
});

describe('DocumentDetail states', () => {
  const base = { id: 'd1', fileName: 'scan.pdf', sizeBytes: 1, uploadedAt: '', model: null, terms: {}, fields: [] };
  it('shows a clear error and a download link for unreadable files', () => {
    const html = renderToStaticMarkup(<DocumentDetail document={{ ...base, status: 'Unreadable', error: { code: 'document.unreadable', message: 'No text layer.' } }}
      canApprove threshold={0.9} onApprove={noop} onShowSource={() => {}} onDownloadOriginal={() => {}} />);
    expect(html).toContain('role="alert"');
    expect(html).toContain('No text layer.');
    expect(html).toContain('Download original');
  });
  it('shows a loading state while the worker reads the file', () => {
    const html = renderToStaticMarkup(<DocumentDetail document={{ ...base, status: 'Processing', error: null }}
      canApprove threshold={0.9} onApprove={noop} onShowSource={() => {}} onDownloadOriginal={() => {}} />);
    expect(html).toContain('aria-busy="true"');
  });
});
