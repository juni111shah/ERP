/** The words and date format of the thermal receipt, in English or Arabic. */

const STATUS_AR: ReadonlyMap<string, string> = new Map([
  ['completed', 'مكتمل'],
  ['refunded', 'مسترد'],
  ['voided', 'ملغى'],
  ['draft', 'مسودة'],
  ['successful', 'ناجح'],
  ['paid', 'مدفوع'],
]);

const PAYMENT_METHOD_AR: ReadonlyMap<string, string> = new Map([
  ['cash', 'نقداً'],
  ['card', 'بطاقة مصرفية'],
  ['credit card', 'بطاقة ائتمان'],
  ['bank transfer', 'تحويل بنكي'],
]);

const WALK_IN = ['walk-in', 'walk-in customer'];

/** The receipt status ("Completed" when none), in Arabic when known. */
export function receiptStatusLabel(status: string | undefined, arabic: boolean): string {
  const raw = status || 'Completed';
  return arabic ? STATUS_AR.get(raw.toLowerCase()) ?? raw : raw;
}

/** A payment method, in Arabic when known. */
export function receiptPaymentLabel(method: string, arabic: boolean): string {
  return arabic ? PAYMENT_METHOD_AR.get((method || '').toLowerCase()) ?? method : method;
}

/** The customer's name, or "Walk-in Customer" for none or a walk-in. */
export function receiptCustomerLabel(customer: string | undefined, arabic: boolean): string {
  if (customer && !WALK_IN.includes(customer.toLowerCase())) return customer;
  return arabic ? 'عميل مباشر' : 'Walk-in Customer';
}

/** "10/11/2026  02:05:09 PM" (ص / م in Arabic); text that is not a date is shown as it is. */
export function receiptDateTime(raw: string | Date, arabic: boolean): string {
  if (!raw) return '';
  const d = new Date(raw);
  if (isNaN(d.getTime())) return String(raw);
  const pad = (n: number) => String(n).padStart(2, '0');
  const hours = d.getHours();
  const isPm = hours >= 12;
  const ampm = arabic ? (isPm ? 'م' : 'ص') : (isPm ? 'PM' : 'AM');
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}  ${pad(hours % 12 || 12)}:${pad(d.getMinutes())}:${pad(d.getSeconds())} ${ampm}`;
}
