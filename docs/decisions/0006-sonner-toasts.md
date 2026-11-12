# ADR-0006: Toasts with sonner behind `shared/notify` (deviation from FlowOps)

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
FlowOps built its own `Toast`/`ToastProvider`. The brief says: use the same toast library as FlowOps, or `sonner` if FlowOps has none. FlowOps has no library.

## Decision
- `sonner` is the toast library. Only `src/shared/notify/` imports it; components call `notify.success/info/error/apiError`.
- `<Toaster>` is mounted once in `AppShell`, top-center (the lower screen belongs to deck gestures), styled with tokens.

## Consequences
+ Accessible, small, maintained; swappable in one folder.
- One more dependency than FlowOps.

## Alternatives rejected
Porting FlowOps' custom toast (more code to own for no gain).
