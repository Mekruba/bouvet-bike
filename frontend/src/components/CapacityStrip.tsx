import type { DayCapacity } from '../api/types.ts'
import { cx } from '../lib/cx.ts'
import { formatDate, formatDayMonth, formatWeekday, todayIso } from '../lib/format.ts'

interface CapacityStripProps {
  days: DayCapacity[]
  selected?: string | null
  onSelect?: (date: string) => void
  /** Booking: full days can't be picked. Staff overview: every day can be clicked to filter. */
  allowFull?: boolean
}

/** One tile per day with a fill meter: the due date picker for customers and the capacity overview for staff. */
export function CapacityStrip({ days, selected, onSelect, allowFull }: CapacityStripProps) {
  const today = todayIso()

  return (
    <div className="capacity" role="group" aria-label="Due dates">
      {days.map((day) => {
        const fill = Math.min(100, Math.round((day.booked / day.capacity) * 100))
        return (
          <button
            key={day.date}
            type="button"
            className={cx(
              'capacity__day',
              day.isFull && 'is-full',
              !day.isFull && day.remaining <= 1 && 'is-busy',
              selected === day.date && 'is-selected',
              day.date === today && 'is-today',
            )}
            disabled={!onSelect || (day.isFull && !allowFull)}
            aria-pressed={selected === day.date}
            aria-label={`${formatDate(day.date)}: ${day.isFull ? 'fully booked' : `${day.remaining} of ${day.capacity} slots left`}`}
            onClick={() => onSelect?.(day.date)}
          >
            <span className="capacity__weekday">{formatWeekday(day.date)}</span>
            <span className="capacity__date">{formatDayMonth(day.date)}</span>
            <span className="capacity__meter" aria-hidden>
              <span className="capacity__meter-fill" style={{ width: `${fill}%` }} />
            </span>
            <span className="capacity__left">{day.isFull ? 'Full' : `${day.remaining} left`}</span>
          </button>
        )
      })}
    </div>
  )
}
