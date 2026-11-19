# Product brief

> Harbourline Lending is a fictional company. All names, documents and numbers in this repo are invented.

## Problem
Harbourline credit analysts copy loan terms out of scanned term sheets by hand, which delays screening by days and causes keying errors.

## Users
Credit analysts (daily, expert, keyboard-heavy); credit committee (weekly, read-only).

## Outcomes
Terms captured in under 5 minutes per document (today ~45); fewer than 1 in 200 fields corrected after approval; every value links to its page.

## Constraints
Documents may contain borrower personal data (POPIA). Model must be swappable for a local one. Invented sample documents only in the repo.

## Out of scope
Anything not needed for the outcomes above in the first three slices.

## Traits (for the architecture advisor)
- DocumentIngestion: "term sheets and facility letters arrive as scanned PDFs by email, roughly 200 a month"
- RegulatoryAudit: "credit committee and auditors ask where each number came from"
- ComplexBusinessRules: "margin, fees and covenants must be cross-checked (e.g. LTV = loan / valuation)"
- SmallTeam: "two engineers"
