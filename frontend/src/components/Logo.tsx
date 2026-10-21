/** The mark: two leaves and a dot, after the Bouvet logo. */
export function LogoMark({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 32 32" aria-hidden>
      <path d="M4 16a12 12 0 0 1 12-12v12H4Z" fill="#f39a52" />
      <path d="M4 16h12v12A12 12 0 0 1 4 16Z" fill="#7fe8a3" />
      <circle cx="23" cy="9" r="5" fill="#1f44d1" />
    </svg>
  )
}
