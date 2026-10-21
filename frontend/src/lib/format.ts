import type { DiscountType, Money, OrderStatus } from '../api/types.ts'

const moneyFormats = new Map<string, Intl.NumberFormat>()

export function formatMoney(money: Money): string {
  let format = moneyFormats.get(money.currency)
  if (!format) {
    format = new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency: money.currency,
      minimumFractionDigits: 0,
      maximumFractionDigits: 2,
    })
    moneyFormats.set(money.currency, format)
  }
  return format.format(money.amount)
}

/** "yyyy-MM-dd" as a local calendar date (not UTC midnight, which can shift the day). */
export function parseDate(iso: string): Date {
  const [year, month, day] = iso.split('-').map(Number)
  return new Date(year, month - 1, day)
}

export function toIsoDate(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

export const todayIso = () => toIsoDate(new Date())

export function addDays(iso: string, days: number): string {
  const date = parseDate(iso)
  date.setDate(date.getDate() + days)
  return toIsoDate(date)
}

const dateFormat = new Intl.DateTimeFormat('en-GB', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' })
const dateTimeFormat = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' })
const weekdayFormat = new Intl.DateTimeFormat('en-GB', { weekday: 'short' })
const dayMonthFormat = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short' })

export const formatDate = (iso: string) => dateFormat.format(parseDate(iso))
export const formatDateTime = (iso: string) => dateTimeFormat.format(new Date(iso))
export const formatWeekday = (iso: string) => weekdayFormat.format(parseDate(iso))
export const formatDayMonth = (iso: string) => dayMonthFormat.format(parseDate(iso))

export const statusLabels: Record<OrderStatus, string> = {
  New: 'New',
  InProgress: 'In progress',
  WaitingForCustomer: 'Waiting for customer',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
}

export const allStatuses = Object.keys(statusLabels) as OrderStatus[]

export function formatDiscount(type: DiscountType, value: number, currency = 'NOK'): string {
  return type === 'Percentage' ? `${value} %` : `${formatMoney({ amount: value, currency })} off`
}

export function formatMinutes(minutes: number): string {
  if (minutes < 60) return `${minutes} min`
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  return rest ? `${hours} h ${rest} min` : `${hours} h`
}

export function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0].toUpperCase())
    .join('')
}
