# Known Issues and Caveats

_Last reviewed: 2026-04-29_

## Project maturity

This repository is still experimental. The recent audit fixed multiple correctness and maintenance issues, but the project is still not positioned as a production-ready PDS.

## Best-effort background queue

Background work is processed through a bounded in-memory queue.

Implications:
- work is not durable across process restarts
- under sustained pressure the queue may drop the oldest work item
- queue pressure now logs a warning when capacity is reached

## Migration utility

The migration utility exists for bulk actor-store upgrades, but it is still utility-grade. Treat it as an operator tool rather than a hardened production migration framework.

## Configuration surface

Some config fields are still present for compatibility/planned usage and are currently documented as unused:

- `PDS_DID_CACHE_DB_LOCATION`
- `PDS_FETCH_MAX_RESPONSE_SIZE`
- `PDS_HCAPTCHA_SITE_KEY`

See `docs/configuration.md`.

## External integrations

The main runtime can interact with:
- PLC
- AppView
- hCaptcha
- SMTP
- Redis
- crawler endpoints

Local and CI tests avoid those live dependencies by using fakes in the integration test host.

## Operational caution

Before treating this as anything beyond a learning system, review:
- persistence durability expectations
- secret management
- monitoring/telemetry needs
- abuse controls
- deployment hardening
