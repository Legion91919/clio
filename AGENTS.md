# AGENTS.md

Clio extracts loan terms from term sheets. Harbourline credit analysts copy loan terms out of scanned term sheets by hand, which delays screening by days and causes keying errors.

## Read first
Everything about this project lives in the hidden `.agentic/` folder.
Start with `.agentic/README.md`; it says which file answers which question.
Load only the files your task needs.

## Hard rules
- Work in thin vertical slices: one ticket from `.agentic/backlog.md` at a time.
- No change without a test that fails first. Run the full test suite before you finish.
- A decision that is hard to reverse gets an ADR in `.agentic/decisions/`.
- Never commit secrets, real client data or personal data. Use `.env.example`.
- Do not grow this file. New knowledge goes in the right `.agentic/` file; history goes in `.agentic/changelog.md`.
- When you learn something that would help on the next project, add it to `.agentic/lessons.md`.

## What is live
Slice 1: upload, rules-based extraction with confidence and page/line, review queue, approve with audit, unreadable-file handling, eval gate. API on port 5201, web on 5173; see `.agentic/runbook.md`.
