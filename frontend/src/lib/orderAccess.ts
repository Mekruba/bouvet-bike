/**
 * Remembers the email or phone a guest used for an order, for this browser tab only (sessionStorage).
 * It's sent in the Order-Contact header, never in the URL, so it doesn't end up in history or server logs.
 */
const key = 'bouvetbike.order-contacts'

function read(): Record<string, string> {
  try {
    return JSON.parse(sessionStorage.getItem(key) ?? '{}') as Record<string, string>
  } catch {
    return {}
  }
}

export function rememberContact(reference: string, contact: string): void {
  try {
    sessionStorage.setItem(key, JSON.stringify({ ...read(), [reference.toUpperCase()]: contact }))
  } catch {
    // Storage unavailable (private mode): the guest just enters it again.
  }
}

export function recallContact(reference: string): string | null {
  return read()[reference.toUpperCase()] ?? null
}
