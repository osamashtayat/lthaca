/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The dev proxy keeps the SPA same-origin with the API (no CORS), exactly like nginx in Docker.
// Override the target with ITHACA_API_URL if your port differs.
const apiTarget = process.env.ITHACA_API_URL ?? 'http://localhost:5280'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      // Repo-level contracts (realtime event names) shared with the backend.
      '@contracts': fileURLToPath(new URL('../../contracts', import.meta.url)),
    },
  },
  server: {
    port: 5273,
    fs: { allow: ['.', '../../contracts'] },
    proxy: {
      '/api': { target: apiTarget, changeOrigin: false },
      '/hubs': { target: apiTarget, changeOrigin: false, ws: true },
    },
  },
  test: {
    environment: 'node',
  },
})
