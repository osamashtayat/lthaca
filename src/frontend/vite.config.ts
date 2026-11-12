/// <reference types="vitest/config" />
import { rmSync } from 'node:fs'
import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vite'

// The dev proxy keeps the SPA same-origin with the API (no CORS), exactly like nginx in Docker.
// Override the target with ITHACA_API_URL if your port differs.
const apiTarget = process.env.ITHACA_API_URL ?? 'http://localhost:5280'

/**
 * The MSW worker lives in public/ so the dev server can serve it (ADR-0020); production builds must not ship it.
 * scripts/check-no-mocks.mjs then fails the build if any mock code reached dist/.
 */
function dropMockServiceWorker(): Plugin {
  let outDir = 'dist'
  return {
    name: 'ithaca:drop-mock-service-worker',
    apply: 'build',
    configResolved(config) {
      outDir = config.build.outDir
    },
    closeBundle() {
      rmSync(fileURLToPath(new URL(`./${outDir}/mockServiceWorker.js`, import.meta.url)), { force: true })
    },
  }
}

export default defineConfig({
  plugins: [react(), tailwindcss(), dropMockServiceWorker()],
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
    // Starts the MSW node server with the same handlers as the dev server; unhandled requests fail (ADR-0020).
    setupFiles: ['./src/test/setup.ts'],
  },
})
