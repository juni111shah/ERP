/** Shared styles for the signed-out pages (forgot and reset password), and the password rule they share with registration. */
export const AUTH_STYLES = `
  :host {
    display: grid;
    place-items: center;
    min-height: 100vh;
    background: radial-gradient(circle at 10% 20%, rgba(16, 149, 93, 0.05) 0%, transparent 40%), var(--erp-color-bg-canvas);
    padding: 24px;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  }
  .card {
    width: 100%;
    max-width: 440px;
    background: var(--erp-color-bg-surface);
    border: 1px solid var(--erp-color-border-subtle);
    border-radius: 16px;
    padding: 36px 36px;
    box-shadow: 0 4px 20px -2px rgba(0, 0, 0, 0.05), 0 2px 6px -1px rgba(0, 0, 0, 0.03);
  }
  .card.wide { max-width: 580px; }
  .brand { display: flex; align-items: center; margin-bottom: 24px; justify-content: center; }
  .brand-mark { width: 34px; height: 34px; border-radius: 9px; background: #10955d; color: #fff; display: grid; place-items: center; font-weight: 700; }
  h1 { font-size: 22px; font-weight: 700; margin: 0 0 6px; color: var(--erp-color-text-primary); letter-spacing: -0.02em; }
  p.sub { color: var(--erp-color-text-secondary); font-size: 14px; margin: 0 0 24px; line-height: 1.5; }
  .row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
  .ws { display: flex; flex-direction: column; gap: 8px; }
  .ws button { text-align: start; height: auto; padding: 12px 16px; border-radius: 8px; }
  .ws small { display: block; color: var(--erp-color-text-secondary); }
  a { color: var(--erp-color-primary-text); font-weight: 600; text-decoration: none; }
  a:hover { text-decoration: underline; }
`;

export const PASSWORD_RULE = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{10,}$/;
export const PASSWORD_HINT = 'At least 10 characters with an upper-case letter, a lower-case letter and a digit.';
