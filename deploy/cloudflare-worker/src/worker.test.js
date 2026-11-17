// Run: node --test deploy/cloudflare-worker   (Node 22+, no dependencies)
import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'
import worker, { isForwarded, toOriginRequest } from './worker.js'

const ORIGIN = 'https://origin.ithacamovies.com'
const realFetch = globalThis.fetch
afterEach(() => {
  globalThis.fetch = realFetch
})

test('only /api/ and /hubs/ are forwarded', () => {
  assert.equal(isForwarded('/api/health'), true)
  assert.equal(isForwarded('/hubs/rooms'), true)
  assert.equal(isForwarded('/films/550'), false)
  assert.equal(isForwarded('/apix'), false)
})

test('path, query, method, body, cookies and the XSRF header reach the origin unchanged', async () => {
  const incoming = new Request('https://ithacamovies.com/api/rooms/ABC/votes?x=1&y=2', {
    method: 'POST',
    headers: {
      Cookie: 'ithaca_session=s; ithaca_af=a',
      'X-XSRF-TOKEN': 'token-123',
      'Content-Type': 'application/json',
    },
    body: '{"filmId":550}',
  })
  const out = toOriginRequest(incoming, ORIGIN)
  assert.equal(out.url, 'https://origin.ithacamovies.com/api/rooms/ABC/votes?x=1&y=2')
  assert.equal(out.method, 'POST')
  assert.equal(out.headers.get('Cookie'), 'ithaca_session=s; ithaca_af=a')
  assert.equal(out.headers.get('X-XSRF-TOKEN'), 'token-123')
  assert.equal(out.redirect, 'manual')
  assert.equal(await out.text(), '{"filmId":550}')
})

test('X-Forwarded-For is the Cloudflare client IP and a spoofed value is dropped', () => {
  const spoofed = new Request('https://ithacamovies.com/api/auth/login', {
    headers: { 'CF-Connecting-IP': '203.0.113.7', 'X-Forwarded-For': '1.2.3.4' },
  })
  const out = toOriginRequest(spoofed, ORIGIN)
  assert.equal(out.headers.get('X-Forwarded-For'), '203.0.113.7')
  assert.equal(out.headers.get('X-Forwarded-Proto'), 'https')

  const noIp = toOriginRequest(new Request('https://ithacamovies.com/api/x', { headers: { 'X-Forwarded-For': '1.2.3.4' } }), ORIGIN)
  assert.equal(noIp.headers.get('X-Forwarded-For'), null)
})

test('WebSocket upgrade headers are passed through and the origin response (a 101 in production) is returned as is', async () => {
  let seen
  const upgraded = new Response(null, { status: 200, headers: { 'X-Origin': 'yes' } })
  globalThis.fetch = async (request) => {
    seen = request
    return upgraded
  }
  const incoming = new Request('https://ithacamovies.com/hubs/rooms?id=abc', {
    headers: { Upgrade: 'websocket', Connection: 'Upgrade', 'Sec-WebSocket-Key': 'k', Cookie: 'ithaca_session=s' },
  })
  const response = await worker.fetch(incoming, { ORIGIN })
  assert.equal(response, upgraded)
  assert.equal(seen.url, 'https://origin.ithacamovies.com/hubs/rooms?id=abc')
  assert.equal(seen.headers.get('Upgrade'), 'websocket')
  assert.equal(seen.headers.get('Sec-WebSocket-Key'), 'k')
  assert.equal(seen.headers.get('Cookie'), 'ithaca_session=s')
})

test('other paths get 404 and a missing ORIGIN gets 500, without calling the origin', async () => {
  globalThis.fetch = async () => assert.fail('origin must not be called')
  assert.equal((await worker.fetch(new Request('https://ithacamovies.com/films/550'), { ORIGIN })).status, 404)
  assert.equal((await worker.fetch(new Request('https://ithacamovies.com/api/health'), {})).status, 500)
})
