# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

A single self-hoster operating their own blue-nile-pds instance. Their job is
day-to-day administration of the server: signing in with the admin password,
finding and inspecting accounts, managing invite codes, browsing repo records,
and checking subject/moderation status. One admin, no multi-user accounts.

## Product Purpose

blue-nile-pds is an experimental, learning-focused ATProto PDS implementation in
.NET 10 (a fork of atompds). The admin web UI is the operator's control surface,
served by the same ASP.NET Core host at /admin/. Success means the owner can
reliably manage their own instance from one small, clear interface.

## Positioning

A self-hostable PDS the owner actually runs — practical and correctness-focused,
not a hosted-service competitor. Distinct value: modern .NET 10 stack, clean
minimal-API surface, and the operator's own instance under their control.

## Operating Context

Runs alongside the PDS host; served at /admin/ via the host's static files
middleware. Talks to the same-origin com.atproto.admin.* XRPC endpoints. Auth is
a single admin password (stored in localStorage after validation). Desktop-first
but usable on mobile/small screens (drawer sidebar, scrollable tables, stacked
cells).

## Capabilities and Constraints

- Login with admin password; protected routes redirect to /login.
- Dashboard stats, account search/detail, invite-code create/revoke, repo
  collection/record browsing with JSON view, subject/moderation status.
- React 19 + Vite + Tailwind CSS v4; TanStack Query v5 for all server state.
- Single-admin password model (no user accounts, no RBAC).
- Experimental; not production-ready. No bindings beyond the minimal & technical
  UI posture the owner confirmed.

## Brand Commitments

No binding visual identity. Keep the admin UI minimal and technical. Existing
name: "PDS Admin".

## Evidence on Hand

Real XRPC endpoints and admin models in src/atompds/Endpoints/ and
src/pdsadmin-web/src/api + hooks. No fabricated content; no testimonials,
customers, or benchmarks to cite.

## Product Principles

1. Own your instance — the operator, not a platform, is in control.
2. Correctness over breadth; experimental scope stays explicit.
3. One admin, one job — minimize what the operator must know to act.
4. Plain and technical beats decorative; the data is the interface.
5. Learning value stays first-class (the repo is a study artifact too).

## Accessibility & Inclusion

Usable on mobile and small screens (responsive sidebar and tables). No
additional product-specific requirement established.
