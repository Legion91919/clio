# Handover

What the client needs to own this without us.

- [ ] Source, infrastructure-as-code and pipelines in the client's own accounts
- [ ] Every secret rotated and owned by the client
- [ ] Architecture and decisions explained (`architecture.md`, `decisions/`)
- [ ] Runbook rehearsed by a client engineer
- [ ] Known issues and next steps listed below
- [ ] AI agent setup documented so their team can keep using it

## Known issues
- Slice 1 stores everything in memory: a restart loses documents, originals, audit and queued work (slice 2).
- The PDF reader handles simple text-layer PDFs only (no OCR, CID fonts or compressed object streams); those files are reported unreadable.
- The LLM adapter is a documented port only (`prompts/`, ADR-0003); `Extraction__Model=llm` refuses to start.
- The eval set has 17 documents; the ai-in-product guideline asks for 30-100 before relying on the threshold in production.

## Recommended next steps
- Slice 2 persistence, then grow the eval set with anonymised real-shaped term sheets and re-tune the threshold.