import { api, query } from './client.ts'
import type {
  AdminOrder,
  AdminOrderQuery,
  AdminOrderSummary,
  CapacityOverride,
  CapacitySchedule,
  CreateDiscountCodeRequest,
  CreateServiceTypeRequest,
  CurrentUser,
  CustomerOrder,
  CustomerOrderSummary,
  DayCapacity,
  DiscountCode,
  EmailConfirmationRequestResponse,
  JsonPatchOperation,
  OrderPrice,
  OrderStatus,
  Paged,
  PlaceOrderRequest,
  PriceOverride,
  RegistrationResponse,
  Role,
  ServiceType,
  StaffUser,
  StatusChange,
} from './types.ts'

/** Guests find their orders by email or phone, sent in a header so it never appears in a URL. */
const contactHeader = (contact: string) => ({ 'Order-Contact': contact })

export const catalogApi = {
  list: (includeInactive = false) => api<ServiceType[]>(`/service-types${query({ includeInactive: includeInactive || undefined })}`),
  create: (request: CreateServiceTypeRequest) => api<ServiceType>('/service-types', { body: request }),
  patch: (id: string, patch: JsonPatchOperation[]) => api<ServiceType>(`/service-types/${id}`, { patch }),
}

export const ordersApi = {
  capacity: (from?: string, days?: number) => api<DayCapacity[]>(`/capacity${query({ from, days })}`),
  quote: (serviceTypeId: string, discountCode?: string | null) =>
    api<OrderPrice>(`/price-quotes${query({ serviceTypeId, discountCode })}`),
  place: (request: PlaceOrderRequest) => api<CustomerOrder>('/orders', { body: request }),
  /** The reference alone opens the order. */
  get: (reference: string) => api<CustomerOrder>(`/orders/${encodeURIComponent(reference)}`),
  patch: (reference: string, patch: JsonPatchOperation[]) => api<CustomerOrder>(`/orders/${encodeURIComponent(reference)}`, { patch }),
  /** Every order with this email or phone, newest first. */
  findByContact: (contact: string) => api<CustomerOrderSummary[]>('/orders', { headers: contactHeader(contact) }),
  /** The logged-in customer's own orders. */
  mine: () => api<CustomerOrderSummary[]>('/orders'),
}

export const adminOrdersApi = {
  search: (params: AdminOrderQuery) => api<Paged<AdminOrderSummary>>(`/admin/orders${query({ ...params })}`),
  get: (id: string) => api<AdminOrder>(`/admin/orders/${id}`),
  patch: (id: string, patch: JsonPatchOperation[]) => api<AdminOrder>(`/admin/orders/${id}`, { patch }),
  remove: (id: string) => api<void>(`/admin/orders/${id}`, { method: 'DELETE' }),
  changeStatus: (id: string, status: OrderStatus, reason: string | null) =>
    api<StatusChange>(`/admin/orders/${id}/status-changes`, { body: { status, reason } }),
  setPriceOverride: (id: string, amount: number, reason: string) =>
    api<PriceOverride>(`/admin/orders/${id}/price-override`, { method: 'PUT', body: { amount, reason } }),
  clearPriceOverride: (id: string) => api<void>(`/admin/orders/${id}/price-override`, { method: 'DELETE' }),
}

export const capacityApi = {
  schedule: () => api<CapacitySchedule>('/admin/capacity/schedule'),
  patchSchedule: (patch: JsonPatchOperation[]) => api<CapacitySchedule>('/admin/capacity/schedule', { patch }),
  overrides: (from?: string, to?: string) => api<CapacityOverride[]>(`/admin/capacity/overrides${query({ from, to })}`),
  /** Sets or replaces one date's capacity; 0 closes it. */
  setOverride: (date: string, maxOrders: number, reason: string | null) =>
    api<CapacityOverride>(`/admin/capacity/overrides/${date}`, { method: 'PUT', body: { maxOrders, reason } }),
  /** Back to the weekday default. */
  removeOverride: (date: string) => api<void>(`/admin/capacity/overrides/${date}`, { method: 'DELETE' }),
}

export const discountsApi = {
  list: () => api<DiscountCode[]>('/admin/discount-codes'),
  create: (request: CreateDiscountCodeRequest) => api<DiscountCode>('/admin/discount-codes', { body: request }),
  patch: (id: string, patch: JsonPatchOperation[]) => api<DiscountCode>(`/admin/discount-codes/${id}`, { patch }),
}

export const accountApi = {
  me: () => api<CurrentUser>('/users/me'),
  /** Creates the account with an unconfirmed email; doesn't sign in. */
  register: (name: string, email: string, phone: string | null, password: string) =>
    api<RegistrationResponse>('/users', { body: { name, email, phone, password } }),
  confirmEmail: (userId: string, token: string) => api<void>(`/users/${userId}/email-confirmation`, { method: 'PUT', body: { token } }),
  requestEmailConfirmation: (email: string) =>
    api<EmailConfirmationRequestResponse>('/email-confirmation-requests', { body: { email } }),
  login: (email: string, password: string, rememberMe: boolean) =>
    api<CurrentUser>('/session', { body: { email, password, rememberMe } }),
  logout: () => api<void>('/session', { method: 'DELETE' }),
  staff: () => api<StaffUser[]>('/admin/staff'),
  createStaff: (name: string, email: string, password: string, role: Role) =>
    api<StaffUser>('/admin/staff', { body: { name, email, password, role } }),
}
