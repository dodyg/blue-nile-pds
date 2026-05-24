---
name: dotnet-wolverine
description: Build and review .NET applications with Wolverine messaging, durable outbox/inbox, background handlers, and event-sourced workflows.
---

# dotnet-wolverine

Use this skill when working on Wolverine-backed .NET code in this repository.

## Core guidance

- Use Wolverine for internal domain events, asynchronous side effects, background handlers, audit fan-out, and non-blocking read-model updates.
- Keep user-facing commands synchronous at the API boundary; publish durable side effects through Wolverine after the primary state change commits.
- Use the outbox from the start so state changes and background messages stay consistent.
- Add inbox/idempotency handling when background flows can be replayed or duplicated.
- Prefer pure business functions and make side effects obvious from the root handler.
- Do not hide Wolverine behind unnecessary abstractions.

## Handler design

- Keep handlers small and explicit.
- Use cascading messages for follow-up work and notifications.
- Prefer clear command/message names that describe one intent.
- Use Wolverine error handling and retries instead of ad hoc recovery logic.
- Keep call stacks short and avoid splitting one workflow across too many layers.

## Event sourcing and aggregates

- When using Marten with Wolverine, prefer aggregate handler patterns for event-sourced workflows.
- Use `[AggregateHandler]`, `[WriteAggregate]`, `[ConsistentAggregate]`, or `[Aggregate]` only where the workflow truly needs aggregate loading and event emission.
- Be explicit about stream identity, optimistic concurrency, and missing-stream behavior.
- Use natural keys or strong typed identifiers when they make aggregate identity clearer.

## Messaging and durability

- Use transactional inbox/outbox support for durable async boundaries.
- Use background handlers for non-blocking work such as notifications, projections, and integration fan-out.
- Use queueing or exclusive locks when concurrency must be serialized.
- Prefer Wolverine scheduling/delay support for deferred work rather than custom timers.

## Diagnostics

- Check generated code and routing when behavior looks wrong.
- Inspect handler discovery, routing, and configuration before assuming the handler logic is broken.
- Use Wolverine CLI/runtime diagnostics to understand transport, durability, and message flow issues.

## Repository fit

This repo uses Wolverine for:

- domain events for internal side effects
- integration-style background work
- asynchronous notification dispatch
- audit fan-out
- non-blocking reporting and read-model updates

Keep the API boundary synchronous and publish Wolverine-driven side effects after durable state changes complete.
