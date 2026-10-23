/**
 * TenantThemeService — applies tenant branding, colour theme, density, motion,
 * language and text direction at runtime.
 *
 * - Brand: derives a full, contrast-checked brand scale (OKLCH) from ONE tenant
 *   colour and writes it to `--erp-brand-*` custom properties on <html>.
 *   tokens.scss maps those to semantic tokens and to ng-zorro's `--ant-*` vars,
 *   so every ng-zorro component re-themes with no rebuild.
 * - Theme: 'light' | 'dark' | 'system' → `data-theme` on <html>.
 * - Density: 'comfortable' | 'compact' → `data-density`.
 * - Direction: 'ltr' | 'rtl' → `dir`/`lang` on <html> and a `dir` signal that the
 *   root template binds to CDK's Dir directive (`<div [dir]="theme.dir()">`),
 *   which is what ng-zorro components read (nzDirection).
 *
 * Industry-neutral: nothing here knows about modules or business types.
 * Angular 17+ (standalone, signals). No third-party dependencies.
 */
import { DOCUMENT } from '@angular/common';
import {
  DestroyRef,
  Injectable,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';

// ---------------------------------------------------------------------------
// Public contracts
// ---------------------------------------------------------------------------
export type ThemeMode = 'light' | 'dark' | 'system';
export type Density = 'comfortable' | 'compact';
export type TextDirection = 'ltr' | 'rtl';
export type MotionPreference = 'system' | 'reduced';

/**
 * Branding a workspace (the SaaS customer; "tenant" in code only — the UI says
 * "Workspace", see uiux/00-shared-conventions.md §4) configures in Settings → Branding.
 */
export interface WorkspaceBranding {
  /** Brand colour as #RRGGBB. Everything else is derived and contrast-checked. */
  primaryColor?: string;
  /** Optional separate colour for dark mode; otherwise derived from primaryColor. */
  primaryColorDark?: string;
  /** Logo for light surfaces (https:, same-origin path, or data:image/*). */
  logoUrl?: string;
  /** Logo for dark surfaces; falls back to logoUrl. */
  logoUrlDark?: string;
  /** Compact square mark for the collapsed sidebar. */
  logoMarkUrl?: string;
  /** Base control radius in px (0–16). Cards = +2, overlays = +4. */
  radius?: number;
  /** Font family name (Latin). Arabic-script text always uses the Arabic stack. */
  fontFamily?: string;
  /** Optional woff2 URL for fontFamily (https or same-origin). */
  fontUrl?: string;
}

/** Per-user preferences (persisted locally, also synced to the user profile API). */
export interface UserThemePreferences {
  mode: ThemeMode;
  density: Density;
  motion: MotionPreference;
  /** BCP-47 language tag, e.g. 'en', 'ar', 'ur'. */
  lang: string;
}

/** Result of applying a brand, surfaced in the Branding settings screen. */
export interface BrandContrastReport {
  input: string;
  adjusted: boolean;
  solidLight: string;
  onSolidLight: string;
  solidLightRatio: number;
  textLight: string;
  textLightRatio: number;
  solidDark: string;
  onSolidDark: string;
  solidDarkRatio: number;
  warnings: string[];
}

/** Alias kept for the file/service name requested in the brief. */
export type TenantBranding = WorkspaceBranding;

const RTL_LANGS = new Set(['ar', 'ur', 'fa', 'he', 'ps', 'sd', 'ug', 'yi', 'ckb', 'dv']);
const STORAGE_KEY = 'erp.ui.prefs';
const DEFAULT_PREFS: UserThemePreferences = {
  mode: 'system',
  density: 'comfortable',
  motion: 'system',
  lang: 'en',
};

// Surfaces the derivation is checked against (must match tokens.scss)
const SURFACE_LIGHT = '#ffffff';
const CANVAS_LIGHT = '#f4f6f8';
const SURFACE_DARK = '#16181c';
const RAISED_DARK = '#1e2126';
const AA_TEXT = 4.5;

@Injectable({ providedIn: 'root' })
export class TenantThemeService {
  private readonly doc = inject(DOCUMENT);
  private readonly root = this.doc.documentElement;
  private readonly win = this.doc.defaultView;

  // ---- state --------------------------------------------------------------
  private readonly prefs = signal<UserThemePreferences>(this.readPrefs());
  private readonly systemDark = signal<boolean>(this.matches('(prefers-color-scheme: dark)'));
  private readonly brandReport = signal<BrandContrastReport | null>(null);
  private readonly branding = signal<TenantBranding>({});

  readonly mode = computed(() => this.prefs().mode);
  readonly density = computed(() => this.prefs().density);
  readonly lang = computed(() => this.prefs().lang || 'en');
  /** Theme actually rendered. */
  readonly resolvedTheme = computed<'light' | 'dark'>(() =>
    this.mode() === 'system' ? (this.systemDark() ? 'dark' : 'light') : (this.mode() as 'light' | 'dark'),
  );
  /** Bind on the root element: `<div [dir]="theme.dir()">` (CDK BidiModule). */
  readonly dir = computed<TextDirection>(() =>
    RTL_LANGS.has(this.lang().split('-')[0].toLowerCase()) ? 'rtl' : 'ltr',
  );
  readonly isRtl = computed(() => this.dir() === 'rtl');
  /** Logo for the current theme (for <img [src]>). */
  readonly logoUrl = computed(() => {
    const b = this.branding();
    return this.resolvedTheme() === 'dark' ? b.logoUrlDark ?? b.logoUrl ?? null : b.logoUrl ?? null;
  });
  readonly logoMarkUrl = computed(() => this.branding().logoMarkUrl ?? null);
  readonly contrastReport = this.brandReport.asReadonly();

  constructor() {
    const destroyRef = inject(DestroyRef);
    const mq = this.win?.matchMedia?.('(prefers-color-scheme: dark)');
    if (mq) {
      const onChange = (e: MediaQueryListEvent) => this.systemDark.set(e.matches);
      mq.addEventListener('change', onChange);
      destroyRef.onDestroy(() => mq.removeEventListener('change', onChange));
    }

    // Reflect preferences onto <html>. One effect = one style recalculation.
    effect(() => {
      const p = this.prefs();
      this.root.setAttribute('data-theme', this.resolvedTheme());
      this.root.setAttribute('data-density', p.density);
      if (p.motion === 'reduced') this.root.setAttribute('data-motion', 'reduced');
      else this.root.removeAttribute('data-motion');
      this.root.setAttribute('lang', p.lang);
      this.root.setAttribute('dir', this.dir());
      this.writePrefs(p);
    });
  }

  // ---- user preferences ---------------------------------------------------
  setMode(mode: ThemeMode): void {
    this.withThemeTransition(() => this.prefs.update((p) => ({ ...p, mode })));
  }
  /** Header toggle: flips between light and dark (leaves 'system' once used). */
  toggleTheme(): void {
    this.setMode(this.resolvedTheme() === 'dark' ? 'light' : 'dark');
  }
  setDensity(density: Density): void {
    this.prefs.update((p) => ({ ...p, density }));
  }
  setMotion(motion: MotionPreference): void {
    this.prefs.update((p) => ({ ...p, motion }));
  }
  /**
   * Change UI language. Direction follows the language. Call NzI18nService.setLocale
   * with the matching ng-zorro locale (en_US / ar_EG / ur_PK) from the i18n layer.
   */
  setLanguage(lang: string): void {
    this.prefs.update((p) => ({ ...p, lang }));
  }

  // ---- tenant branding ----------------------------------------------------
  /**
   * Apply a tenant's branding. Safe to call repeatedly (e.g. after the org
   * settings load or when an admin previews changes). Returns a contrast report.
   */
  applyBranding(branding: TenantBranding): BrandContrastReport | null {
    this.branding.set(branding);
    const style = this.root.style;

    // Radius
    if (branding.radius !== undefined && Number.isFinite(branding.radius)) {
      const r = Math.min(16, Math.max(0, Math.round(branding.radius)));
      style.setProperty('--erp-radius-base', `${r}px`);
    } else {
      style.removeProperty('--erp-radius-base');
    }

    // Font
    const family = sanitizeFontFamily(branding.fontFamily);
    if (family) {
      style.setProperty('--erp-brand-font', `'${family}'`);
      const url = safeAssetUrl(branding.fontUrl);
      if (url && 'fonts' in this.doc) {
        const face = new FontFace(family, `url("${url}") format("woff2")`, { display: 'swap' });
        face.load().then((f) => (this.doc as Document).fonts.add(f)).catch(() => {
          /* keep fallback stack; surface in admin UI if needed */
        });
      }
    } else {
      style.removeProperty('--erp-brand-font');
    }

    // Logos (CSS vars too, for pure-CSS consumers such as the print header)
    this.setUrlVar('--erp-brand-logo', branding.logoUrl);
    this.setUrlVar('--erp-brand-logo-dark', branding.logoUrlDark ?? branding.logoUrl);

    // Colour
    const hex = normaliseHex(branding.primaryColor);
    if (!hex) {
      BRAND_VARS.forEach((v) => style.removeProperty(v));
      this.brandReport.set(null);
      return null;
    }
    const { vars, report } = deriveBrand(hex, normaliseHex(branding.primaryColorDark));
    Object.entries(vars).forEach(([k, v]) => style.setProperty(k, v));
    this.brandReport.set(report);
    return report;
  }

  /** Remove tenant overrides (e.g. on sign-out) and fall back to the product default. */
  resetBranding(): void {
    const style = this.root.style;
    [...BRAND_VARS, '--erp-radius-base', '--erp-brand-font', '--erp-brand-logo', '--erp-brand-logo-dark'].forEach((v) =>
      style.removeProperty(v),
    );
    this.branding.set({});
    this.brandReport.set(null);
  }

  /**
   * Scope a module accent to an element (dashboard widget, module landing page).
   * Accent is decorative only (borders, icons, KPI bars) — never body text.
   */
  applyModuleAccent(el: HTMLElement, accentHex: string | undefined): void {
    const hex = normaliseHex(accentHex);
    if (!hex) {
      el.style.removeProperty('--erp-module-accent');
      el.style.removeProperty('--erp-module-accent-dark');
      return;
    }
    // Non-text contrast (1.4.11) ≥ 3:1 against the surface in each theme
    el.style.setProperty('--erp-module-accent', ensureContrast(hex, SURFACE_LIGHT, 3, 'darken'));
    el.style.setProperty('--erp-module-accent-dark', ensureContrast(hex, SURFACE_DARK, 3, 'lighten'));
  }

  // ---- internals ----------------------------------------------------------
  /** Avoid a half-themed flash: suppress transitions for one frame while switching. */
  private withThemeTransition(apply: () => void): void {
    const el = this.root;
    el.classList.add('erp-theme-switching');
    apply();
    this.win?.requestAnimationFrame(() =>
      this.win?.requestAnimationFrame(() => el.classList.remove('erp-theme-switching')),
    );
  }

  private setUrlVar(name: string, url: string | undefined): void {
    const safe = safeAssetUrl(url);
    if (safe) this.root.style.setProperty(name, `url("${safe.replace(/"/g, '%22')}")`);
    else this.root.style.removeProperty(name);
  }

  private matches(q: string): boolean {
    return !!this.win?.matchMedia?.(q).matches;
  }

  private readPrefs(): UserThemePreferences {
    try {
      const raw = this.win?.localStorage.getItem(STORAGE_KEY);
      return raw ? { ...DEFAULT_PREFS, ...(JSON.parse(raw) as Partial<UserThemePreferences>) } : DEFAULT_PREFS;
    } catch {
      return DEFAULT_PREFS;
    }
  }

  private writePrefs(p: UserThemePreferences): void {
    try {
      this.win?.localStorage.setItem(STORAGE_KEY, JSON.stringify(p));
    } catch {
      /* storage may be blocked; preferences still apply for the session */
    }
  }
}

// ===========================================================================
// Colour maths (pure functions, unit-testable)
// ===========================================================================
type RGB = [number, number, number]; // 0..1 sRGB
type OKLCH = [number, number, number]; // L 0..1, C, H degrees

const BRAND_STEPS: Record<string, number> = {
  '50': 0.97, '100': 0.935, '200': 0.875, '300': 0.795, '400': 0.715,
  '500': 0.635, '600': 0.555, '700': 0.485, '800': 0.415, '900': 0.315, '950': 0.225,
};
const CHROMA_FACTOR: Record<string, number> = {
  '50': 0.22, '100': 0.4, '200': 0.65, '300': 0.85, '400': 1, '500': 1,
  '600': 1, '700': 0.95, '800': 0.85, '900': 0.6, '950': 0.45,
};

export const BRAND_VARS: string[] = [
  ...Object.keys(BRAND_STEPS).map((s) => `--erp-brand-${s}`),
  '--erp-brand-solid-light', '--erp-brand-solid-hover-light', '--erp-brand-solid-active-light',
  '--erp-brand-on-solid-light', '--erp-brand-text-light', '--erp-brand-subtle-light',
  '--erp-brand-subtle-hover-light', '--erp-brand-border-light', '--erp-brand-ink',
  '--erp-brand-solid-dark', '--erp-brand-solid-hover-dark', '--erp-brand-solid-active-dark',
  '--erp-brand-on-solid-dark', '--erp-brand-text-dark', '--erp-brand-subtle-dark',
  '--erp-brand-subtle-hover-dark', '--erp-brand-border-dark',
];

/** Derive every brand CSS variable from one colour, enforcing WCAG AA. */
export function deriveBrand(
  hex: string,
  darkHex?: string | null,
): { vars: Record<string, string>; report: BrandContrastReport } {
  const warnings: string[] = [];
  const [, c, h] = hexToOklch(hex);
  const vars: Record<string, string> = {};

  // 1. Tonal scale at fixed lightness, hue preserved, chroma gamut-mapped.
  for (const [step, L] of Object.entries(BRAND_STEPS)) {
    vars[`--erp-brand-${step}`] = oklchToHex([L, c * CHROMA_FACTOR[step], h]);
  }

  // 2. Solid (button fill) in light theme: keep the exact brand colour when an
  //    on-colour reaches 4.5:1; otherwise darken until white text passes.
  let solidLight = hex;
  let onSolidLight = bestOn(hex);
  if (contrast(hex, onSolidLight) < AA_TEXT) {
    solidLight = ensureContrast(hex, '#ffffff', AA_TEXT, 'darken');
    onSolidLight = '#ffffff';
    warnings.push(`Brand colour ${hex} was darkened to ${solidLight} so button text meets 4.5:1.`);
  }
  vars['--erp-brand-solid-light'] = solidLight;
  vars['--erp-brand-on-solid-light'] = onSolidLight;
  vars['--erp-brand-solid-hover-light'] = shiftL(solidLight, onSolidLight === '#ffffff' ? -0.06 : 0.05);
  vars['--erp-brand-solid-active-light'] = shiftL(solidLight, onSolidLight === '#ffffff' ? -0.12 : 0.1);

  // 3. Brand text (links, active nav label): ≥4.5:1 on the canvas (the darker light surface).
  const textLight = ensureContrast(solidLight, CANVAS_LIGHT, AA_TEXT, 'darken');
  vars['--erp-brand-text-light'] = textLight;
  vars['--erp-brand-subtle-light'] = vars['--erp-brand-50'];
  vars['--erp-brand-subtle-hover-light'] = vars['--erp-brand-100'];
  vars['--erp-brand-border-light'] = vars['--erp-brand-300'];
  vars['--erp-brand-ink'] = oklchToHex([0.3, Math.min(c, 0.07), h]);

  // 4. Dark theme: lighten until ≥4.5:1 on the raised dark surface.
  //    Prefer the vivid 400 step (reads as "the brand" on dark), then the input.
  const darkBase = darkHex ?? hex;
  const preferred = darkHex ? [darkHex] : [vars['--erp-brand-400'], hex];
  const solidDark =
    preferred.find((cand) => contrast(cand, RAISED_DARK) >= AA_TEXT) ??
    ensureContrast(darkBase, RAISED_DARK, AA_TEXT, 'lighten');
  const onSolidDark = bestOn(solidDark, oklchToHex([0.2, Math.min(c, 0.05), h]));
  vars['--erp-brand-solid-dark'] = solidDark;
  vars['--erp-brand-on-solid-dark'] = onSolidDark;
  vars['--erp-brand-solid-hover-dark'] = shiftL(solidDark, 0.06);
  vars['--erp-brand-solid-active-dark'] = shiftL(solidDark, -0.06);
  vars['--erp-brand-text-dark'] = solidDark;
  vars['--erp-brand-subtle-dark'] = mix(solidDark, SURFACE_DARK, 0.16);
  vars['--erp-brand-subtle-hover-dark'] = mix(solidDark, SURFACE_DARK, 0.24);
  vars['--erp-brand-border-dark'] = mix(solidDark, SURFACE_DARK, 0.4);
  if (contrast(solidDark, onSolidDark) < AA_TEXT) {
    warnings.push('Dark-mode button text is below 4.5:1; consider providing primaryColorDark.');
  }

  return {
    vars,
    report: {
      input: hex,
      adjusted: solidLight !== hex,
      solidLight,
      onSolidLight,
      solidLightRatio: round2(contrast(solidLight, onSolidLight)),
      textLight,
      textLightRatio: round2(contrast(textLight, CANVAS_LIGHT)),
      solidDark,
      onSolidDark,
      solidDarkRatio: round2(contrast(solidDark, onSolidDark)),
      warnings,
    },
  };
}

/** Move a colour's OKLCH lightness until it reaches `ratio` against `against`. */
export function ensureContrast(hex: string, against: string, ratio: number, dir: 'darken' | 'lighten'): string {
  if (contrast(hex, against) >= ratio) return hex;
  const [L, c, h] = hexToOklch(hex);
  const step = dir === 'darken' ? -0.01 : 0.01;
  for (let l = L + step; l > 0 && l < 1; l += step) {
    const candidate = oklchToHex([l, c, h]);
    if (contrast(candidate, against) >= ratio) return candidate;
  }
  return dir === 'darken' ? '#000000' : '#ffffff';
}

/** White or the given dark ink, whichever contrasts more. */
function bestOn(bg: string, dark = '#111418'): string {
  return contrast(bg, '#ffffff') >= contrast(bg, dark) ? '#ffffff' : dark;
}

export function contrast(a: string, b: string): number {
  const la = relLuminance(hexToRgb(a));
  const lb = relLuminance(hexToRgb(b));
  const [hi, lo] = la > lb ? [la, lb] : [lb, la];
  return (hi + 0.05) / (lo + 0.05);
}

function relLuminance([r, g, b]: RGB): number {
  const f = (v: number) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4);
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
}

