/** The public assistant's chat: its words in both languages, the saved conversation and how a streamed reply grows. */
import { PublicPlan } from '@core/api/platform.api';

export type BrandAssistantLang = 'en' | 'ar';
/** One bubble of the chat: text, a list of plans (with a trial button when sign-up is open) or the contact form offer. */
export interface BrandAssistantMessage { role: 'visitor' | 'assistant'; text: string; plans?: PublicPlan[]; canStart?: boolean; contact?: boolean; }

/** Session-storage keys: the server's chat token, the conversation so far and the chosen language. */
export const BRAND_ASSISTANT_KEYS = { token: 'erp.brandAssistant.token', messages: 'erp.brandAssistant.messages', lang: 'erp.brandAssistant.lang' } as const;

/** The words of the public assistant, in English and Arabic. */
export const BRAND_ASSISTANT_WORDS = {
  en: {
    open: 'Ask about HigherSquare 360', title: 'HigherSquare assistant', close: 'Close the assistant', language: 'Arabic', languageShort: 'ع',
    notice: 'AI answers can be wrong, so check anything important. Please do not share passwords or payment details.',
    privacy: 'Privacy', hello: 'Hi! Ask me about plans, modules or getting started.', placeholder: 'Type your question', send: 'Send', stop: 'Stop', thinking: 'Thinking',
    failed: 'Sorry, something went wrong. Please try again.', busy: 'You have sent a lot of questions. Please try again a little later.',
    contact: 'Talk to our team', contactTitle: 'Talk to our team', name: 'Your name', email: 'Work email', phone: 'Phone (optional)', company: 'Company (optional)', need: 'How can we help? (optional)',
    consent: 'I agree that you may contact me about this.', submit: 'Send my details', back: 'Back to the chat', sent: 'Thanks. Our team will contact you by email.',
    start: 'Start free trial', choose: 'Choose this plan', compare: 'Compare plans', perMonth: 'per month', users: (n: number) => `Up to ${n} users`, noLimit: 'No limit on users',
    suggestions: ['Which plan fits 12 users?', 'What does the free trial include?', 'Which modules can I use?'],
  },
  ar: {
    open: 'اسأل عن HigherSquare 360', title: 'مساعد HigherSquare', close: 'إغلاق المساعد', language: 'English', languageShort: 'EN',
    notice: 'قد تكون إجابات الذكاء الاصطناعي خاطئة، لذا تحقق من أي أمر مهم. يرجى عدم مشاركة كلمات المرور أو بيانات الدفع.',
    privacy: 'الخصوصية', hello: 'مرحباً! اسألني عن الخطط أو الوحدات أو كيفية البدء.', placeholder: 'اكتب سؤالك', send: 'إرسال', stop: 'إيقاف', thinking: 'جارٍ التفكير',
    failed: 'عذراً، حدث خطأ ما. يرجى المحاولة مرة أخرى.', busy: 'لقد أرسلت أسئلة كثيرة. يرجى المحاولة لاحقاً.',
    contact: 'تحدث مع فريقنا', contactTitle: 'تحدث مع فريقنا', name: 'اسمك', email: 'بريد العمل', phone: 'الهاتف (اختياري)', company: 'الشركة (اختياري)', need: 'كيف يمكننا مساعدتك؟ (اختياري)',
    consent: 'أوافق على أن تتواصلوا معي بخصوص هذا الأمر.', submit: 'إرسال بياناتي', back: 'العودة إلى المحادثة', sent: 'شكراً لك. سيتواصل معك فريقنا عبر البريد الإلكتروني.',
    start: 'ابدأ التجربة المجانية', choose: 'اختر هذه الخطة', compare: 'قارن الخطط', perMonth: 'شهرياً', users: (n: number) => `حتى ${n} مستخدم`, noLimit: 'بلا حد للمستخدمين',
    suggestions: ['أي خطة تناسب 12 مستخدماً؟', 'ماذا تشمل الفترة التجريبية المجانية؟', 'ما الوحدات التي يمكنني استخدامها؟'],
  },
};

/** A value kept for this browser tab, or null (also when storage is unavailable). */
export function readTabValue(key: string): string | null {
  try { return sessionStorage.getItem(key); } catch { return null; }
}

/** Keeps a value for this browser tab. */
export function writeTabValue(key: string, value: string): void {
  try { sessionStorage.setItem(key, value); } catch { /* private browsing: the chat just will not survive a page change */ }
}

/** The conversation saved for this tab, or none. */
export function savedConversation(): BrandAssistantMessage[] {
  try { return JSON.parse(readTabValue(BRAND_ASSISTANT_KEYS.messages) ?? '[]') as BrandAssistantMessage[]; } catch { return []; }
}

/** The language chosen before in this tab, else the page's or the browser's (Arabic or English). */
export function initialBrandAssistantLang(): BrandAssistantLang {
  const saved = readTabValue(BRAND_ASSISTANT_KEYS.lang);
  if (saved === 'en' || saved === 'ar') return saved;
  return (document.documentElement.lang || navigator.language || '').toLowerCase().startsWith('ar') ? 'ar' : 'en';
}

/** The chat with streamed reply text added to the last text reply, or a new reply started. */
export function appendBrandReply(list: BrandAssistantMessage[], text: string): BrandAssistantMessage[] {
  const last = list[list.length - 1];
  return last?.role === 'assistant' && !last.plans && !last.contact ? [...list.slice(0, -1), { ...last, text: last.text + text }] : [...list, { role: 'assistant', text }];
}

/** Whether the chat ends with a reply that already has text (so status updates no longer show). */
export function endsWithReply(list: BrandAssistantMessage[]): boolean {
  const last = list[list.length - 1];
  return last?.role === 'assistant' && !!last.text;
}
