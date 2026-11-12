import { HttpResponse } from 'msw'
import type { ProblemCode, paths } from '@/shared/api'

type Method = 'get' | 'post' | 'put' | 'patch' | 'delete'

/**
 * The JSON body the contract declares for `path` + `method` + `status`, straight from the generated schema.d.ts.
 * Handlers type their responses with it, so a contract change breaks the mock at compile time instead of drifting.
 */
export type ResponseOf<P extends keyof paths, M extends Method, S extends number> = paths[P][M] extends {
  responses: { [K in S]: { content: { 'application/json': infer Body } } }
}
  ? Body
  : never

/** The JSON request body the contract declares for `path` + `method`. */
export type RequestOf<P extends keyof paths, M extends Method> = paths[P][M] extends {
  requestBody?: { content: { 'application/json': infer Body } }
}
  ? Body
  : never

/**
 * MSW pattern for a contract path: `/api/films/{tmdbId}` → `* /api/films/:tmdbId` (without the space).
 * The leading `*` matches any origin, so the same handler works in the browser and in Vitest (node has no location).
 */
export function apiPath<P extends keyof paths>(path: P): string {
  return `*${path.replace(/\{(\w+)\}/g, ':$1')}`
}

/**
 * An RFC 9457 ProblemDetails reply shaped like the API's (`code`, `traceId`, optional `errors`). `code` must exist in
 * problem.ts, so a mock can't invent an error the backend doesn't have.
 */
export function problem(status: number, code: ProblemCode, fieldErrors?: Record<string, string[]>) {
  return HttpResponse.json(
    { type: null, title: code, status, detail: null, code, traceId: 'mock-trace', ...(fieldErrors ? { errors: fieldErrors } : {}) },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  )
}
