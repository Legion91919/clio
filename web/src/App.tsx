import { useCallback, useEffect, useMemo, useState } from 'react';
import { Api } from './api';
import { isBusy } from './format';
import type { FieldName, Me, Page, ReviewItem, Source, TermDocument } from './types';
import { DocumentDetail } from './components/DocumentDetail';
import { DocumentList } from './components/DocumentList';
import { LoginForm } from './components/LoginForm';
import { Logo } from './components/Logo';
import { ReviewQueue } from './components/ReviewQueue';
import { SourcePage } from './components/SourcePage';
import { UploadForm } from './components/UploadForm';

const THRESHOLD = 0.9;

export function App() {
  const [token, setToken] = useState<string | null>(null);
  return token ? <Workspace api={new Api(token)} onSignOut={() => setToken(null)} /> : <LoginForm onLoggedIn={setToken} />;
}

function Workspace({ api, onSignOut }: { api: Api; onSignOut: () => void }) {
  const client = useMemo(() => api, [api]);
  const [me, setMe] = useState<Me | null>(null);
  const [documents, setDocuments] = useState<TermDocument[] | null>(null);
  const [listError, setListError] = useState<string | null>(null);
  const [queue, setQueue] = useState<ReviewItem[] | null>(null);
  const [queueError, setQueueError] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [source, setSource] = useState<Source | null>(null);
  const [page, setPage] = useState<Page | null>(null);
  const [pageState, setPageState] = useState<{ loading: boolean; error: string | null }>({ loading: false, error: null });

  const refresh = useCallback(async () => {
    try { setDocuments(await client.documents()); setListError(null); } catch (e) { setListError((e as Error).message); }
    try { setQueue(await client.reviewQueue()); setQueueError(null); } catch (e) { setQueueError((e as Error).message); }
  }, [client]);

  useEffect(() => { client.me().then(setMe).catch(() => onSignOut()); void refresh(); }, [client, refresh, onSignOut]);

  // Poll while the worker is still reading a document.
  useEffect(() => {
    if (!documents?.some(isBusy)) return;
    const t = setTimeout(() => void refresh(), 1000);
    return () => clearTimeout(t);
  }, [documents, refresh]);

  const selected = documents?.find(d => d.id === selectedId) ?? documents?.[0] ?? null;
  const canApprove = me?.roles.includes('analyst') ?? false;

  async function showSource(s: Source) {
    if (!selected) return;
    setSource(s); setPageState({ loading: true, error: null });
    try { setPage(await client.page(selected.id, s.page)); setPageState({ loading: false, error: null }); }
    catch (e) { setPageState({ loading: false, error: (e as Error).message }); }
  }

  async function approve(field: FieldName, value: string | null) {
    if (!selected) return;
    await client.approve(selected.id, field, value);
    await refresh();
  }

  function select(id: string) { setSelectedId(id); setPage(null); setSource(null); }

  return (
    <>
      <header className="top">
        <Logo />
        <span className="muted">{me ? `${me.email} · ${me.roles.join(', ')}` : '…'} <button onClick={onSignOut}>Sign out</button></span>
      </header>
      <main className="layout">
        <aside>
          <DocumentList documents={documents} error={listError} selectedId={selected?.id ?? null} onSelect={select} onRetry={() => void refresh()} />
          {canApprove && <p><UploadForm api={client} onUploaded={d => { select(d.id); void refresh(); }} /></p>}
          <ReviewQueue items={queue} error={queueError} onOpen={select} onRetry={() => void refresh()} />
        </aside>
        <section aria-label="Document">
          {!selected
            ? documents && <div><h2>No term sheets yet</h2><p className="muted">Upload a text-layer PDF or a .txt file. Clio extracts loan amount, margin and maturity and links each value to its page.</p></div>
            : (
              <div className="detail">
                <DocumentDetail document={selected} meId={me?.id} canApprove={canApprove} threshold={THRESHOLD} onApprove={approve}
                  onShowSource={s => void showSource(s)} onDownloadOriginal={() => void client.downloadOriginal(selected.id, selected.fileName)} />
                <SourcePage page={page} highlightLine={source?.line ?? null} loading={pageState.loading} error={pageState.error} />
              </div>
            )}
        </section>
      </main>
    </>
  );
}
