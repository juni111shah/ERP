/** The public front page. Theme colours only, so it follows light and dark. */
export const PRICING_STYLES = `
  :host { display: block; min-height: 100vh; background: var(--erp-color-bg-surface); color: var(--erp-color-text-primary); }
  .inner { max-width: 1160px; margin: 0 auto; padding: 0 24px; }
  .inner.narrow { max-width: 780px; }
  section { padding: 72px 0; scroll-margin-top: 64px; }
  section.band { background: var(--erp-color-bg-canvas); }
  h2 { font-size: 30px; font-weight: 800; letter-spacing: -.02em; line-height: 1.2; margin: 0 0 10px; color: var(--erp-color-text-primary); }
  h3 { color: var(--erp-color-text-primary); }
  .head { text-align: center; max-width: 660px; margin: 0 auto 40px; }
  .head p { margin: 0; color: var(--erp-color-text-muted); font-size: 16px; line-height: 1.55; }
  .center { padding: 48px; text-align: center; }

  .top { position: sticky; top: 0; z-index: 20; background: var(--erp-color-bg-surface); border-bottom: 1px solid var(--erp-color-border-subtle); }
  .top .inner { display: flex; align-items: center; gap: 24px; height: 64px; }
  .brand { display: inline-flex; flex-shrink: 0; min-width: 0; }
  .top erp-brand-logo, footer erp-brand-logo { width: auto; }
  nav { display: flex; gap: 4px; flex: 1; justify-content: center; }
  nav button, .foot-links button { background: none; border: 0; cursor: pointer; font: inherit; color: var(--erp-color-text-secondary); padding: 6px 12px; border-radius: 8px; font-weight: 500; }
  nav button:hover { color: var(--erp-color-primary); background: var(--erp-color-primary-subtle); }
  .top-actions { display: flex; align-items: center; gap: 14px; margin-inline-start: auto; flex-shrink: 0; }
  .signin { font-weight: 600; white-space: nowrap; }

  .hero { padding: 64px 0 72px; background: radial-gradient(circle at 12% 10%, var(--erp-color-primary-subtle) 0%, transparent 45%), var(--erp-color-bg-canvas); }
  .hero-copy { max-width: 820px; margin: 0 auto 34px; text-align: center; }
  .hero-copy .cta, .hero-copy .ticks { justify-content: center; }
  .eyebrow { display: inline-block; padding: 4px 12px; border-radius: 9999px; background: var(--erp-color-primary-subtle); border: 1px solid var(--erp-color-primary-border); color: var(--erp-color-primary); font-size: 13px; font-weight: 600; margin-bottom: 18px; }
  h1 { font-size: 46px; font-weight: 800; letter-spacing: -.03em; line-height: 1.1; margin: 0 0 18px; color: var(--erp-color-text-primary); }
  .lead { font-size: 18px; line-height: 1.55; color: var(--erp-color-text-secondary); margin: 0 0 26px; }
  .cta { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 22px; }
  .ticks { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: 8px 20px; color: var(--erp-color-text-secondary); font-size: 14px; }
  .ticks li { display: flex; gap: 7px; align-items: baseline; }
  .ticks span[nz-icon] { color: var(--erp-color-primary); }
  .ticks.column { flex-direction: column; margin-top: 16px; }

  /* ---- The 3D scene: a stage sets the camera, the scene turns inside it, and each layer stands at its own depth. ---- */
  .hero { overflow: hidden; }
  .hero-phone ::ng-deep .body { box-shadow: 0 10px 18px -8px rgba(15, 23, 42, .45); }
  .hero-stage { position: relative; perspective: 2000px; --tilt-x: 0deg; --tilt-y: 0deg; padding: 22px 34px 40px; min-width: 0; }
  .scene { position: relative; transform-style: preserve-3d; transform: rotateX(calc(9deg + var(--tilt-x))) rotateY(calc(-5deg + var(--tilt-y))); transition: transform .3s ease-out; animation: scene-in 1.1s cubic-bezier(.2, .8, .2, 1) backwards; }
  .scene .window { transform: translateZ(0); }
  .float { position: absolute; display: flex; align-items: center; gap: 10px; padding: 10px 14px; border-radius: 12px; background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); box-shadow: 0 18px 40px -14px rgba(15, 23, 42, .35); z-index: 2; white-space: nowrap; }
  .float strong { display: block; font-size: 13px; color: var(--erp-color-text-primary); }
  .float small { font-size: 11.5px; color: var(--erp-color-text-muted); }
  .f-ico { display: flex; align-items: center; justify-content: center; width: 30px; height: 30px; border-radius: 9px; background: var(--erp-color-primary-subtle); color: var(--erp-color-primary); font-size: 15px; }
  .f-ico.warn { color: var(--erp-color-warning-text); }
  .float.a { top: -20px; right: 24%; --z: 70px; animation: float 6s ease-in-out infinite; }
  .float.b { bottom: 70px; left: -30px; --z: 110px; animation: float 7s ease-in-out -2s infinite; }
  .hero-phone { position: absolute; right: -58px; bottom: -44px; width: 236px; height: 484px; transform-origin: bottom right; transform: translateZ(130px) rotateY(-8deg) scale(.8); transform-style: preserve-3d; z-index: 3; }
  .fit { position: relative; overflow: hidden; }
  .fit > * { position: absolute; top: 0; left: 0; transform-origin: top left; }
  .todo-title { margin: 0 0 8px; font-size: 16px; font-weight: 700; color: var(--erp-color-text-primary); transform: translateZ(34px); }
  .todo { display: flex; gap: 11px; padding: 10px 12px; margin-bottom: 8px; border-radius: 10px; background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); transform: translateZ(34px); }
  .todo:last-child { margin-bottom: 0; }
  .todo p { margin: 0; font-size: 13.5px; color: var(--erp-color-text-primary); }
  .sign { width: 30px; height: 30px; border-radius: 50%; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
  .sign.red { background: #fde4e4; color: #d92d2d; }
  .sign.amber { background: #fdf2c9; color: #b98900; }
  .tags { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 3px; }
  .tag { padding: 0 7px; border-radius: 5px; border: 1px solid var(--erp-color-border-subtle); font-size: 11.5px; line-height: 19px; color: var(--erp-color-text-secondary); }
  .tag.red { background: #fdeeee; border-color: #f5c2c2; color: #cf2d2d; }
  .tag.amber { background: #fdf7df; border-color: #f0dc9c; color: #a87c00; }

  .stage3d { perspective: 1200px; --tilt-x: 0deg; --tilt-y: 0deg; min-width: 0; }
  .stage3d .panel { transform-style: preserve-3d; transition: transform .35s ease-out, box-shadow .35s ease; box-shadow: 0 26px 50px -28px rgba(15, 23, 42, .35); }
  .stage3d .panel.left { transform: rotateX(calc(4deg + var(--tilt-x))) rotateY(calc(9deg + var(--tilt-y))); }
  .stage3d .panel.right { transform: rotateX(calc(4deg + var(--tilt-x))) rotateY(calc(-9deg + var(--tilt-y))); }
  .stage3d .step, .stage3d .journal { transform: translateZ(34px); }
  .stage3d.fx-wait .panel { opacity: 0; }
  .stage3d.fx-in .panel { animation: panel-in .9s cubic-bezier(.2, .8, .2, 1) backwards; }

  /* Entrances. fx-wait is only ever set by the directive that also clears it. */
  .pop { transition: opacity .6s ease, transform .6s cubic-bezier(.2, .8, .2, 1); transition-delay: calc(var(--d, 0) * 90ms + 120ms); }
  .fx-wait .pop { opacity: 0; }
  .fx-wait .feature.pop, .fx-wait .steps .pop, .steps.fx-wait .pop { transform: translateY(26px) rotateX(-14deg); }
  .fx-wait .todo.pop, .fx-wait .step.pop { transform: translateY(18px) translateZ(0); }
  .rise { transition: opacity .7s ease, transform .7s cubic-bezier(.2, .8, .2, 1); }
  .rise.fx-wait { opacity: 0; transform: translateY(18px); }
  .fx-wait .flow-line i { width: 0 !important; }
  .flow-line i { transition: width 1.3s cubic-bezier(.2, .8, .2, 1) .35s; }
  .cards { perspective: 1400px; }
  .feature { transition: transform .3s ease, box-shadow .3s ease, border-color .3s ease, opacity .6s ease; }
  .feature:hover { transform: translateY(-6px) rotateX(4deg); box-shadow: 0 22px 40px -22px rgba(15, 23, 42, .4); border-color: var(--erp-color-primary-border); }
  .plans erp-plan-card { transition: transform .3s ease; }
  .plans erp-plan-card:hover { transform: translateY(-6px); }

  .mobile-band { position: relative; overflow: hidden; color: #fff; background: radial-gradient(circle at 80% 30%, #1a8a60 0%, transparent 55%), linear-gradient(135deg, #082c21, #0f5c43); }

  @keyframes scene-in { from { opacity: 0; transform: rotateX(26deg) rotateY(-14deg) translateY(50px); } }
  @keyframes float { 0%, 100% { transform: translateZ(var(--z)) translateY(0); } 50% { transform: translateZ(var(--z)) translateY(-10px); } }
  @keyframes panel-in { from { opacity: 0; transform: rotateX(16deg) rotateY(-16deg) translateY(36px); } }

  @media (prefers-reduced-motion: reduce) {
    .scene, .float, .stage3d.fx-in .panel { animation: none !important; }
    .pop, .rise, .feature, .scene, .stage3d .panel, .flow-line i, .plans erp-plan-card { transition: none !important; }
    .float.a, .float.b { transform: translateZ(var(--z)); }
    html { scroll-behavior: auto; }
  }

  .window { border-radius: 14px; overflow: hidden; background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); box-shadow: 0 24px 60px -24px rgba(15, 23, 42, .28); min-width: 0; }
  .window-bar { display: flex; gap: 6px; padding: 11px 14px; border-bottom: 1px solid var(--erp-color-border-subtle); }
  .window-bar i { width: 10px; height: 10px; border-radius: 50%; background: var(--erp-color-border-subtle); }

  .facts { padding: 26px 0; border-bottom: 1px solid var(--erp-color-border-subtle); }
  .facts .inner { display: flex; flex-wrap: wrap; justify-content: space-around; gap: 16px 32px; text-align: center; }
  .facts strong { display: block; font-size: 24px; font-weight: 800; color: var(--erp-color-primary); }
  .facts span { color: var(--erp-color-text-muted); font-size: 14px; }

  .cycle { display: flex; flex-direction: column; align-items: center; gap: 6px; margin-bottom: 30px; }
  .cycle small { color: var(--erp-color-success-text); font-weight: 600; }
  .plans { display: grid; gap: 20px; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); align-items: stretch; }
  .note { text-align: center; margin: 22px 0 0; color: var(--erp-color-text-muted); font-size: 13.5px; }
  .closed { text-align: center; padding: 14px 18px; margin-bottom: 24px; border-radius: 10px; background: var(--erp-color-primary-subtle); border: 1px solid var(--erp-color-primary-border); }
  .compare-title { text-align: center; font-size: 20px; font-weight: 700; margin: 52px 0 18px; }
  .compare { overflow-x: auto; border: 1px solid var(--erp-color-border-subtle); border-radius: 12px; background: var(--erp-color-bg-surface); }
  .compare table { width: 100%; border-collapse: collapse; min-width: 560px; }
  .compare th, .compare td { padding: 12px 18px; text-align: start; border-bottom: 1px solid var(--erp-color-border-subtle); font-size: 14px; }
  .compare tbody tr:last-child th, .compare tbody tr:last-child td { border-bottom: 0; }
  .compare thead th { font-weight: 700; color: var(--erp-color-text-primary); }
  .compare tbody th { font-weight: 500; color: var(--erp-color-text-secondary); }
  .compare td { color: var(--erp-color-text-primary); font-weight: 600; }

  .show { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 56px; align-items: center; margin-bottom: 64px; }
  .show.flip > :first-child { order: 2; }
  .show h3 { font-size: 23px; font-weight: 700; letter-spacing: -.01em; margin: 0 0 10px; }
  .show p { color: var(--erp-color-text-secondary); font-size: 15.5px; line-height: 1.6; margin: 0; }
  .panel { padding: 22px; border-radius: 14px; background: var(--erp-color-bg-canvas); border: 1px solid var(--erp-color-border-subtle); min-width: 0; }
  .flow { display: flex; justify-content: space-between; gap: 6px; }
  .step { display: flex; flex-direction: column; align-items: center; gap: 6px; font-size: 12.5px; color: var(--erp-color-text-muted); min-width: 0; }
  .step span { display: flex; align-items: center; justify-content: center; width: 32px; height: 32px; border-radius: 50%; border: 1px solid var(--erp-color-border-subtle); background: var(--erp-color-bg-surface); font-weight: 700; }
  .step.done span { background: var(--erp-color-primary); border-color: var(--erp-color-primary); color: var(--erp-color-on-primary, #fff); }
  .step.now { color: var(--erp-color-text-primary); font-weight: 600; }
  .step.now span { border: 2px solid var(--erp-color-primary); color: var(--erp-color-primary); }
  .flow-line { height: 5px; border-radius: 9999px; background: var(--erp-color-border-subtle); margin: 18px 0 12px; overflow: hidden; }
  .flow-line i { display: block; height: 100%; background: var(--erp-color-primary); }
  .show .flow-note { font-size: 13.5px; color: var(--erp-color-text-muted); text-align: center; }
  .journal { width: 100%; border-collapse: collapse; font-size: 13.5px; background: var(--erp-color-bg-surface); border-radius: 10px; overflow: hidden; }
  .journal th, .journal td { padding: 10px 12px; text-align: end; border-bottom: 1px solid var(--erp-color-border-subtle); color: var(--erp-color-text-primary); }
  .journal th:first-child, .journal td:first-child { text-align: start; }
  .journal th { color: var(--erp-color-text-muted); font-weight: 600; font-size: 12.5px; }
  .journal tfoot td { font-weight: 700; border-bottom: 0; color: var(--erp-color-success-text); }
  .illustration { text-align: center; font-size: 12.5px; color: var(--erp-color-text-muted); margin: 0; }

  .cards { display: grid; gap: 18px; grid-template-columns: repeat(4, minmax(0, 1fr)); }
  .cards.modules { grid-template-columns: repeat(3, minmax(0, 1fr)); }
  .feature { padding: 22px; border-radius: 14px; background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); min-width: 0; }
  section:not(.band) .feature { background: var(--erp-color-bg-canvas); }
  .feature h3 { font-size: 15.5px; font-weight: 700; margin: 12px 0 6px; }
  .feature p { margin: 0; font-size: 14px; line-height: 1.5; color: var(--erp-color-text-muted); }
  .ico, .mono { display: inline-flex; align-items: center; justify-content: center; width: 40px; height: 40px; border-radius: 10px; background: var(--erp-color-primary-subtle); color: var(--erp-color-primary); font-size: 19px; font-weight: 800; }

  .two { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 56px; }
  .two h2 { font-size: 24px; }
  .two p { color: var(--erp-color-text-muted); line-height: 1.55; margin: 0 0 16px; }
  .chips { display: flex; flex-wrap: wrap; gap: 8px; }
  .chips span { padding: 5px 12px; border-radius: 9999px; background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); font-size: 13.5px; color: var(--erp-color-text-secondary); }

  .steps { list-style: none; margin: 0; padding: 0; display: grid; gap: 24px; grid-template-columns: repeat(4, minmax(0, 1fr)); counter-reset: none; }
  .steps b { display: flex; align-items: center; justify-content: center; width: 36px; height: 36px; border-radius: 50%; background: var(--erp-color-primary); color: var(--erp-color-on-primary, #fff); font-size: 15px; }
  .steps h3 { font-size: 16px; font-weight: 700; margin: 12px 0 6px; }
  .steps p { margin: 0; font-size: 14px; line-height: 1.5; color: var(--erp-color-text-muted); }

  .faq { display: flex; flex-direction: column; gap: 10px; }
  details { border: 1px solid var(--erp-color-border-subtle); border-radius: 12px; background: var(--erp-color-bg-surface); }
  summary { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 15px 18px; cursor: pointer; font-weight: 600; color: var(--erp-color-text-primary); list-style: none; }
  summary::-webkit-details-marker { display: none; }
  summary span[nz-icon] { color: var(--erp-color-text-muted); transition: transform .15s ease; flex-shrink: 0; }
  details[open] summary span[nz-icon] { transform: rotate(180deg); }
  details p { margin: 0; padding: 0 18px 16px; color: var(--erp-color-text-secondary); line-height: 1.6; font-size: 14.5px; }
  .ask { text-align: center; margin: 22px 0 0; color: var(--erp-color-text-muted); }

  .final { background: var(--erp-color-primary); text-align: center; padding: 60px 0; }
  .final h2 { color: var(--erp-color-on-primary, #fff); }
  .final p { color: var(--erp-color-on-primary, #fff); opacity: .9; margin: 0 0 22px; font-size: 16px; }

  footer { padding: 36px 0 28px; border-top: 1px solid var(--erp-color-border-subtle); }
  footer .inner { display: grid; grid-template-columns: auto minmax(0, 1fr); gap: 18px 40px; align-items: center; }
  .foot-brand p { margin: 8px 0 0; color: var(--erp-color-text-muted); font-size: 13.5px; }
  .foot-links { display: flex; flex-wrap: wrap; gap: 6px 18px; justify-content: flex-end; align-items: center; font-size: 14px; }
  .foot-links button { padding: 0; font-size: 14px; }
  .foot-links button:hover { color: var(--erp-color-primary); }
  .copyright { grid-column: 1 / -1; margin: 0; padding-top: 14px; border-top: 1px solid var(--erp-color-border-subtle); color: var(--erp-color-text-muted); font-size: 13px; }

  @media (max-width: 1023px) {
    nav { display: none; }
    .hero-phone { right: -18px; transform: translateZ(110px) rotateY(-6deg) scale(.62); }
    .show, .two { grid-template-columns: minmax(0, 1fr); gap: 32px; }
    .show.flip > :first-child { order: 0; }
    .cards, .cards.modules, .steps { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    h1 { font-size: 38px; }
  }
  @media (max-width: 767px) {
    section { padding: 48px 0; }
    .hero-stage { padding: 16px 0 44px; }
    .scene { transform: rotateX(6deg) rotateY(-4deg); }
    .float.a { right: 4px; top: -22px; }
    .float.b { left: 0; bottom: -30px; }
    .float { padding: 7px 10px; }
    .hero-phone { right: -6px; bottom: -40px; transform: translateZ(80px) rotateY(-4deg) scale(.46); }
    .stage3d .panel.left, .stage3d .panel.right { transform: rotateX(3deg) rotateY(0deg); }
    .inner { padding: 0 16px; }
    .hero { padding: 36px 0 44px; }
    h1 { font-size: 30px; }
    h2 { font-size: 24px; }
    .lead { font-size: 16px; }
    .top .inner { gap: 10px; height: 58px; }
    .top-actions { gap: 10px; }
    .top-actions button { display: none; }
    .cards, .cards.modules, .steps { grid-template-columns: minmax(0, 1fr); }
    .show { margin-bottom: 44px; }
    .panel { padding: 14px; }
    .step { font-size: 11px; }
    .step span { width: 26px; height: 26px; font-size: 12px; }
    footer .inner { grid-template-columns: minmax(0, 1fr); }
    .foot-links { justify-content: flex-start; }
  }
`;
