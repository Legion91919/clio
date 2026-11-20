export type FieldName = 'LoanAmount' | 'Margin' | 'Maturity';
export type FieldStatus = 'AutoAccepted' | 'NeedsReview' | 'Approved';
export type DocumentStatus = 'Received' | 'Processing' | 'NeedsReview' | 'Complete' | 'Unreadable' | 'Failed';

export interface Source { page: number; line: number; }
export interface Field {
  field: FieldName; value: string | null; confidence: number; source: Source | null;
  evidence: string | null; note: string | null; status: FieldStatus; approvedBy: string | null; approvedAt: string | null;
}
export interface TermDocument {
  id: string; fileName: string; status: DocumentStatus; sizeBytes: number; uploadedAt: string; model: string | null;
  error: { code: string; message: string } | null; fields: Field[]; terms: Partial<Record<FieldName, string>>;
}
export interface ReviewItem { documentId: string; fileName: string; field: FieldName; value: string | null; confidence: number; source: Source | null; note: string | null; }
export interface Page { number: number; lines: string[]; }
export interface Me { id: string; email: string; roles: string[]; }
