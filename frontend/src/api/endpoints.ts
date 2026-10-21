import { api, query } from './client.ts'
import type {
  AdminOrder,
  AdminOrderQuery,
  AdminOrderSummary,
  CreateDiscountCodeRequest,
  CreateServiceTypeRequest,
  CurrentUser,
  CustomerOrder,
  CustomerOrderSummary,
  DayCapacity,
  DiscountCode,
  JsonPatchOperation,
  OrderPrice,
  OrderStatus,
  Paged,
  PlaceOrderRequest,
  PriceOverride,
  Role,
  ServiceType,
  StaffUser,
  StatusChange,
} from './types.ts'

/** Header with the email or phone on an order, how guests prove access (never sent in the URL). */
const contactHeader = (contact?: string | null) => (contact ? { 'Order-Contact': contact } : undefined)

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
  get: (reference: string, contact?: string | null) =>
    api<CustomerOrder>(`/orders/${encodeURIComponent(reference)}`, { headers: contactHeader(contact) }),
  patch: (reference: string, contact: string | null, patch: JsonPatchOperation[]) =>
    api<CustomerOrder>(`/orders/${encodeURIComponent(reference)}`, { patch, headers: contactHeader(contact) }),
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

export const discountsApi = {
  list: () => api<DiscountCode[]>('/admin/discount-codes'),
  create: (request: CreateDiscountCodeRequest) => api<DiscountCode>('/admin/discount-codes', { body: request }),
  patch: (id: string, patch: JsonPatchOperation[]) => api<DiscountCode>(`/admin/discount-codes/${id}`, { patch }),
}

export const accountApi = {
  me: () => api<CurrentUser>('/users/me'),
  register: (name: string, email: string, phone: string | null, password: string) =>
    api<CurrentUser>('/users', { body: { name, email, phone, password } }),
  login: (email: string, password: string, rememberMe: boolean) =>
    api<CurrentUser>('/session', { body: { email, password, rememberMe } }),
  logout: () => api<void>('/session', { method: 'DELETE' }),
  staff: () => api<StaffUser[]>('/admin/staff'),
  createStaff: (name: string, email: string, password: string, role: Role) =>
    api<StaffUser>('/admin/staff', { body: { name, email, password, role } }),
}
