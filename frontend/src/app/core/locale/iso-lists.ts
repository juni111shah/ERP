/** Generic ISO 3166 / 4217 pick-lists for workspace setup. Country-specific behaviour lives in country packs, not here. */
export interface CountryOption { code: string; name: string; currency: string; timeZone: string; }
export interface CurrencyOption { code: string; name: string; }

export const COUNTRIES: CountryOption[] = [
  { code: 'AE', name: 'United Arab Emirates', currency: 'AED', timeZone: 'Asia/Dubai' },
  { code: 'SA', name: 'Saudi Arabia', currency: 'SAR', timeZone: 'Asia/Riyadh' },
  { code: 'QA', name: 'Qatar', currency: 'QAR', timeZone: 'Asia/Qatar' },
  { code: 'KW', name: 'Kuwait', currency: 'KWD', timeZone: 'Asia/Kuwait' },
  { code: 'BH', name: 'Bahrain', currency: 'BHD', timeZone: 'Asia/Bahrain' },
  { code: 'OM', name: 'Oman', currency: 'OMR', timeZone: 'Asia/Muscat' },
  { code: 'PK', name: 'Pakistan', currency: 'PKR', timeZone: 'Asia/Karachi' },
  { code: 'IN', name: 'India', currency: 'INR', timeZone: 'Asia/Kolkata' },
  { code: 'GB', name: 'United Kingdom', currency: 'GBP', timeZone: 'Europe/London' },
  { code: 'US', name: 'United States', currency: 'USD', timeZone: 'America/New_York' },
];

export const CURRENCIES: CurrencyOption[] = [
  { code: 'AED', name: 'UAE dirham' }, { code: 'SAR', name: 'Saudi riyal' }, { code: 'QAR', name: 'Qatari riyal' },
  { code: 'KWD', name: 'Kuwaiti dinar' }, { code: 'BHD', name: 'Bahraini dinar' }, { code: 'OMR', name: 'Omani rial' },
  { code: 'PKR', name: 'Pakistani rupee' }, { code: 'INR', name: 'Indian rupee' }, { code: 'GBP', name: 'Pound sterling' },
  { code: 'USD', name: 'US dollar' }, { code: 'EUR', name: 'Euro' },
];
