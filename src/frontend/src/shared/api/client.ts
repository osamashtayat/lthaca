import createClient, { type Middleware } from 'openapi-fetch'
import { ApiError, NETWORK_ERROR_CODE, apiErrorFromResponse } from './apiError'
import type { paths } from './schema'

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE'])
export const XSRF_COOKIE = 'XSRF-TOKEN'
export const XSRF_HEADER = 'X-XSRF-TOKEN'

/** The antiforgery token the server put in the JS-readable `XSRF-TOKEN` cookie (api-contract §4), if any. */
export function readXsrfToken(cookieString: string | undefined = globalThis.document?.cookie): string | undefined {
  if (!cookieString) return undefined
  for (const part of cookieString.split(';')) {
    const [name, ...rest] = part.trim().split('=')
    if (name === XSRF_COOKIE) return decodeURIComponent(rest.join('='))
  }
  return undefined
}

/** Echoes the XSRF cookie in `X-XSRF-TOKEN` on every unsafe method (POST/PUT/PATCH/DELETE). No endpoint is exempt. */
const xsrfMiddleware: Middleware = {
  onRequest({ request }) {
    if (SAFE_METHODS.has(request.method.toUpperCase())) return undefined
    const token = readXsrfToken()
    if (token) request.headers.set(XSRF_HEADER, token)
    return request
  },
}

/** Turns every non-2xx response into a thrown ApiError, so callers (TanStack Query) just `await`. */
const problemMiddleware: Middleware = {
  async onResponse({ response }) {
    if (!response.ok) throw await apiErrorFromResponse(response)
    return response
  },
}

/** The only place in the app that calls the global `fetch`. */
async function fetchOrThrowApiError(request: Request): Promise<Response> {
  try {
    return await fetch(request)
  } catch {
    // fetch only rejects when no response arrived at all.
    throw new ApiError({ status: 0, code: NETWORK_ERROR_CODE })
  }
}

export function createApiClient() {
  const client = createClient<paths>({
    // Same origin (the Vite proxy / nginx forwards /api). An absolute base keeps `Request` valid outside the browser.
    baseUrl: globalThis.location?.origin ?? 'http://localhost',
    credentials: 'same-origin',
    fetch: fetchOrThrowApiError,
  })
  client.use(xsrfMiddleware, problemMiddleware)
  return client
}

/**
 * Typed client generated from contracts/openapi.json (`npm run api:types`). Resolves with `data`;
 * rejects with `ApiError`. Use it ONLY inside feature `api/` hooks, never in components.
 */
export const api = createApiClient()
