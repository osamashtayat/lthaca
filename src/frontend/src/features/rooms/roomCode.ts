/** Codes are case-insensitive (`pop-428` = `POP-428`); the URL, the cache keys and the hub use this one spelling. */
export const normalizeRoomCode = (code: string): string => code.trim().toUpperCase()

export const MODE_LABEL = { justSwipe: 'Just swipe', mood: 'By mood' } as const

export const MODE_OPTIONS = [
  { value: 'justSwipe', label: MODE_LABEL.justSwipe },
  { value: 'mood', label: MODE_LABEL.mood },
] as const

/** "2 OF 3 READY": what the host sees on the start button. */
export const readyCount = (members: readonly { isReady: boolean }[]) =>
  `${members.filter((m) => m.isReady).length} OF ${members.length} READY`
