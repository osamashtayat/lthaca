import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, fileUpload, requireData } from '@/shared/api'
import { queryKeys } from '@/shared/query'
import type { ImportJob } from '../types'

/**
 * Uploads the Letterboxd ZIP (or one CSV from it) as `multipart/form-data`; the API client adds the XSRF header like
 * for every POST. The file is only handed over, never read here. The new job starts `queued`.
 */
export function useCreateImport() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (file: File): Promise<ImportJob> => {
      const { data } = await api.POST('/api/imports', { body: {}, ...fileUpload('file', file) })
      return requireData(data, 201)
    },
    onSuccess: (job) => {
      queryClient.setQueryData(queryKeys.imports.detail(job.id), job)
      void queryClient.invalidateQueries({ queryKey: queryKeys.imports.latest })
    },
  })
}
