/** "Ithaca" in Shrikhand with the pink offset shadow. The display font is for this and big moments only. */
export function Wordmark({ className = '' }: { className?: string }) {
  return <span className={`font-display leading-none [text-shadow:2px_2px_0_var(--color-pink)] ${className}`}>Ithaca</span>
}
