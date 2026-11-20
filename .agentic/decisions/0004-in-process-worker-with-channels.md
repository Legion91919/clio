# 0004. Extraction worker in the API process, fed by a bounded channel

- Status: Accepted
- Date: 2026-11-20

## Context
ADR-0001 asks for a separate ingestion pipeline so uploads stay fast. Volume is about 200 documents a month, two engineers.

## Decision
`POST /api/documents` stores the original and writes the id to a bounded `System.Threading.Channels` channel
(capacity 500, writers wait when full). `ExtractionWorker`, a `BackgroundService` in the same process, reads it; each
document is one unit of work (DI scope, per-document lock, outbox commit on success). Failures mark the document
Failed and keep the original; the loop carries on.

## Alternatives considered
- A broker (queue service) and a separate worker deployable: survives restarts and scales out, but adds infrastructure
  for 10 documents a day.
- Extract inside the HTTP request: simplest, but slow PDFs would block uploads and time out.

## Consequences
- In-memory queue: a restart loses queued ids. With the database in slice 2, the worker re-queues documents still in
  `Received` on start-up; a broker replaces `IExtractionChannel` when volume or multiple instances need it.
