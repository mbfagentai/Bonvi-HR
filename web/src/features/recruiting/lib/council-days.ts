import type { CouncilDay } from '../api/council-api'

/** The day to open with: the nearest coming day with invitations, else the latest past one, else today. */
export function defaultCouncilDay(days: readonly CouncilDay[], today: string): string {
  const coming = days.find((day) => day.date >= today && day.invited > 0)
  if (coming) return coming.date

  return days.filter((day) => day.date < today).at(-1)?.date ?? today
}

/** Today in Tashkent as `yyyy-MM-dd`. */
export function tashkentToday(now: Date = new Date()): string {
  return new Date(now.getTime() + 5 * 60 * 60 * 1000).toISOString().slice(0, 10)
}
