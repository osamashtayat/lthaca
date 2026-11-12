# ADR-0004: Expected failures are returned as Result, not thrown (deviation from FlowOps)

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
FlowOps returns failures as typed exceptions (`AppException`, `DomainException`) mapped by one global handler. The Ithaca brief asks for one shared Result/Error type in Application and controllers that only map a result to a response.

## Decision
- `Result` / `Result<T>` and `Error(Code, Message, Field?)` in `Application/Common`. Services return them for anything a user can cause.
- Kept from FlowOps: `ErrorCodes` (stable `area.snake_case` codes), the single code → status table (`ErrorStatusCodes`), ProblemDetails with `code` + `traceId`, and the global exception handler, which now covers only `DomainException` (entity invariants) and bugs (500).
- `ResultExtensions.ToActionResult / ToCreatedResult` in the Api is the only place a Result becomes HTTP. Controllers never inspect `IsSuccess`.

## Consequences
+ Failure paths are visible in signatures; no exceptions for control flow; easy to unit-test.
+ The HTTP shape is identical whether the error came from a Result or a DomainException.
- Two carriers for errors (Result in services, DomainException in entities); the rule for which to use is in backend-conventions.md.

## Alternatives rejected
Exceptions only (FlowOps style): rejected because the brief requires a Result type. A third-party Result library: not needed for ~60 lines.
