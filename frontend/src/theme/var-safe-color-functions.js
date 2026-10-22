/**
 * var-safe-color-functions.js — tiny Less plugin.
 *
 * ng-zorro's LESS calls colour functions (fade, fadein, fadeout, tint, shade,
 * darken, lighten) on theme variables. Those functions throw when the variable
 * is a CSS custom property (`var(--erp-…)`), which is what we need for runtime
 * dark mode + tenant branding. This plugin wraps each function: real colours
 * are handled by Less exactly as before; `var(...)` inputs are converted to
 * the native CSS `color-mix()` equivalent (Baseline 2023: Chrome 111, Safari
 * 16.2, Firefox 113), evaluated by the browser at runtime.
 *
 * Loaded from ng-zorro-overrides.less with:  @plugin "./var-safe-color-functions";
 */
module.exports = {
  install(less, pluginManager, functions) {
    const builtins = less.functions.functionRegistry;
    const isVar = (n) => n && !(n instanceof less.tree.Color) && /var\(/.test(n.toCSS ? n.toCSS({}) : String(n.value));
    const css = (n) => (n.toCSS ? n.toCSS({}) : String(n.value));
    const pct = (a) => {
      const v = Number(a.value);
      return a.unit && a.unit.toString() === '%' ? v : v * 100;
    };
    const raw = (s) => new less.tree.Anonymous(s);
    const wrap = (name, toCss) => {
      const original = builtins.get(name);
      functions.add(name, function (color, amount, ...rest) {
        if (isVar(color)) return raw(toCss(css(color), amount ? pct(amount) : 0));
        return original.call(this, color, amount, ...rest);
      });
    };
    // fade(c, a%)  → c at alpha a
    wrap('fade', (c, a) => `color-mix(in srgb, ${c} ${a}%, transparent)`);
    // fadein/fadeout adjust alpha relatively; approximation keeps the colour opaque/lighter
    wrap('fadein', (c) => c);
    wrap('fadeout', (c, a) => `color-mix(in srgb, ${c} ${100 - a}%, transparent)`);
    wrap('tint', (c, a) => `color-mix(in srgb, #fff ${a}%, ${c})`);
    wrap('shade', (c, a) => `color-mix(in srgb, #000 ${a}%, ${c})`);
    wrap('lighten', (c, a) => `color-mix(in oklab, #fff ${a}%, ${c})`);
    wrap('darken', (c, a) => `color-mix(in oklab, #000 ${a}%, ${c})`);
  },
};
