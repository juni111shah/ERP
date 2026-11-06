import { HttpErrorResponse } from '@angular/common/http';
import { assistantErrorText, leadErrorText, statusText } from './brand-assistant.component';

describe('brand assistant wording', () => {
  it('translates what the server says it is doing, and shows anything new as written', () => {
    expect(statusText('ar', 'Checking the plans')).toBe('جارٍ مراجعة الخطط');
    expect(statusText('ar', 'Reading the policy')).toBe('جارٍ قراءة السياسة');
    expect(statusText('ar', 'Doing something new')).toBe('Doing something new');
    expect(statusText('en', 'Checking the plans')).toBe('Checking the plans');
  });

  it('has its own words for every limit code in both languages', () => {
    for (const code of ['daily-limit', 'no-service', 'unavailable', 'interrupted']) {
      expect(assistantErrorText('en', code, 'server words')).not.toBe('server words');
      expect(assistantErrorText('ar', code, 'server words')).not.toBe('server words');
      expect(assistantErrorText('ar', code, '')).toMatch(/[؀-ۿ]/);
    }
  });

  it('falls back to the server words for a code it does not know, then to its own', () => {
    expect(assistantErrorText('ar', 'new-code', 'server words')).toBe('server words');
    expect(assistantErrorText('ar', 'new-code', '')).toBe('عذراً، حدث خطأ ما. يرجى المحاولة مرة أخرى.');
  });

  const refused = (errors: Record<string, string[]>) => new HttpErrorResponse({ status: 400, error: { title: 'Validation failed', errors } });

  it('translates a refused contact form by field', () => {
    expect(leadErrorText('ar', refused({ 'Input.Email': ['Enter a valid email address.'] }))).toBe('أدخل عنوان بريد إلكتروني صحيحاً.');
    expect(leadErrorText('ar', refused({ 'Input.Consent': ['Please agree so that we can contact you.'] }))).toBe('يرجى الموافقة حتى نتمكن من التواصل معك.');
    expect(leadErrorText('en', refused({ 'Input.Email': ['Enter a valid email address.'] }))).toBe('Enter a valid email address.');
  });

  it('shows the server words for a field it has no translation for', () => {
    expect(leadErrorText('ar', refused({ 'Input.Phone': ['Too long.'] }))).toBe('Too long.');
  });

  it('says the visitor is sending too much when the rate limit answers', () => {
    expect(leadErrorText('ar', new HttpErrorResponse({ status: 429 }))).toBe('لقد أرسلت أسئلة كثيرة. يرجى المحاولة لاحقاً.');
    expect(leadErrorText('en', new HttpErrorResponse({ status: 429 }))).toBe('You have sent a lot of questions. Please try again a little later.');
  });
});
