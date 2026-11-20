# Runbook

## Run locally
```bash
# API (port 5201). Development seeds samples/ and demo users analyst@example.com / committee@example.com.
export Demo__Password='pick-a-local-demo-password'   # empty: a password is generated and printed once
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5201 dotnet run --project src/Clio.Api
# Web (port 5173, proxies /api /auth /audit to 5201)
cd web && npm ci && npm run dev
```
Config keys: `.env.example`. Packages: `nuget.config` restores `Agentic.Blocks.*` from `eng/packages/` (vendored, ADR-0002) and the rest from nuget.org.

## Tests
```bash
dotnet test Clio.sln                       # with nuget.org access (CI)
dotnet run --project src/Clio.Api -- eval eval   # eval report; exit 1 below the bar
cd web && npx vitest run && npm run build
python3 tools/make_samples.py              # regenerate eval/ and samples/ (invented data)
```
Offline workspace (no nuget.org): build each src project with `--configfile <offline config>` and run tests with the kit's
`run-xunit-offline.sh tests/Clio.Tests src/Clio.Api/Clio.Api.csproj ...` plus a project that references `Agentic.Blocks.Testing`.

## Deploy
Not deployed in slice 1 (in-memory state). Target: one container (API + worker + static web) behind HTTPS in Harbourline's
tenancy, database and blob storage from slice 2, Data Protection keys persisted. Smoke test after deploy: `/health/ready`, login, upload `samples/kestrel-bay-term-sheet.txt`, expect `Complete`.

## Monitor and alerts
- `/health/live`, `/health/ready`; logs carry `CorrelationId`; traces from ActivitySource `Clio.Extraction`.
- Alert: outbox dead letters > 0; oldest review-queue item older than one working day; 5xx error-budget burn.
- `GET /audit/verify` (committee role) should return `{"intact":true}`.

## Common incidents
| Symptom | Check | Fix |
|---|---|---|
| Document stuck in `Received`/`Processing` | API logs for `Extraction worker failed` | Restart API (slice 1 loses the queue: re-upload; dedupe returns the same id) |
| `Unreadable` for a real PDF | does it have a text layer (can you select text)? | Ask for a text PDF or key terms by hand; OCR adapter is on the backlog |
| Many fields in review | eval report, recent template changes | Add the wording to the rules/eval set; never lower the threshold without the eval |
| API refuses to start: `Extraction:Model=llm` | config | Set `Extraction__Model=rules` until the LLM adapter exists |
