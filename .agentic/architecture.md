# Architecture

## Style and why
Advisor output (traits: DocumentIngestion, RegulatoryAudit, ComplexBusinessRules, SmallTeam):

### Recommendation: CleanArchitecture

One business area with rules that must be tested without the database or UI. Keep the domain at the centre.

#### Patterns
- **AuditTrail**: Keep an immutable record of who did what and when; regulators and clients will ask.
- **SeparateIngestionPipeline**: Documents arrive in bursts; queue them and process in a worker so the API stays responsive.
- **HumanInTheLoop**: AI extraction is never 100%: route low-confidence fields to a person and learn from their corrections.

#### Closest reference systems
- Document extraction with human review (similarity 1.00): Clean Architecture API plus a worker fed by a queue; fields the model is unsure about wait for a person.
- Employee tool (leave, timesheets) (similarity 0.40): Monolith: layered, or Clean Architecture if the approval rules are complex.
- Clinic or hospital patient system (similarity 0.33): Clean Architecture or modular monolith, with a full audit history.

#### Risks to watch
- Trusting AI output blindly. Validate against a schema and keep a person in the loop.

#### Evolve when
- A second business area appears with its own language: make it a module of a modular monolith.

Record the final choice with write_adr, including what you rejected.

Judgement: Build a Clean Architecture API with a separate extraction worker fed by a queue, and a review queue for low-confidence fields. See `decisions/0001-architecture-style.md`.

## Context
```mermaid
flowchart LR
  user[User] --> app[This system]
  app --> ext[External system]
```

## Modules and boundaries
TODO: each module, what it owns, what it may call.

## Cross-cutting
TODO: auth, logging, tracing, configuration, error handling.