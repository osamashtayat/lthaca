// Cloudflare Worker on ithacamovies.com/api/* and ithacamovies.com/hubs/* (ADR-0030, "B1").
// Forwards each request to the VPS origin unchanged: method, path, query, body, cookies, X-XSRF-TOKEN and WebSocket
// upgrades. It sets exactly one thing: the client's address (X-Forwarded-For from CF-Connecting-IP, and
// X-Forwarded-Proto), so per-IP rate limits see real users (issue #74). No auth, caching or response rewriting.

const FORWARDED_PREFIXES = ['/api/', '/hubs/']

/** True for the paths this Worker owns; the routes in wrangler.toml should never send it anything else. */
export function isForwarded(pathname) {
  return FORWARDED_PREFIXES.some((prefix) => pathname.startsWith(prefix))
}

/** Builds the request sent to the origin. Pure, so the test can check it without a network. */
export function toOriginRequest(request, origin) {
  const url = new URL(request.url)
  const target = new URL(url.pathname + url.search, origin)
  const headers = new Headers(request.headers)
  // Overwrite, never append: a client-supplied X-Forwarded-For must not reach the API.
  const clientIp = request.headers.get('CF-Connecting-IP')
  if (clientIp) {
    headers.set('X-Forwarded-For', clientIp)
  } else {
    headers.delete('X-Forwarded-For')
  }
  headers.set('X-Forwarded-Proto', 'https')
  const hasBody = request.method !== 'GET' && request.method !== 'HEAD'
  return new Request(target, {
    method: request.method,
    headers,
    body: hasBody ? request.body : undefined,
    // Redirects and Set-Cookie go back to the browser as the API sent them.
    redirect: 'manual',
    duplex: 'half',
  })
}

export default {
  async fetch(request, env) {
    if (!env.ORIGIN) {
      return new Response('Worker misconfigured: ORIGIN is not set.', { status: 500 })
    }
    if (!isForwarded(new URL(request.url).pathname)) {
      return new Response('Not found', { status: 404 })
    }
    // The origin's response, including a 101 WebSocket upgrade, is returned as is.
    return fetch(toOriginRequest(request, env.ORIGIN))
  },
}
