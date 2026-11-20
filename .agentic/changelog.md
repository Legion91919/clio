# Changelog

## Unreleased
- Frame, Map and Decide legs complete (product.md, slice 1, ADR-0001).
- 2026-11-20: Product named Clio. Sketch leg (tokens, three variants, final document-review screen). Slice 1 built test-first: Clean Architecture (Domain, Application, Infrastructure, Api), channel-fed extraction worker, rules extractor behind IExtractionModel, review queue, audited approvals, outbox hand-off, unreadable-file handling, 17-document eval set gating tests and CI, React web app. ADRs 0002-0004. Design dossier in docs/DESIGN.md.
- 2026-11-20: README demo: captioned walkthrough (demo.gif/mp4) and screenshots recorded from the running app with demo/ (Playwright, invented fixture demo/fixtures/sable-ridge-term-sheet.txt); shows upload, confidence + source line, and approving a low-confidence field from the review queue. Layout fix: the source-page panel no longer overflows at 1280px (grid columns may shrink, page lines wrap with numbers aligned, the field table scrolls inside its panel on narrow screens).
