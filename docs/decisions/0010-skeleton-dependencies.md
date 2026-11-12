# ADR-0010: Only the dependencies the skeleton uses (deviation from FlowOps)

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
FlowOps carries MSW (API mocks), react-hook-form + zod (forms), Testing Library + jsdom, lucide-react, Respawn and the SignalR .NET client for tests. The brief says every dependency must have a reason.

## Decision
- Not added yet: MSW, react-hook-form, zod, Testing Library/jsdom, lucide-react (icons are inline SVG from the design), Respawn, SignalR .NET client.
- Added beyond FlowOps: `sonner` (ADR-0006), `Pgvector(.EntityFrameworkCore)` (ADR-0002), `Microsoft.EntityFrameworkCore.Relational` pinned to the EF Core version (Npgsql alone resolved an older one and tests failed to load it), `@fontsource` packages for the three brand fonts.
- A feature adds a library when it needs it, with the reason in its PR and, if it's a pattern choice, an ADR.

## Consequences
+ Smaller, clearer skeleton.
- The first form-heavy or interaction-heavy feature will add these back; follow FlowOps' choices unless there's a reason not to.

## Alternatives rejected
Copying FlowOps' full dependency list up front.
