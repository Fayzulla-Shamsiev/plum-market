import type { DeliveryType, OrderStatus, PaymentMethod, Platform } from './api'

const num = new Intl.NumberFormat('ru-RU')

export const money = (v: number) => `${num.format(Math.round(v))} сум`
export const count = (v: number) => num.format(v)

/** Compact form for chart axes: 1,2 млн / 350 тыс. */
export function compactMoney(v: number) {
  const abs = Math.abs(v)
  if (abs >= 1_000_000) return `${(v / 1_000_000).toLocaleString('ru-RU', { maximumFractionDigits: 1 })} млн`
  if (abs >= 1_000) return `${Math.round(v / 1_000)} тыс`
  return String(v)
}

export function dateTime(iso: string) {
  const d = new Date(iso)
  return d.toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export function date(iso: string) {
  return new Date(iso).toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

/** "5 мин назад", "3 ч назад", otherwise a date. */
export function relative(iso: string) {
  const mins = Math.round((Date.now() - new Date(iso).getTime()) / 60000)
  if (mins < 1) return 'только что'
  if (mins < 60) return `${mins} мин назад`
  if (mins < 24 * 60) return `${Math.round(mins / 60)} ч назад`
  return date(iso)
}

/** Percent change vs previous period; null when there's nothing to compare to. */
export function delta(cur: number, prev: number): number | null {
  if (!prev) return null
  return ((cur - prev) / prev) * 100
}

export const toIsoDate = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

export const statusLabel: Record<OrderStatus, string> = {
  New: 'Новый',
  InProgress: 'В процессе',
  Overdue: 'Просрочен',
  Ready: 'Готов',
  OnTheWay: 'В пути',
  Completed: 'Выполнен',
  Cancelled: 'Отменён',
}

/** Allowed next steps in the order workflow, in the order they're offered. */
export const nextStatuses: Record<OrderStatus, OrderStatus[]> = {
  New: ['InProgress', 'Cancelled'],
  InProgress: ['Ready', 'Cancelled'],
  Overdue: ['InProgress', 'Ready', 'Cancelled'],
  Ready: ['OnTheWay', 'Completed', 'Cancelled'],
  OnTheWay: ['Completed', 'Cancelled'],
  Completed: [],
  Cancelled: ['New'],
}

export const paymentLabel: Record<PaymentMethod, string> = {
  Cash: 'Наличные',
  CardToCard: 'Перевод на карту',
  Click: 'Click',
  Payme: 'Payme',
}

export const deliveryLabel: Record<DeliveryType, string> = {
  Delivery: 'Доставка',
  Pickup: 'Самовывоз',
}

export const platformLabel: Record<Platform, string> = {
  Telegram: 'Telegram',
  Website: 'Веб-сайт',
  Instagram: 'Instagram',
}

export const languageLabel: Record<string, string> = {
  ru: 'Русский',
  uz: "O'zbek",
  en: 'English',
}

/** Categorical chart slots (validated default palette, fixed order). */
export const series = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4', '#008300', '#4a3aa7', '#e34948']

export const platformColor: Record<Platform, string> = {
  Telegram: series[0],
  Website: series[1],
  Instagram: series[2],
}

export const catalogLangs = ['ru', 'uz', 'oz'] as const
export const catalogLangLabel: Record<string, string> = { ru: 'Русский', uz: "O'zbekcha", oz: 'Ўзбекча' }

/** Russian value, else any filled language. */
export function loc(l: Partial<Record<string, string>> | null | undefined): string {
  if (!l) return ''
  return l.ru || l.uz || l.oz || Object.values(l).find(Boolean) || ''
}

export const stockStatusLabel: Record<string, string> = {
  Unlimited: 'Безлимитный',
  Limited: 'Ограничено',
  OutOfStock: 'Нет в наличии',
}

export const channelLabel: Record<string, string> = {
  Telegram: 'Telegram',
  Instagram: 'Instagram',
  Website: 'Веб-сайт',
  Wolt: 'Wolt',
}

export const units = ['шт', 'кг', 'г', 'л', 'мл', 'порция', 'упак']

export function timeShort(iso: string) {
  const d = new Date(iso)
  const today = new Date()
  if (d.toDateString() === today.toDateString()) return d.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })
  const y = new Date(today)
  y.setDate(y.getDate() - 1)
  if (d.toDateString() === y.toDateString()) return 'вчера'
  return d.toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit' })
}
