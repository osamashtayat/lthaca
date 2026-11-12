#!/usr/bin/env node
/**
 * Fails the build if the mock API reached the production bundle (ADR-0020): the MSW worker file, or MSW's runtime
 * code. main.tsx loads the mocks only behind `import.meta.env.DEV`, so a hit here means that guard broke.
 */
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'

const dist = new URL('../dist/', import.meta.url).pathname
const markers = ['setupWorker', 'mockServiceWorker', 'MockApiFailedBanner', 'onUnhandledFrame']

if (!existsSync(dist)) {
  console.error('check-no-mocks: dist/ not found; run vite build first.')
  process.exit(1)
}

const problems = []
const walk = (dir) => {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) walk(path)
    else if (name === 'mockServiceWorker.js') problems.push(`${relative(dist, path)}: MSW worker file`)
    else if (/\.(js|html)$/.test(name)) {
      const text = readFileSync(path, 'utf8')
      for (const marker of markers) if (text.includes(marker)) problems.push(`${relative(dist, path)}: contains "${marker}"`)
    }
  }
}
walk(dist)

if (problems.length > 0) {
  console.error(`check-no-mocks: mock API code found in the production build:\n  ${problems.join('\n  ')}`)
  process.exit(1)
}
console.log('check-no-mocks: production build is free of mocks.')
