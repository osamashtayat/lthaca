import { LegalPage, type LegalItem } from './LegalPage'

const items: LegalItem[] = [
  {
    title: 'What Ithaca is.',
    body: <p>A free site that helps you pick a film, alone or with friends. Run by osama.</p>,
  },
  {
    title: 'Accounts.',
    body: <p>Solo decks need an account. Keep your password safe. You can delete your account anytime in Profile.</p>,
  },
  {
    title: 'Rooms.',
    body: <p>Anyone with a room link can join as a guest. Share links only with people you want in.</p>,
  },
  {
    title: 'Be decent.',
    body: <p>Don't misuse the site: no spam, scraping, attacks, or flooding it with requests.</p>,
  },
  {
    title: 'Film info.',
    body: (
      <p>
        Film data and images come from TMDB. This product uses the TMDB API but is not endorsed or certified by TMDB.
        Recommendations are suggestions, nothing more.
      </p>
    ),
  },
  {
    title: 'No guarantees.',
    body: <p>We provide Ithaca "as is" and may change or stop it at any time.</p>,
  },
  {
    title: 'Changes.',
    body: <p>We may update these terms. If you keep using Ithaca, you accept the new version.</p>,
  },
  { title: 'Contact.', body: <p>hello@ithacamovies.com</p> },
]

/** `/terms`: the Terms of Use (L5). */
export function TermsPage() {
  return <LegalPage title="Terms of Use" items={items} />
}
