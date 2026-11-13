/** Where to go after signing in: the `next` query value when it is an in-app path, otherwise the home screen. */
export function safeNextPath(next: string | null | undefined): string {
  if (!next || !next.startsWith('/') || next.startsWith('//') || next.includes('\\')) return '/'
  return next
}
