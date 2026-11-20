# Quality

## Definition of done
- Acceptance criteria met and demonstrated (one acceptance test per criterion, real HTTP).
- Unit tests for rules, adapter tests for readers/extractor/queue, architecture test for dependency direction.
- Eval passes: per-field accuracy >= 85% and auto-accept precision 100% on `eval/` (tests and CI).
- CI green: build (warnings as errors), format, tests + coverage, eval, vulnerable packages, gitleaks, privacy guard, web (vitest + build).
- Docs in `.agentic/` and `docs/DESIGN.md` updated in the same change.

## Service levels
| SLI | SLO |
|---|---|
| API success rate (non-5xx) | 99.9% over 30 days |
| p95 API latency (reads, upload accept) | < 300 ms |
| Upload to extracted, p95 | < 10 s (rules), < 60 s (future LLM adapter) |
| Auto-accept precision (weekly audit sample) | >= 99.5% |
| Fields corrected after approval | < 1 in 200 |
| Terms captured per document (analyst time) | < 5 minutes |
| Oldest item in the review queue | < 1 working day |
