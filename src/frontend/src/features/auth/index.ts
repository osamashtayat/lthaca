/** Public surface of the auth feature: the session hooks, the route guard, the Welcome page and the menu action. */
export { useCurrentUser } from './api/useCurrentUser'
export { RequireUser } from './components/RequireUser'
export { SessionAction } from './components/SessionAction'
export { SignInPage } from './pages/SignInPage'
export { isUser, type CurrentUser } from './types'
