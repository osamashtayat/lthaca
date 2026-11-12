#!/usr/bin/env node
/**
 * Fails if components use colours outside the design tokens in src/shared/styles/tokens.css.
 * Rationale and rules: docs/frontend-conventions.md (Design tokens).
 *
 * Checks every .ts/.tsx file under src/ (except generated API types) for:
 *  - hex colour literals (#abc, #aabbcc, #aabbccdd)
 *  - rgb()/rgba()/hsl()/hsla()/oklch() colour functions
 *  - Tailwind arbitrary colour values like bg-[#fff] or text-[rgb(…)]
 *  - inline style colour properties (style={{ color: … }})
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'

const root = new URL('../src/', import.meta.url).pathname
const ignored = new Set(['shared/api/schema.d.ts'])

const checks = [
  { name: 'hex colour literal', re: /(?<![\w&])#[0-9a-fA-F]{3,8}\b/g },
  { name: 'colour function', re: /\b(?:rgba?|hsla?|oklch|oklab)\s*\(/g },
  { name: 'arbitrary colour class', re: /\b(?:bg|text|border|ring|outline|fill|stroke|from|to|via|shadow|decoration|accent|caret|divide)-\[(?:#|rgb|hsl|oklch|color:)/g },
  { name: 'inline colour style', re: /style=\{\{[^}]*\b(?:color|background|backgroundColor|borderColor)\s*:/g },
  // Tailwind's default palette is disabled in tokens.css, so these classes silently do nothing.
  // `yellow` and `pink` (no shade number) are Ithaca tokens and allowed; `yellow-400` etc. are not.
  {
    name: 'default Tailwind palette class (disabled — use an Ithaca token)',
    re: /\b(?:bg|text|border|ring|outline|fill|stroke|from|to|via|decoration|accent|caret|divide|placeholder)-(?:(?:slate|gray|zinc|stone|red|orange|amber|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|rose)(?:-\d{2,3})?|(?:yellow|pink)-\d{2,3}|neutral-\d{2,3}|black|white)\b/g,
  },
]

function* walk(dir) {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry)
    if (statSync(full).isDirectory()) yield* walk(full)
    else if (/\.(ts|tsx)$/.test(entry)) yield full
  }
}

const problems = []
for (const file of walk(root)) {
  const rel = relative(root, file)
  if (ignored.has(rel)) continue
  const lines = readFileSync(file, 'utf8').split('\n')
  lines.forEach((line, i) => {
    for (const { name, re } of checks) {
      re.lastIndex = 0
      if (re.test(line)) problems.push(`src/${rel}:${i + 1}  ${name}: ${line.trim()}`)
    }
  })
}

if (problems.length > 0) {
  console.error('Design-token check failed. Use theme tokens from src/shared/styles/tokens.css instead:\n')
  for (const p of problems) console.error('  ' + p)
  process.exit(1)
}
console.log('Design-token check passed.')
