import { useState, type ChangeEvent } from 'react';
import type { Api } from '../api';
import type { TermDocument } from '../types';

export function UploadForm({ api, onUploaded }: { api: Api; onUploaded: (doc: TermDocument) => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function choose(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setBusy(true); setError(null);
    try { onUploaded(await api.upload(file)); }
    catch (err) { setError((err as Error).message); }
    finally { setBusy(false); e.target.value = ''; }
  }

  return (
    <div>
      <label className="muted" htmlFor="upload">Upload a term sheet (.pdf with text, .txt, .md)</label><br />
      <input id="upload" type="file" accept=".pdf,.txt,.md,application/pdf,text/plain" onChange={choose} disabled={busy} />
      {busy && <p className="muted" aria-live="polite">Uploading…</p>}
      {error && <div className="alert" role="alert">{error}</div>}
    </div>
  );
}
