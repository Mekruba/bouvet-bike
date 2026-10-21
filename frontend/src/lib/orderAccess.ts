/**
 * Remembers the last email or phone a guest searched with, for this browser tab only (sessionStorage), so
 * going back from an order shows the list again. It's never put in a URL.
 */
const key = 'bouvetbike.last-order-search'

export function rememberSearch(contact: string): void {
  try {
    sessionStorage.setItem(key, contact)
  } catch {
    // Storage unavailable (private mode): the guest just searches again.
  }
}

export function recallSearch(): string {
  try {
    return sessionStorage.getItem(key) ?? ''
  } catch {
    return ''
  }
}
