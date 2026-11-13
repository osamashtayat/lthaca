const DAYS = ['SUNDAY', 'MONDAY', 'TUESDAY', 'WEDNESDAY', 'THURSDAY', 'FRIDAY', 'SATURDAY'] as const

/** "Good evening" by the hour of the visitor's own clock. */
export function greetingFor(date: Date): string {
  const hour = date.getHours()
  if (hour >= 5 && hour < 12) return 'Good morning'
  if (hour >= 12 && hour < 18) return 'Good afternoon'
  return 'Good evening'
}

/** "FRIDAY NIGHT" style label: the weekday plus morning / afternoon / night. */
export function dayLabelFor(date: Date): string {
  const hour = date.getHours()
  const part = hour >= 5 && hour < 12 ? 'MORNING' : hour >= 12 && hour < 18 ? 'AFTERNOON' : 'NIGHT'
  return `${DAYS[date.getDay()]} ${part}`
}
