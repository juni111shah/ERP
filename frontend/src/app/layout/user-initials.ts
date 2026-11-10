/** The avatar letters of a person: the first letters of their first two names, upper-cased ("?" without a name). */
export function userInitials(fullName: string | null | undefined): string {
  return (fullName ?? '?').split(' ').map((p) => p[0]).slice(0, 2).join('').toUpperCase();
}
