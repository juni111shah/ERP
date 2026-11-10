import { AssistantActionCard } from '@core/api/assistant-stream';
import { AssistantConversationDetail } from '@core/api/workspace-assistant.api';

/** One entry of the assistant's chat log: a question, a reply, or a change the server proposes (busy while it is being confirmed). */
export type AssistantChatItem =
  | { kind: 'user'; text: string }
  | { kind: 'assistant'; text: string }
  | { kind: 'action'; card: AssistantActionCard; busy?: boolean };

/** The words of the assistant panel, in English and Arabic. */
export const ASSISTANT_WORDS = {
  en: {
    title: 'Assistant', close: 'Close the assistant', newChat: 'New chat', history: 'Past chats', back: 'Back to the chat', noHistory: 'No past chats yet.', remove: 'Delete this chat',
    notice: 'AI answers can be wrong, so check anything important. It only uses what you are allowed to see.', hello: 'Ask me about your records, or where to find something.', placeholder: 'Ask a question', send: 'Send', stop: 'Stop', working: 'Working',
    failed: 'Sorry, something went wrong. Please try again.', confirm: 'Confirm', cancel: 'Cancel', open: 'Open', cancelled: 'Cancelled', expired: 'Expired', check: 'Please check this before confirming.',
    suggestions: ['What needs my attention today?', 'Who owes us the most?', 'Where do I add a new customer?'],
  },
  ar: {
    title: 'المساعد', close: 'إغلاق المساعد', newChat: 'محادثة جديدة', history: 'المحادثات السابقة', back: 'العودة إلى المحادثة', noHistory: 'لا توجد محادثات سابقة بعد.', remove: 'حذف هذه المحادثة',
    notice: 'قد تكون إجابات الذكاء الاصطناعي خاطئة، لذا تحقق من أي أمر مهم. لا يستخدم إلا ما يحق لك الاطلاع عليه.', hello: 'اسألني عن سجلاتك أو عن مكان أي شيء في النظام.', placeholder: 'اطرح سؤالاً', send: 'إرسال', stop: 'إيقاف', working: 'جارٍ العمل',
    failed: 'عذراً، حدث خطأ ما. يرجى المحاولة مرة أخرى.', confirm: 'تأكيد', cancel: 'إلغاء', open: 'فتح', cancelled: 'أُلغي', expired: 'انتهت صلاحيته', check: 'يرجى التحقق من هذا قبل التأكيد.',
    suggestions: ['ما الذي يحتاج إلى انتباهي اليوم؟', 'من أكثر من يدين لنا بالمال؟', 'أين أضيف عميلاً جديداً؟'],
  },
};

/** A saved conversation as a chat log: its messages and proposed changes, in time order. */
export function conversationItems(c: AssistantConversationDetail): AssistantChatItem[] {
  const timed: { at: string; item: AssistantChatItem }[] = [
    ...c.messages.map((m) => ({ at: m.at, item: (m.fromUser ? { kind: 'user', text: m.text } : { kind: 'assistant', text: m.text }) as AssistantChatItem })),
    ...c.actions.map((a) => ({ at: a.createdAt, item: { kind: 'action', card: a } as AssistantChatItem })),
  ];
  return timed.sort((x, y) => x.at.localeCompare(y.at)).map((t) => t.item);
}

/** The log with streamed reply text added to the last reply, or a new reply started. */
export function appendReply(list: AssistantChatItem[], text: string): AssistantChatItem[] {
  const last = list[list.length - 1];
  return last?.kind === 'assistant' ? [...list.slice(0, -1), { ...last, text: last.text + text }] : [...list, { kind: 'assistant', text }];
}

/** The log with one action card marked busy or not. */
export function withCardBusy(list: AssistantChatItem[], id: string, busy: boolean): AssistantChatItem[] {
  return list.map((i) => (i.kind === 'action' && i.card.id === id ? { ...i, busy } : i));
}

/** The log with an action card replaced by the server's updated one. */
export function withCard(list: AssistantChatItem[], card: AssistantActionCard): AssistantChatItem[] {
  return list.map((i) => (i.kind === 'action' && i.card.id === card.id ? { kind: 'action', card } : i));
}
