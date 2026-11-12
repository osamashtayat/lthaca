import { Link } from 'react-router'

export function NotFoundPage() {
  return (
    <section className="flex flex-col items-start gap-3">
      <h1 className="font-display text-4xl">Lost the plot</h1>
      <p>That page doesn't exist.</p>
      <Link to="/" className="font-bold text-pink-text">
        Back to tonight
      </Link>
    </section>
  )
}
