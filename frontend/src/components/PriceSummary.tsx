import type { OrderPrice } from '../api/types.ts'
import { cx } from '../lib/cx.ts'
import { formatMoney } from '../lib/format.ts'

interface PriceSummaryProps {
  price: OrderPrice
  /** Label for the service line, e.g. the service name. */
  serviceLabel?: string
  totalLabel?: string
}

/** Service price, discount and total. With a staff override, the calculated total is shown struck through. */
export function PriceSummary({ price, serviceLabel = 'Service', totalLabel = 'Total' }: PriceSummaryProps) {
  const calculated = price.servicePrice.amount - price.discountAmount.amount

  return (
    <div className="price-summary">
      <div className="price-summary__row">
        <span>{serviceLabel}</span>
        <span>{formatMoney(price.servicePrice)}</span>
      </div>
      {price.discountCode && (
        <div className="price-summary__row price-summary__row--discount">
          <span>Discount ({price.discountCode})</span>
          <span>−{formatMoney(price.discountAmount)}</span>
        </div>
      )}
      {price.isOverridden && (
        <div className={cx('price-summary__row', 'price-summary__row--struck')}>
          <span>Calculated price</span>
          <span>{formatMoney({ amount: calculated, currency: price.total.currency })}</span>
        </div>
      )}
      <div className="price-summary__total">
        <span>{price.isOverridden ? 'Final price' : totalLabel}</span>
        <span>{formatMoney(price.total)}</span>
      </div>
      {price.isOverridden && price.overrideReason && (
        <p className="price-summary__note">
          <strong>Why the price changed:</strong> {price.overrideReason}
        </p>
      )}
    </div>
  )
}
