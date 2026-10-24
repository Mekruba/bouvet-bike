/*
 * Types mirroring the backend contracts (backend/BouvetBike/<Module>/Contracts).
 * Enums arrive as strings, dates as "yyyy-MM-dd", timestamps as ISO 8601 UTC.
 */

export type OrderStatus = 'New' | 'InProgress' | 'WaitingForCustomer' | 'Completed' | 'Cancelled'
export type DiscountType = 'Percentage' | 'FixedAmount'
export type OrderSort = 'DueDate' | 'CreatedAt' | 'CustomerName' | 'Status'
export type Role = 'Admin' | 'Mechanic' | 'Customer'

export interface Money {
  amount: number
  currency: string
}

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** RFC 6902 operation. */
export interface JsonPatchOperation {
  op: 'add' | 'remove' | 'replace' | 'test'
  path: string
  value?: unknown
}

// ---- Catalog ----

export interface ServiceType {
  id: string
  name: string
  description: string | null
  price: Money
  estimatedMinutes: number
  isActive: boolean
}

export interface CreateServiceTypeRequest {
  name: string
  description: string | null
  price: number
  estimatedMinutes: number
}

// ---- Orders ----

export interface DayCapacity {
  date: string
  booked: number
  /** This date's capacity: its override if it has one, otherwise the weekday default. */
  capacity: number
  /** The weekday default, to show deviations like "6/5". */
  defaultCapacity: number
  remaining: number
  isFull: boolean
  isClosed: boolean
  isAdjusted: boolean
}

export type Weekday = 'monday' | 'tuesday' | 'wednesday' | 'thursday' | 'friday' | 'saturday' | 'sunday'

/** Default capacity per weekday; 0 is closed. */
export type CapacitySchedule = Record<Weekday, number>

export interface CapacityOverride {
  date: string
  maxOrders: number
  defaultCapacity: number
  reason: string | null
}

export interface OrderPrice {
  servicePrice: Money
  discountCode: string | null
  discountAmount: Money
  total: Money
  isOverridden: boolean
}

export interface Contact {
  name: string
  email: string
  phone: string
}

export interface PlaceOrderRequest {
  name: string
  email: string
  phone: string
  bikeBrand: string
  bikeModel: string | null
  /** Null when not chosen yet, so the API answers with a field error instead of failing to parse "". */
  serviceTypeId: string | null
  dueDate: string | null
  notes: string | null
  discountCode: string | null
}

export interface CustomerOrder {
  reference: string
  status: OrderStatus
  contact: Contact
  bikeBrand: string
  bikeModel: string | null
  serviceName: string
  dueDate: string
  notes: string | null
  canEditNotes: boolean
  price: OrderPrice
  createdAt: string
}

export interface CustomerOrderSummary {
  reference: string
  status: OrderStatus
  bikeBrand: string
  serviceName: string
  dueDate: string
  total: Money
}

export interface AdminOrderQuery {
  search?: string
  status?: OrderStatus
  serviceTypeId?: string
  dueFrom?: string
  dueTo?: string
  sort?: OrderSort
  descending?: boolean
  page?: number
  pageSize?: number
}

export interface AdminOrderSummary {
  id: string
  reference: string
  status: OrderStatus
  contact: Contact
  isGuest: boolean
  bikeBrand: string
  serviceName: string
  dueDate: string
  total: Money
  createdAt: string
}

export interface StatusChange {
  id: string
  from: OrderStatus
  to: OrderStatus
  changedBy: string | null
  changedAt: string
  reason: string | null
}

export interface PriceOverride {
  amount: Money
  reason: string
  overriddenBy: string | null
  overriddenAt: string
}

export interface AdminOrder {
  id: string
  reference: string
  status: OrderStatus
  allowedStatusChanges: OrderStatus[]
  contact: Contact
  customerId: string | null
  bikeBrand: string
  bikeModel: string | null
  serviceTypeId: string
  serviceName: string
  dueDate: string
  customerNotes: string | null
  internalNotes: string | null
  price: OrderPrice
  priceOverride: PriceOverride | null
  statusHistory: StatusChange[]
  createdAt: string
  updatedAt: string
}

// ---- Discounts ----

export interface DiscountCode {
  id: string
  code: string
  type: DiscountType
  value: number
  validFrom: string | null
  validTo: string | null
  maxUses: number | null
  timesUsed: number
  isActive: boolean
}

export interface CreateDiscountCodeRequest {
  code: string
  type: DiscountType
  value: number
  validFrom: string | null
  validTo: string | null
  maxUses: number | null
}

// ---- Users ----

export interface CurrentUser {
  id: string
  name: string
  email: string
  phone: string | null
  roles: Role[]
  isStaff: boolean
}

export interface RegistrationResponse {
  user: CurrentUser
  /** Stand-in for email delivery: the link the confirmation email would contain. */
  emailConfirmationLink: string
}

export interface EmailConfirmationRequestResponse {
  /** Stand-in for email delivery; null when there's no unconfirmed account with that email. */
  emailConfirmationLink: string | null
}

export interface StaffUser {
  id: string
  name: string
  email: string
  roles: Role[]
}
