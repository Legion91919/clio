import { useState, type FormEvent } from 'react';
import { Api } from '../api';
import { Logo } from './Logo';

export function LoginForm({ onLoggedIn }: { onLoggedIn: (token: string) => void }) {
  const [email, setEmail] = useState('analyst@example.com');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true); setError(null);
    try { onLoggedIn(await Api.login(email, password)); }
    catch (err) { setError((err as Error).message); }
    finally { setBusy(false); }
  }

  return (
    <form className="login" onSubmit={submit} aria-label="Sign in">
      <Logo />
      <p className="muted">Sign in to read and review term sheets.</p>
      <label htmlFor="email">Email</label>
      <input id="email" type="email" value={email} onChange={e => setEmail(e.target.value)} autoComplete="username" required />
      <label htmlFor="password">Password</label>
      <input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="current-password" required />
      {error && <div className="alert" role="alert">{error}</div>}
      <button className="primary" type="submit" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
      <p className="note">Demo users (Development): analyst@example.com, committee@example.com. Password: the API console or <code>Demo__Password</code>.</p>
    </form>
  );
}
