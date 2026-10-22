/** Shared styles for the signed-out pages (login, forgot/reset password, onboarding). */
export const AUTH_STYLES = `
  :host { display: grid; place-items: center; min-height: 100vh; background: var(--erp-color-bg-canvas); padding: 24px; }
  .card { width: 100%; max-width: 420px; background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle);
          border-radius: var(--erp-radius-card); padding: 32px; box-shadow: var(--erp-shadow-1, none); }
  .card.wide { max-width: 560px; }
  .brand { display: flex; align-items: center; gap: 8px; font-weight: 600; font-size: 18px; color: var(--erp-color-primary); margin-bottom: 24px; }
  .brand-mark { width: 32px; height: 32px; border-radius: 8px; background: var(--erp-color-primary); color: var(--erp-color-on-primary); display: grid; place-items: center; }
  h1 { font-size: 20px; margin: 0 0 4px; color: var(--erp-color-text-primary); }
  p.sub { color: var(--erp-color-text-secondary); margin: 0 0 24px; }
  .row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
  .ws { display: flex; flex-direction: column; gap: 8px; }
  .ws button { text-align: start; height: auto; padding: 12px 16px; }
  .ws small { display: block; color: var(--erp-color-text-secondary); }
`;

export const PASSWORD_RULE = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{10,}$/;
export const PASSWORD_HINT = 'At least 10 characters with an upper-case letter, a lower-case letter and a digit.';
