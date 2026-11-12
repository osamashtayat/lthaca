import type { components } from '@/shared/api'

export type CurrentUser = components['schemas']['CurrentUserDto']

/** A signed-in `User`. A guest (`kind: "guest"`) is signed out as far as user-only routes are concerned. */
export function isUser(user: CurrentUser | null | undefined): user is CurrentUser {
  return user?.kind === 'user'
}
