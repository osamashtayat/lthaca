import { ApiError, NETWORK_ERROR_CODE } from './apiError'

/**
 * User-facing text for every error `code` the API can return. The code list lives in
 * `Ithaca.Domain/Errors/ErrorCodes.cs`; a backend test fails if a code exists there without a message here.
 * Say what happened and what to do next.
 */
export const PROBLEM_MESSAGES = {
  validation: 'Some of that information is not valid. Check it and try again.',
  not_found: "We couldn't find that.",
  conflict: 'That changed in the meantime. Refresh and try again.',
  'server.error': 'Something went wrong on our side. Try again in a moment.',
  'film.not_found': "This film isn't in our catalog yet.",
  'server.not_implemented': "This part of Ithaca isn't ready yet. Check back soon.",
  'auth.antiforgery_failed': 'Your session needs a refresh. Reload the page and try again.',
  'auth.unauthenticated': 'Sign in to keep going.',
  'auth.invalid_credentials': "That email and password don't match. Try again.",
  'auth.forbidden': "You don't have access to that.",
  'auth.email_taken': 'That email already has an account. Sign in instead.',
  'deck.not_found': "We couldn't find that deck. Start a new one.",
  'deck.item_not_found': "That card isn't in this deck anymore. Refresh and keep swiping.",
  'deck.already_swiped': 'You already swiped this one.',
  'deck.no_yeses': 'Swipe right on a few films first, then see your top picks.',
  'room.not_found': "We couldn't find that room. Check the code or ask for a new link.",
  'room.not_member': 'Join this room first.',
  'room.not_host': 'Only the host can do that.',
  'room.already_voted': 'You already voted on this one.',
  'room.closed': 'This room is closed. Ask the host for a new one.',
  'room.full': 'This room is full.',
  'room.invalid_state': 'The room has moved on. Refresh to catch up.',
  'room.no_result': "There's no result yet. Keep swiping.",
  'mood.unclear': 'We couldn\'t find a mood in that. Try something like "funny and under 2 hours", or tap a mood above.',
  'ai.invalid_output': "We couldn't come up with picks this time. Try again.",
  'ai.unavailable': 'Our movie brain is taking a break. Try again in a minute.',
  'tmdb.unavailable': "We can't load film details right now. Try again in a moment.",
  'wikidata.unavailable': "Film facts aren't available right now. Try again later.",
  'ai.refused': "We can't help with that request. Try describing your mood differently.",
} as const satisfies Record<string, string>

export type ProblemCode = keyof typeof PROBLEM_MESSAGES

const NETWORK_MESSAGE = "Can't reach Ithaca. Check your connection and try again."
const GENERIC_MESSAGE = 'Something went wrong. Try again.'

function isKnownCode(code: string): code is ProblemCode {
  return Object.hasOwn(PROBLEM_MESSAGES, code)
}

/** The text to show a user for any thrown value. Mapped from `code`, with a status-based fallback. Never the server's `detail`. */
export function toUserMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return GENERIC_MESSAGE
  if (error.code === NETWORK_ERROR_CODE) return NETWORK_MESSAGE
  if (isKnownCode(error.code)) return PROBLEM_MESSAGES[error.code]
  if (error.status === 404) return PROBLEM_MESSAGES.not_found
  if (error.status >= 500) return PROBLEM_MESSAGES['server.error']
  return GENERIC_MESSAGE
}

/** The first server message for a form field from a 400 response, or undefined. */
export function serverFieldError(error: unknown, field: string): string | undefined {
  if (!(error instanceof ApiError)) return undefined
  const key = Object.keys(error.fieldErrors).find((name) => name === field || name.startsWith(`${field}.`))
  return key ? error.fieldErrors[key]?.[0] : undefined
}
