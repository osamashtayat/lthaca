import { LegalPage, type LegalItem } from './LegalPage'

const items: LegalItem[] = [
  {
    title: 'What we keep.',
    body: (
      <p>
        Your email, display name, and what you do in the app: swipes, ratings, your watchlist, imports. Guests only give a
        display name.
      </p>
    ),
  },
  {
    title: 'Why.',
    body: <p>To run your account, build your decks and room results, and send password-reset emails. Nothing else.</p>,
  },
  {
    title: 'No selling, no ads.',
    body: (
      <p>We don't sell your data or show ads. We use only the cookies needed to keep you signed in.</p>
    ),
  },
  {
    title: 'Services we use.',
    body: (
      <>
        <p>These help us run Ithaca, and each gets only what it needs:</p>
        <ul className="ml-5 list-disc">
          <li>an AI provider for mood text and pick reasons, with no account details attached;</li>
          <li>an email provider for password resets;</li>
          <li>an error-tracking service that sees an internal id, never your name or email.</li>
        </ul>
      </>
    ),
  },
  {
    title: 'Deleting.',
    body: (
      <p>
        Delete your account in Profile and your data goes with it. A guest's data goes when they leave the room.
      </p>
    ),
  },
  { title: 'Where.', body: <p>Our servers are in [country].</p> },
  { title: 'Contact.', body: <p>Questions or requests: [email]</p> },
]

/** `/privacy`: the Privacy Policy (L5). */
export function PrivacyPage() {
  return <LegalPage title="Privacy Policy" items={items} />
}
