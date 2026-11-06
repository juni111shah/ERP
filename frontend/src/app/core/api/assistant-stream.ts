import type { PublicPlan } from './platform.api';

export type AssistantStreamEvent =
  | { type: 'status'; text: string }
  | { type: 'delta'; text: string }
  | { type: 'service'; name: string }
  | { type: 'done'; inputTokens: number; outputTokens: number }
  | { type: 'error'; code: string; message: string }
  /** The public assistant's signed conversation. */
  | { type: 'session'; token: string }
  /** Price cards, straight from the price list. */
  | { type: 'plans'; plans: PublicPlan[]; mode: string }
  /** Offer the contact form. */
  | { type: 'contact'; reason: string }
  /** The workspace assistant's saved conversation. */
  | { type: 'conversation'; id: string }
  /** A change waiting for the person's click. */
  | { type: 'action'; action: AssistantActionCard };

/** Built by the server from what would really happen, never from the model's words. */
export interface AssistantActionCard { id: string; title: string; lines: { label: string; value: string }[]; status: 'pending' | 'done' | 'cancelled' | 'failed' | 'expired'; message?: string | null; link?: string | null; }

/** Reads server-sent events from text that arrives in pieces, which can split an event or even a character sequence anywhere. */
export class AssistantStreamParser {
  private buffer = '';

  push(piece: string): AssistantStreamEvent[] {
    this.buffer += piece;
    const events: AssistantStreamEvent[] = [];
    let end = this.buffer.indexOf('\n\n');
    while (end >= 0) {
      const event = parseBlock(this.buffer.slice(0, end));
      if (event) events.push(event);
      this.buffer = this.buffer.slice(end + 2);
      end = this.buffer.indexOf('\n\n');
    }
    return events;
  }
}

function parseBlock(block: string): AssistantStreamEvent | null {
  let name = '';
  let data = '';
  for (const line of block.split('\n')) {
    if (line.startsWith('event:')) name = line.slice(6).trim();
    else if (line.startsWith('data:')) data += line.slice(5).trim();
  }
  if (!name || !data) return null; // keep-alive comments have neither
  try {
    const body = JSON.parse(data) as Record<string, unknown>;
    switch (name) {
      case 'status': return { type: 'status', text: String(body['text'] ?? '') };
      case 'delta': return { type: 'delta', text: String(body['text'] ?? '') };
      case 'service': return { type: 'service', name: String(body['name'] ?? '') };
      case 'done': return { type: 'done', inputTokens: Number(body['inputTokens'] ?? 0), outputTokens: Number(body['outputTokens'] ?? 0) };
      case 'error': return { type: 'error', code: String(body['code'] ?? ''), message: String(body['message'] ?? '') };
      case 'session': return { type: 'session', token: String(body['token'] ?? '') };
      case 'plans': return { type: 'plans', plans: (body['plans'] ?? []) as PublicPlan[], mode: String(body['mode'] ?? '') };
      case 'contact': return { type: 'contact', reason: String(body['reason'] ?? '') };
      case 'conversation': return { type: 'conversation', id: String(body['id'] ?? '') };
      case 'action': return { type: 'action', action: body as unknown as AssistantActionCard };
      default: return null;
    }
  } catch {
    return null;
  }
}

/** A failed streaming call answers with the normal error envelope, but as text, because the request asked for text. */
export function streamErrorMessage(err: unknown, fallback: string): string {
  const body = (err as { error?: unknown })?.error;
  if (typeof body !== 'string') return fallback;
  try {
    const parsed = JSON.parse(body) as { errors?: Record<string, string[]>; detail?: string; message?: string; statusCode?: number };
    const field = parsed.errors ? Object.values(parsed.errors)[0]?.[0] : undefined;
    if (field) return field;
    // A server failure's detail is for developers.
    if ((parsed.statusCode ?? 0) >= 500) return fallback;
    return parsed.detail ?? parsed.message ?? fallback;
  } catch {
    return fallback;
  }
}
