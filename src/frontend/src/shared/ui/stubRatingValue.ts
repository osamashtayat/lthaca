/** What a tap on stub `n` means: `n`, or `n - 0.5` when the rating is already `n` ("3.5" is tap 4, tap 4). */
export function nextRating(value: number | null, n: number): number {
  return value === n ? n - 0.5 : n
}