function shiftL(hex: string, delta: number): string {
  const [L, c, h] = hexToOklch(hex);
  return oklchToHex([Math.min(0.99, Math.max(0.05, L + delta)), c, h]);
}

function mix(a: string, b: string, weightA: number): string {
  const ra = hexToRgb(a);
  const rb = hexToRgb(b);
  return rgbToHex([0, 1, 2].map((i) => ra[i] * weightA + rb[i] * (1 - weightA)) as RGB);
}

// --- conversions (Björn Ottosson's OKLab) ----------------------------------
function hexToRgb(hex: string): RGB {
  const h = hex.replace('#', '');
  return [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16) / 255) as RGB;
}
function rgbToHex(rgb: RGB): string {
  return (
    '#' +
    rgb
      .map((v) => Math.round(Math.min(1, Math.max(0, v)) * 255).toString(16).padStart(2, '0'))
      .join('')
  );
}
const toLinear = (v: number) => (v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4);
const toGamma = (v: number) => (v <= 0.0031308 ? 12.92 * v : 1.055 * v ** (1 / 2.4) - 0.055);

function hexToOklch(hex: string): OKLCH {
  const [r, g, b] = hexToRgb(hex).map(toLinear);
  const l = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
  const m = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
  const s = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
  const L = 0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s;
  const A = 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s;
  const B = 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s;
  const C = Math.hypot(A, B);
  const H = ((Math.atan2(B, A) * 180) / Math.PI + 360) % 360;
  return [L, C, H];
}

