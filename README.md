# Clio

Clio extracts loan terms from term sheets for Harbourline Lending's credit analysts (Harbourline is fictional; all data is invented).
Analysts used to copy terms out of scanned term sheets by hand, which delayed screening by days and caused keying errors.

## See it in action

![Clio walkthrough: an analyst signs in, uploads an invented term sheet, sees loan amount, margin and maturity extracted with confidence scores and source lines, then approves the low-confidence step-up margin from the review queue](docs/media/demo.gif)

| Fields with confidence and source | Review queue | Approved by the analyst |
|---|---|---|
| ![Extracted fields with confidence and page and line links, source line highlighted](docs/media/extracted.png) | ![Review queue listing the step-up margin at 0.60 confidence](docs/media/review-queue.png) | ![Margin marked Approved by you with the source line highlighted](docs/media/approved.png) |

Full-quality video: [docs/media/demo.mp4](docs/media/demo.mp4). Recorded from the running app with `demo/` (`cd demo && npm install && npm run record`).

## Design at a glance
Upload a term sheet; Clio proposes **loan amount, margin and maturity**, each with a confidence and the page and line it came from.
Values at or above 0.90 are saved; the rest wait in a review queue, and every approval records who approved it in a tamper-evident audit log.

**Architecture: Clean Architecture monolith** (Domain, Application, Infrastructure, Api) with an extraction worker
(`BackgroundService` fed by a bounded `System.Threading.Channels` channel) in the API process. One business area whose
rules must be tested without a database, UI or model, two engineers, ~200 documents a month: a distributed design would add
cost and no benefit, while Clean Architecture keeps the extraction model a swappable adapter (rules today, LLM or local model
later, ADR-0003). Full reasoning, capacity sums, failure modes and Q&A: [`docs/DESIGN.md`](docs/DESIGN.md).

```mermaid
flowchart LR
  analyst[Credit analyst] -->|upload, approve| web[React web] --> api[Clio.Api]
  committee[Credit committee] -->|read, audit| web
  api -->|channel| worker[Extraction worker\nIExtractionModel: rules-v1]
  api --> audit[(Audit log)]
  api -->|outbox: terms.captured.v1| screening[Credit screening]
```


## Run it in GitHub Codespaces

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Legion91919/clio?quickstart=1)

The dev container installs the SDKs, restores packages and runs every test once on first start. The same setup works locally with VS Code's Dev Containers extension.

| What | Command | Port |
|---|---|---|
| API (Clio) | `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://0.0.0.0:5201 dotnet run --project src/Clio.Api` | 5201 |
| Web app | `cd web && npm run dev` | 5173 |
| All tests (same as CI) | `scripts/test-all.sh` | - |

## What it does (slice 1)
- `POST /api/documents`: text-layer PDF, `.txt` or `.md` (10 MB max). Same file twice returns the same document.
- Fields with value, confidence, source page/line, status (auto-accepted, needs review, approved by).
- Review queue, approve with optional correction (analyst role), committee is read-only.
- Unreadable files (no text layer, binary, corrupt PDF) get a plain error; the original stays downloadable.
- Eval set of 17 labelled invented documents (`eval/`) with a runner that reports per-field accuracy and gates CI.

## Run locally
```bash
# API on http://localhost:5201 (Development seeds invented samples and two demo users)
Demo__Password='pick-a-local-demo-password' ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/Clio.Api
# Web on http://localhost:5173 (proxies /api, /auth, /audit to 5201)
cd web && npm ci && npm run dev
```
Sign in as `analyst@example.com` or `committee@example.com` with that password. More (tests, eval, offline builds): `.agentic/runbook.md`.

## How this project is organised
`src/` four Clean Architecture projects, `tests/Clio.Tests` (domain, adapters, acceptance, eval, architecture), `web/` React app,
`eval/` labelled documents, `samples/` demo files, `prompts/` the LLM port's prompt and schema, `eng/packages/` vendored building blocks.
Project knowledge lives in the hidden `.agentic/` folder (start with `.agentic/README.md`). AI agents read `AGENTS.md` first.

Built with the Baton model from the Agentic Engineering Kit: frame → map → decide → sketch → slice loop → pass the baton.
