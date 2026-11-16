/**
 * `/dev/crash` (dev server only, like /dev/status): throws while rendering, to check the crash screen and, with
 * `VITE_SENTRY_DSN` set, that the error reaches Sentry (ADR-0028). Production builds don't contain it.
 */
export function CrashTestPage(): never {
  throw new Error(`Sentry test error ${Date.now()}`)
}
