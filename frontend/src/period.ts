import { toIsoDate } from './format'

export interface Period { preset: string; from: string; to: string }

/** Rolling windows ending today (a "month" is the last 30 days, etc.). */
export function periodFor(preset: string): Period {
  const to = new Date()
  const from = new Date()
  const back: Record<string, number> = { today: 0, week: 6, month: 29, quarter: 89, year: 364 }
  from.setDate(from.getDate() - (back[preset] ?? 29))
  return { preset, from: toIsoDate(from), to: toIsoDate(to) }
}
