import { SalesOrderStatus, SalesQuoteStatus } from '@core/api/sales.api';
import { displayLabel } from '@core/format/display';

/** Every quote status, in the order a quote moves through them. */
export const QUOTE_STATUSES: readonly SalesQuoteStatus[] = ['draft', 'sent', 'accepted', 'declined', 'expired', 'converted'];

/** Every sales-order status, in the order an order moves through them. */
export const ORDER_STATUSES: readonly SalesOrderStatus[] = ['draft', 'confirmed', 'processing', 'delivered', 'invoiced', 'cancelled'];

/** The toast after a status change: "SQ-0001 is now accepted." */
export const statusChangedText = (number: string, status: SalesQuoteStatus | SalesOrderStatus): string =>
  `${number} is now ${displayLabel(status).toLowerCase()}.`;
