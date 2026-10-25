import type { DayCapacity } from '../api/types.ts'
import { cx } from '../lib/cx.ts'
import { formatDate, formatDayMonth, formatWeekday, todayIso } from '../lib/format.ts'

interface CapacityStripProps {
  days: DayCapacity[]
  selected?: string | null
  onSelect?: (date: string) => void
  /** Booking: full and closed days can't be picked. Staff: every day can be clicked. */
  allowFull?: boolean
  /** Booking: days before this date can't be picked, because the chosen service takes that long. */
  earliest?: string
  /** Staff view: booked/capacity, and a marker when a day deviates from its weekday default (e.g. "6/5"). */
  detailed?: boolean
}

/** One tile per day with a fill meter: the due date picker for customers and the capacity overview for staff. */
export function CapacityStrip({ days, selected, onSelect, allowFull, earliest, detailed }: CapacityStripProps) {
  const today = todayIso()

  return (
    <div className="capacity" role="group" aria-label="Due dates">
      {days.map((day) => {
        const fill = day.capacity === 0 ? 100 : Math.min(100, Math.round((day.booked / day.capacity) * 100))
        const tooSoon = earliest !== undefined && day.date < earliest
        return (
          <button
            key={day.date}
            type="button"
            className={cx(
              'capacity__day',
              day.isFull && 'is-full',
              day.isClosed && 'is-closed',
              tooSoon && 'is-too-soon',
              !day.isFull && day.remaining <= 1 && 'is-busy',
              selected === day.date && 'is-selected',
              day.date === today && 'is-today',
            )}
            disabled={!onSelect || (day.isFull && !allowFull) || tooSoon}
            aria-pressed={selected === day.date}
            aria-label={`${formatDate(day.date)}: ${tooSoon ? 'too soon for this service' : describe(day)}`}
            onClick={() => onSelect?.(day.date)}
          >
            <span className="capacity__weekday">{formatWeekday(day.date)}</span>
            <span className="capacity__date">{formatDayMonth(day.date)}</span>
            <span className="capacity__meter" aria-hidden>
              <span className="capacity__meter-fill" style={{ width: `${fill}%` }} />
            </span>
            <span className="capacity__left">
              {tooSoon
                ? 'Too soon'
                : day.isClosed
                  ? detailed && day.booked > 0
                    ? `Closed · ${day.booked} booked` // bikes booked before the day was closed stay
                    : 'Closed'
                  : detailed
                    ? `${day.booked}/${day.capacity} booked`
                    : day.isFull
                      ? 'Full'
                      : `${day.remaining} left`}
            </span>
            {detailed && day.isAdjusted && (
              <span className="capacity__adjusted" title={`Default for this weekday: ${day.defaultCapacity}`}>
                {day.capacity}/{day.defaultCapacity}
              </span>
            )}
          </button>
        )
      })}
    </div>
  )
}

function describe(day: DayCapacity): string {
  if (day.isClosed) return 'closed'
  const adjusted = day.isAdjusted ? ` (adjusted from ${day.defaultCapacity})` : ''
  return day.isFull
    ? `fully booked, ${day.booked} of ${day.capacity}${adjusted}`
    : `${day.remaining} of ${day.capacity} slots left${adjusted}`
}
