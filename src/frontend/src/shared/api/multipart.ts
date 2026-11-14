/**
 * Body options for a `multipart/form-data` upload with one file (openapi-fetch's own FormData handling then lets the
 * browser set the boundary). The generated schema types the field as a string, so the file is appended by the serializer.
 * (api-contract §13: the form field is `file`.) Usage: `api.POST('/api/imports', { body: {}, ...fileUpload('file', file) })`.
 */
export function fileUpload(field: string, file: File) {
  return {
    bodySerializer: () => {
      const form = new FormData()
      form.append(field, file)
      return form
    },
  }
}
