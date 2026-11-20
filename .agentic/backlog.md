# Backlog

Vertical slices, smallest valuable first.

## Now
- [x] Slice 1: Analyst uploads one term sheet and sees extracted loan amount, margin and maturity, each with confidence and a link to the page
  - [x] Given a clear term sheet, when it is uploaded, then amount, margin and maturity are extracted with confidence >= 0.9
  - [x] Given a field below the threshold, then it is shown in the review queue, not saved
  - [x] Given an approved field, then its source page and the approving user are stored
  - [x] Given an unreadable file, then the analyst sees a clear error and the original is kept
  - Test notes: one acceptance test per criterion; seeded invented data.

## Next
- [x] Sketch leg: tokens + three variants of the slice-1 screen (`sketch-ui`)
- [ ] CI green with all gates before the first feature commit (workflow ready; run on first push)

## Later
- [ ] Slice 2: persistence (database for documents, fields, audit, outbox in one transaction; blob storage for originals; re-queue `Received` on start)
- [ ] Slice 3: more terms (fees, covenants, valuation) with the cross-check LTV = loan / valuation
- [ ] LLM or local-model adapter behind `IExtractionModel` (ADR-0003), judged on the eval set; grow the eval set to 30+ documents
- [ ] OCR adapter behind `IDocumentTextReader` for image-only scans
- [ ] Playwright e2e per flow in `ux/flows.md` with axe
