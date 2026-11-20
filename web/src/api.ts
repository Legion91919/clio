import { problemMessage } from './format';
import type { FieldName, Me, Page, ReviewItem, TermDocument } from './types';

/** Thin client over the Clio API. The token lives in memory only (never localStorage). */
export class Api {
  constructor(private readonly token: string) {}

  static async login(email: string, password: string): Promise<string> {
    const res = await fetch('/auth/login', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ email, password }) });
    const body = await res.json().catch(() => null);
    if (!res.ok) throw new Error(problemMessage(body, 'Sign-in failed.'));
    return (body as { accessToken: string }).accessToken;
  }

  private async send<T>(path: string, init: RequestInit = {}): Promise<T> {
    const res = await fetch(path, { ...init, headers: { ...(init.headers ?? {}), authorization: `Bearer ${this.token}` } });
    const body = await res.json().catch(() => null);
    if (!res.ok) throw new Error(problemMessage(body, `Request failed (${res.status}).`));
    return body as T;
  }

  me = () => this.send<Me>('/auth/me');
  documents = () => this.send<TermDocument[]>('/api/documents');
  document = (id: string) => this.send<TermDocument>(`/api/documents/${id}`);
  page = (id: string, page: number) => this.send<Page>(`/api/documents/${id}/pages/${page}`);
  reviewQueue = () => this.send<ReviewItem[]>('/api/review-queue');
  originalUrl = (id: string) => `/api/documents/${id}/original`;

  upload(file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.send<TermDocument>('/api/documents', { method: 'POST', body: form });
  }

  approve(id: string, field: FieldName, value: string | null) {
    return this.send(`/api/documents/${id}/fields/${field}/approve`, {
      method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ value }),
    });
  }

  async downloadOriginal(id: string, fileName: string) {
    const res = await fetch(this.originalUrl(id), { headers: { authorization: `Bearer ${this.token}` } });
    if (!res.ok) throw new Error('The original could not be downloaded.');
    const url = URL.createObjectURL(await res.blob());
    const a = Object.assign(document.createElement('a'), { href: url, download: fileName });
    a.click();
    URL.revokeObjectURL(url);
  }
}
