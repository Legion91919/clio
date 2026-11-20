# Lessons

Generic, reusable lessons only. No client names, data or secrets.
- For AI extraction, measure auto-accept precision, not just accuracy: a wrong value with low confidence costs a review, a wrong value with high confidence costs trust.
- Keep the value schema in the domain and run every model output (and every human correction) through it; the model then only proposes.
- `[CallerFilePath]` is a simple way for tests to find repo fixtures (eval sets) regardless of where the test binary runs.
- Analyzer rule CA1711 rejects type names ending in Queue; name ports after the role (channel, inbox) instead.
