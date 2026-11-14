// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { queryKeys } from '@/shared/query'
import { useUpdateUserFilm } from './api/useUpdateUserFilm'

vi.mock('@/shared/notify', () => ({ notify: { apiError: vi.fn<(e?: unknown) => void>() } }))

describe('library changes and By taste', () => {
  it('re-reads the taste answer after a like or rating, since those are what it is built from', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
    const spy = vi.spyOn(client, 'invalidateQueries')
    const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>
    const { result } = renderHook(() => useUpdateUserFilm(550), { wrapper })
    result.current.mutate({ liked: true, onWatchlist: false, seen: true, rating: 4 })
    await waitFor(() => expect(spy).toHaveBeenCalledWith({ queryKey: queryKeys.taste.all }))
  })
})
