# 0001. Architecture style

- Status: Accepted
- Date: 2026-11-19

## Context

Harbourline credit analysts copy loan terms out of scanned term sheets by hand, which delays screening by days and causes keying errors. Traits: DocumentIngestion, RegulatoryAudit, ComplexBusinessRules, SmallTeam.

## Decision

Build a Clean Architecture API with a separate extraction worker fed by a queue, and a review queue for low-confidence fields.

## Alternatives considered

- Plain layered app calling the model inside the HTTP request: simpler, but uploads would time out and bursts would block the UI.
- Microservices per document type: far more operations than two engineers can run, for no scaling benefit.

## Consequences

- Clear boundaries that tests can enforce.
- Room to extract a part later if its load demands it.
