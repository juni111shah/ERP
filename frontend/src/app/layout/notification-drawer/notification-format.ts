/** How a notification is drawn in the drawer: its icon from the severity and a short "how long ago". */

/** The icon of a notification's severity (success, warning, error or danger), else a bell. */
export function notificationIcon(severity?: string): string {
  switch ((severity || '').toLowerCase()) {
    case 'success': return 'check-circle';
    case 'warning': return 'exclamation-circle';
    case 'error':
    case 'danger': return 'close-circle';
    default: return 'bell';
  }
}

/** "Just now", "5m ago", "3h ago", "Yesterday", "4d ago", else the day ("Nov 3"). */
export function notificationAge(dateStr: string, now: Date = new Date()): string {
  if (!dateStr) return '';
  const date = new Date(dateStr);
  const diffMin = Math.floor((now.getTime() - date.getTime()) / 60000);
  const diffHours = Math.floor(diffMin / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffMin < 1) return 'Just now';
  if (diffMin < 60) return `${diffMin}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays === 1) return 'Yesterday';
  if (diffDays < 7) return `${diffDays}d ago`;
  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}
