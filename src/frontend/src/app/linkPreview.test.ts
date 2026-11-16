import { describe, expect, it } from 'vitest'
import html from '../../index.html?raw'

/** Files in public/, e.g. 'og-image.png' (keys look like '../../public/og-image.png'). */
const published = Object.keys(import.meta.glob('../../public/*', { query: '?url' })).map((k) => k.split('/').pop())

/** The first <meta> whose attribute (property|name) equals `key`, returning its content. */
function meta(key: string) {
  const re = new RegExp(`<meta[^>]*(?:property|name)="${key}"[^>]*content="([^"]*)"`)
  return re.exec(html)?.[1]
}

describe('link preview tags in index.html', () => {
  it.each(['og:title', 'og:description', 'og:image', 'og:site_name', 'twitter:card', 'description'])(
    'has %s',
    (key) => expect(meta(key)).toBeTruthy(),
  )

  it('points og:image at an absolute URL of a file that ships in public/', () => {
    const image = meta('og:image') ?? ''
    expect(image).toMatch(/^https:\/\//)
    const file = new URL(image).pathname.slice(1)
    expect(published).toContain(file)
  })

  it('ships the app icons', () => {
    expect(html).toContain('href="/apple-touch-icon.png"')
    for (const f of ['apple-touch-icon.png', 'icon-192.png', 'icon-512.png', 'favicon.svg'])
      expect(published).toContain(f)
  })
})
