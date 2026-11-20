# User flows

## Flow 1: capture terms from one term sheet (slice 1)
1. Sign in: `LoginForm` (`POST /auth/login`).
2. Upload: `UploadForm` on `screens/document-review.html` (`POST /api/documents`, multipart). API stores the original and replies 202; the same file again returns the same document (200).
3. Wait: `DocumentDetail` loading state while the worker extracts (`GET /api/documents/{id}` polled every second).
4. Check: `FieldTable` shows amount, margin, maturity with confidence and source; fields >= threshold are auto-accepted and saved.
5. Verify a value: click the source link, `SourcePage` shows the page with the line highlighted (`GET /api/documents/{id}/pages/{page}`).

## Flow 2: review low-confidence fields
1. `ReviewQueue` lists fields under the threshold across documents (`GET /api/review-queue`).
2. Open one: `DocumentDetail`; correct the value if needed and Approve (`POST /api/documents/{id}/fields/{field}/approve`). Approver and time are stored and an audit entry is written.
3. When all three terms are saved the document is Complete and a `terms.captured.v1` message goes to screening through the outbox.

## Flow 3: unreadable file
1. Upload a file without a text layer: `DocumentDetail` error state with a clear message (`document.unreadable`).
2. Download the kept original (`GET /api/documents/{id}/original`).
