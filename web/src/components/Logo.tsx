/** Clio mark: a folded page with three extracted lines, in the accent colour. */
export function Logo() {
  return (
    <span className="mark">
      <svg width="24" height="24" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M5 2h10l4 4v16H5z" fill="var(--color-accent)" />
        <path d="M15 2v4h4" fill="var(--color-accent-soft)" />
        <rect x="8" y="10" width="8" height="2" fill="#fff" /><rect x="8" y="14" width="6" height="2" fill="#fff" /><rect x="8" y="18" width="7" height="2" fill="var(--color-highlight)" />
      </svg>
      Clio <span className="muted" style={{ fontWeight: 400 }}>· Harbourline</span>
    </span>
  );
}