function oklchToLinearRgb([L, C, H]: OKLCH): RGB {
  const a = C * Math.cos((H * Math.PI) / 180);
  const b = C * Math.sin((H * Math.PI) / 180);
  const l = (L + 0.3963377774 * a + 0.2158037573 * b) ** 3;
  const m = (L - 0.1055613458 * a - 0.0638541728 * b) ** 3;
  const s = (L - 0.0894841775 * a - 1.291485548 * b) ** 3;
  return [
    4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
    -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
    -0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s,
  ];
}

/** OKLCH → hex with gamut mapping by chroma reduction (keeps hue + lightness). */
function oklchToHex([L, C, H]: OKLCH): string {
  let c = C;
  for (let i = 0; i < 40; i++) {
    const rgb = oklchToLinearRgb([L, c, H]);
    if (rgb.every((v) => v >= -0.0005 && v <= 1.0005)) {
      return rgbToHex(rgb.map(toGamma) as RGB);
    }
    c *= 0.92;
  }
  return rgbToHex(oklchToLinearRgb([L, 0, H]).map(toGamma) as RGB);
}

// --- input hygiene ----------------------------------------------------------
function normaliseHex(v: string | undefined | null): string | null {
  if (!v) return null;
  const s = v.trim().toLowerCase();
  if (/^#[0-9a-f]{6}$/.test(s)) return s;
  if (/^#[0-9a-f]{3}$/.test(s)) return '#' + [...s.slice(1)].map((ch) => ch + ch).join('');
  return null;
}
function sanitizeFontFamily(v: string | undefined): string | null {
  if (!v) return null;
  const s = v.trim();
  return /^[A-Za-z0-9 \-]{1,48}$/.test(s) ? s : null;
}
/** Allow https:, same-origin absolute paths and raster/SVG data URIs only. */
function safeAssetUrl(v: string | undefined): string | null {
  if (!v) return null;
  const s = v.trim();
  if (/^data:image\/(png|jpeg|webp|svg\+xml);base64,[A-Za-z0-9+/=]+$/.test(s)) return s;
  if (/^\/(?!\/)[^\s"'()\\]*$/.test(s)) return s;
  try {
    const u = new URL(s);
    return u.protocol === 'https:' && !/["'()\\\s]/.test(s) ? u.toString() : null;
  } catch {
    return null;
  }
}
const round2 = (n: number) => Math.round(n * 100) / 100;
