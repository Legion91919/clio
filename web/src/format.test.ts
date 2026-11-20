import { describe, expect, it } from 'vitest';
import { formatValue, problemMessage, statusTone, summarise } from './format';
import type { Field } from './types';

const field = (status: Field['status']): Field => ({ field: 'Margin', value: '2.85', confidence: 0.9, source: null, evidence: null, note: null, status, approvedBy: null, approvedAt: null });

describe('formatValue', () => {
  it('formats amounts with thousands separators and two decimals', () => {
    expect(formatValue('LoanAmount', 'ZAR 48500000.00')).toBe('ZAR 48,500,000.00');
  });
  it('shows margins as percentages and missing values as a dash', () => {
    expect(formatValue('Margin', '2.85')).toBe('2.85%');
    expect(formatValue('Maturity', null)).toBe('—');
  });
});

describe('statusTone', () => {
  it('always gives a text label, never colour alone', () => {
    expect(statusTone('NeedsReview')).toEqual({ label: 'Needs review', tone: 'rev' });
    expect(statusTone('Unreadable').label).toBe('Unreadable');
    expect(statusTone('Processing').label).toBe('Reading…');
  });
});

describe('summarise and problemMessage', () => {
  it('counts saved and waiting fields', () => {
    expect(summarise([field('AutoAccepted'), field('NeedsReview'), field('Approved')])).toBe('2 of 3 terms saved · 1 waiting for review');
  });
  it('prefers field errors, then detail, then the fallback', () => {
    expect(problemMessage({ detail: 'x', errors: { value: ['Use an ISO date'] } }, 'f')).toBe('Use an ISO date');
    expect(problemMessage({ detail: 'The file is empty.' }, 'f')).toBe('The file is empty.');
    expect(problemMessage(null, 'fallback')).toBe('fallback');
  });
});
