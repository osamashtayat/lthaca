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
  'ai.invalid_output': "We couldn't come up with picks this time. Try again.",
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
