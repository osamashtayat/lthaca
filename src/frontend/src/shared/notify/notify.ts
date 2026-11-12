import { toast } from 'sonner'
import { toUserMessage } from '@/shared/api/problem'

/**
 * The one toast helper. Components call `notify.*`; they never import the toast library (lint enforces it),
 * so the library can be swapped here alone.
 */
export const notify = {
  success: (message: string) => toast.success(message),
  info: (message: string) => toast(message),
  /** Errors stay until dismissed. */
  error: (message: string) => toast.error(message, { duration: Infinity }),
  /** For a caught API error: shows the mapped user message, never the server's raw detail. */
  apiError: (error: unknown) => toast.error(toUserMessage(error), { duration: Infinity }),
}
