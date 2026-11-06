import { AssistantStreamParser, streamErrorMessage } from './assistant-stream';

describe('AssistantStreamParser', () => {
  it('reads whole events', () => {
    const events = new AssistantStreamParser().push('event: status\ndata: {"text":"Thinking"}\n\nevent: delta\ndata: {"text":"Hi"}\n\n');
    expect(events).toEqual([{ type: 'status', text: 'Thinking' }, { type: 'delta', text: 'Hi' }]);
  });

  it('waits for the rest of an event split across pieces', () => {
    const parser = new AssistantStreamParser();
    expect(parser.push('event: delta\ndata: {"te')).toEqual([]);
    expect(parser.push('xt":"مرحبا"}\n')).toEqual([]);
    expect(parser.push('\n')).toEqual([{ type: 'delta', text: 'مرحبا' }]);
  });

  it('ignores keep-alive comments and unknown events', () => {
    expect(new AssistantStreamParser().push(': keep-alive\n\nevent: other\ndata: {}\n\n')).toEqual([]);
  });

  it('survives a block it cannot read', () => {
    expect(new AssistantStreamParser().push('event: delta\ndata: {broken\n\nevent: delta\ndata: {"text":"ok"}\n\n')).toEqual([{ type: 'delta', text: 'ok' }]);
  });

  it('reads the end of a reply and an error', () => {
    const events = new AssistantStreamParser().push('event: done\ndata: {"inputTokens":3,"outputTokens":4}\n\nevent: error\ndata: {"code":"daily-limit","message":"Limit"}\n\n');
    expect(events).toEqual([{ type: 'done', inputTokens: 3, outputTokens: 4 }, { type: 'error', code: 'daily-limit', message: 'Limit' }]);
  });

  it('reads the id a reply is saved under', () => {
    expect(new AssistantStreamParser().push('event: reply\ndata: {"id":"r1"}\n\n')).toEqual([{ type: 'reply', id: 'r1' }]);
  });
});

describe('streamErrorMessage', () => {
  it('prefers a field message, then the detail', () => {
    expect(streamErrorMessage({ error: JSON.stringify({ statusCode: 400, errors: { messages: ['Type a message.'] } }) }, 'x')).toBe('Type a message.');
    expect(streamErrorMessage({ error: JSON.stringify({ statusCode: 403, detail: 'Only the owner.' }) }, 'x')).toBe('Only the owner.');
  });

  it('never shows the detail of a server failure', () => {
    expect(streamErrorMessage({ error: JSON.stringify({ statusCode: 500, detail: 'stack trace' }) }, 'Could not send')).toBe('Could not send');
  });

  it('falls back on anything it cannot read', () => {
    expect(streamErrorMessage({ error: 'not json' }, 'Could not send')).toBe('Could not send');
    expect(streamErrorMessage(null, 'Could not send')).toBe('Could not send');
  });
});
