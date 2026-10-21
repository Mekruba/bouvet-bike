import type { ReactNode } from 'react'
import { cx } from '../lib/cx.ts'
import { EmptyState, Loading } from './Feedback.tsx'

export interface Column<T> {
  key: string
  header: ReactNode
  render: (row: T) => ReactNode
  /** Makes the header a sort button; the value is passed to onSortChange. */
  sortKey?: string
  align?: 'left' | 'right'
}

export interface SortState {
  key: string
  descending: boolean
}

interface DataTableProps<T> {
  columns: Column<T>[]
  rows: T[] | undefined
  rowKey: (row: T) => string
  loading?: boolean
  onRowClick?: (row: T) => void
  sort?: SortState
  onSortChange?: (sort: SortState) => void
  empty?: ReactNode
}

/** Table with optional sortable headers, clickable rows, and loading and empty states. */
export function DataTable<T>({ columns, rows, rowKey, loading, onRowClick, sort, onSortChange, empty }: DataTableProps<T>) {
  if (!rows) return loading ? <Loading /> : null

  const toggleSort = (key: string) =>
    onSortChange?.({ key, descending: sort?.key === key ? !sort.descending : false })

  return (
    <div className="table-wrap">
      <table className={cx('table', onRowClick && 'table--clickable')}>
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column.key} className={cx(column.align === 'right' && 'align-right')} aria-sort={ariaSort(column, sort)}>
                {column.sortKey && onSortChange ? (
                  <button
                    type="button"
                    className={cx('table__sort', sort?.key === column.sortKey && 'is-active')}
                    onClick={() => toggleSort(column.sortKey!)}
                  >
                    {column.header}
                    <span className="table__sort-icon" aria-hidden>
                      {sort?.key === column.sortKey ? (sort.descending ? '↓' : '↑') : '↕'}
                    </span>
                  </button>
                ) : (
                  column.header
                )}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={rowKey(row)} onClick={onRowClick ? () => onRowClick(row) : undefined}>
              {columns.map((column) => (
                <td key={column.key} className={cx(column.align === 'right' && 'align-right')}>
                  {column.render(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      {rows.length === 0 && (empty ?? <EmptyState title="Nothing here yet" />)}
    </div>
  )
}

function ariaSort<T>(column: Column<T>, sort?: SortState) {
  if (!column.sortKey || sort?.key !== column.sortKey) return undefined
  return sort.descending ? 'descending' : 'ascending'
}
