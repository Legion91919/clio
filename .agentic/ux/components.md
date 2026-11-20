# Components (React, `web/src/components/`)

| Component | Props | Loading | Empty | Error | Success |
|---|---|---|---|---|---|
| `LoginForm` | `onLoggedIn(token)` | button disabled, "Signing in…" | — | inline `role=alert` with the API message | calls `onLoggedIn` |
| `UploadForm` | `onUploaded(doc)` | "Uploading…" | file picker with accepted types | `role=alert` with ProblemDetails `detail` | new document selected |
| `DocumentList` | `documents`, `selectedId`, `onSelect` | skeleton rows | "No term sheets yet" + upload hint | `role=alert` "Could not load documents" + retry | table with status pills |
| `DocumentDetail` | `document`, `onApprove(field, value)`, `onShowSource(location)` | "Reading …" skeleton while status is Received/Processing | — | Unreadable: error alert + "download original" link | summary line + `FieldTable` |
| `FieldTable` | `fields`, `onApprove`, `onShowSource` | — | "No fields extracted" | per-row error under the field after a failed approve | rows: value, confidence, source link, status pill, approve |
| `StatusPill` | `status` | — | — | — | text label + tone (never colour only) |
| `SourcePage` | `page` (`number`, `lines`), `highlightLine` | "Loading page…" | "Pick a source link to see the page" | `role=alert` "Page not available" | numbered lines, hit line highlighted and scrolled into view |
| `ReviewQueue` | `items`, `onOpen(documentId)` | skeleton | "Nothing to review" | `role=alert` + retry | count + list of fields waiting |
