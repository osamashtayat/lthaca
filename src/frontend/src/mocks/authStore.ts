import type { components } from '@/shared/api'

type CurrentUserDto = components['schemas']['CurrentUserDto']

/** Mock accounts: sample content for dev and tests. The rules in handlers.ts (password 8–128, name 1–40, taken email) mirror api-contract §4. */
export const MOCK_USER: CurrentUserDto = {
  kind: 'user',
  id: '0190c4a2-0000-7000-8000-000000000001',
  displayName: 'Lina',
  email: 'lina@example.com',
  roomCode: null,
}
export const MOCK_PASSWORD = 'password123'
export const MOCK_GUEST: CurrentUserDto = {
  kind: 'guest',
  id: '0190c4a2-0000-7000-8000-000000000002',
  displayName: 'Guest',
  email: null,
  roomCode: 'ABCD',
}

const SESSION_KEY = 'ithaca.mock.session'

type Account = { user: CurrentUserDto; password: string }
const initialAccounts = (): Map<string, Account> =>
  new Map([[MOCK_USER.email ?? '', { user: MOCK_USER, password: MOCK_PASSWORD }]])

let accounts = initialAccounts()
let session: CurrentUserDto | null = readSession()

function readSession(): CurrentUserDto | null {
  try {
    const raw = globalThis.sessionStorage?.getItem(SESSION_KEY)
    return raw === null || raw === undefined ? MOCK_USER : (JSON.parse(raw) as CurrentUserDto | null)
  } catch {
    return MOCK_USER
  }
}

function writeSession(value: CurrentUserDto | null) {
  session = value
  try {
    // Survives a page reload in the dev server (the mock worker restarts with the page).
    globalThis.sessionStorage?.setItem(SESSION_KEY, JSON.stringify(value))
  } catch {
    // No storage (private mode, node): the in-memory value is enough.
  }
}

/** Signed in as the mock user by default; tests switch it with `setMockSession(null | MOCK_GUEST | …)`. */
export function getMockSession(): CurrentUserDto | null {
  return session
}

export function setMockSession(value: CurrentUserDto | null) {
  writeSession(value)
}

export function findMockAccount(email: string, password: string): CurrentUserDto | undefined {
  const account = accounts.get(email.trim().toLowerCase())
  return account && account.password === password ? account.user : undefined
}

export function isMockEmailTaken(email: string): boolean {
  return accounts.has(email.trim().toLowerCase())
}

export function addMockAccount(email: string, password: string, displayName: string): CurrentUserDto {
  const user: CurrentUserDto = {
    kind: 'user',
    id: `0190c4a2-0000-7000-8000-${String(accounts.size + 100).padStart(12, '0')}`,
    displayName,
    email: email.trim().toLowerCase(),
    roomCode: null,
  }
  accounts.set(user.email ?? '', { user, password })
  return user
}

/** Called from src/test/setup.ts after every test. */
export function resetMockAuth() {
  accounts = initialAccounts()
  try {
    globalThis.sessionStorage?.removeItem(SESSION_KEY)
  } catch {
    // ignore
  }
  session = MOCK_USER
}
