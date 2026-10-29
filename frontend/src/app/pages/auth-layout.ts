/** Shared styles for the signed-out pages (login, forgot/reset password, onboarding). */
export const AUTH_STYLES = `
  :host {
    display: grid;
    place-items: center;
    min-height: 100vh;
    background: radial-gradient(circle at 10% 20%, rgba(16, 149, 93, 0.05) 0%, transparent 40%), #f8fafc;
    padding: 24px;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  }
  .card {
    width: 100%;
    max-width: 440px;
    background: #ffffff;
    border: 1px solid #e2e8f0;
    border-radius: 16px;
    padding: 36px 36px;
    box-shadow: 0 4px 20px -2px rgba(0, 0, 0, 0.05), 0 2px 6px -1px rgba(0, 0, 0, 0.03);
  }
  .card.wide { max-width: 580px; }
  .brand { display: flex; align-items: center; margin-bottom: 24px; justify-content: center; }
  .brand-mark { width: 34px; height: 34px; border-radius: 9px; background: #10955d; color: #fff; display: grid; place-items: center; font-weight: 700; }
  h1 { font-size: 22px; font-weight: 700; margin: 0 0 6px; color: #0f172a; letter-spacing: -0.02em; }
  p.sub { color: #64748b; font-size: 14px; margin: 0 0 24px; line-height: 1.5; }
  .row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
  .ws { display: flex; flex-direction: column; gap: 8px; }
  .ws button { text-align: start; height: auto; padding: 12px 16px; border-radius: 8px; }
  .ws small { display: block; color: #64748b; }
  a { color: #10955d; font-weight: 600; text-decoration: none; }
  a:hover { text-decoration: underline; color: #0c7648; }
`;

export const PASSWORD_RULE = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{10,}$/;
export const PASSWORD_HINT = 'At least 10 characters with an upper-case letter, a lower-case letter and a digit.';
