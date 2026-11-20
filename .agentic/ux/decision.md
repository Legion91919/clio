# Decision: document review screen

Scored 1–5 at 1440px and 390px (CSS grid collapses to one column below 800–900px).

| Variant | Main job speed | Clarity of numbers | Accessibility | Fits tone | Total |
|---|---|---|---|---|---|
| A: dense table (`document-review-a.html`) | 5 | 5 | 4 | 5 | 19 |
| B: summary cards (`document-review-b.html`) | 3 | 3 (rounded "48.5m" hides cents) | 4 | 3 | 13 |
| C: review queue (`document-review-c.html`) | 4 | 4 | 5 | 4 | 17 |

**Pick: A, with C's side-by-side source page and inline correction, and B's one-line summary.**
Analysts are expert and keyboard-heavy, so the table wins on speed and exact figures (tabular, right-aligned, full precision).
C's source page next to the value is what makes approving fast and trustworthy, so it stays as the right-hand panel.
B's cards round numbers and spread three values over a full screen; only its "1 of 3 terms saved" line survives.
Every status is a text pill, never colour alone. Final: `screens/document-review.html` (loading, empty, error, success).
