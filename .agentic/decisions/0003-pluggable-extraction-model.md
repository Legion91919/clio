# 0003. Pluggable extraction model, rules first, LLM as a port

- Status: Accepted
- Date: 2026-11-20

## Context
Term sheets may hold borrower personal data (POPIA) and the model must be swappable for a local one. Slice 1 needs
amount, margin and maturity with per-field confidence and a source location; an eval set must prove accuracy.

## Decision
- Application owns `IExtractionModel` (`ExtractAsync(DocumentText) -> FieldCandidate[]`: value, confidence, page, line).
  The domain validates every value (`FieldValueRules`), applies plausibility checks and routes by the threshold, so no
  model output is trusted directly.
- Slice 1 ships `RulesExtractionModel` (`rules-v1`): labels, regexes and calibrated confidence. Deterministic, free,
  runs offline, no data leaves the process.
- The LLM adapter is a documented port: prompt `prompts/extract-terms.v1.md`, schema `prompts/term-sheet.schema.json`,
  config `Extraction:Model=llm` (refuses to start until built). The adapter must: call with structured output, validate
  against the schema, retry once with the validation error, then return confidence 0 so a person reviews it; log model,
  version, prompt id, tokens and cost; send no data to a provider without a contract (a local model is the default
  path for sensitive deployments).
- PDF text comes through `IDocumentTextReader`. Slice 1 has a minimal text-layer PDF reader (BCL only); OCR and
  complex PDFs (CID fonts, object streams) are a later adapter behind the same port.

## Alternatives considered
- LLM first: better on messy prose, but needs a provider contract for client data, costs per page and is
  non-deterministic in CI. Kept as the second adapter, judged by the same eval set.
- A PDF library from NuGet: richer, but not available in this build environment; the port keeps that swap one class.

## Consequences
- Any model is compared on the same eval set (`eval/`, run in tests with a bar: per-field accuracy >= 85% and
  auto-accept precision 100%).
- Rules miss free-form wording; those fields arrive with low confidence and go to the review queue.
