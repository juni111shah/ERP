import { isInternalLink, parseAssistantText } from './assistant-text';

describe('parseAssistantText', () => {
  it('splits paragraphs and bold text', () => {
    expect(parseAssistantText('Hello **there**\n\nBye')).toEqual([
      { kind: 'paragraph', parts: [{ text: 'Hello ' }, { text: 'there', bold: true }] },
      { kind: 'paragraph', parts: [{ text: 'Bye' }] },
    ]);
  });

  it('groups bullets and numbers into lists', () => {
    const blocks = parseAssistantText('- one\n- two\n1. three');
    expect(blocks.length).toBe(2);
    expect(blocks[0]).toEqual({ kind: 'list', ordered: false, items: [[{ text: 'one' }], [{ text: 'two' }]] });
    expect(blocks[1].kind === 'list' && blocks[1].ordered).toBeTrue();
  });

  it('drops heading marks', () => {
    expect(parseAssistantText('## Plans')).toEqual([{ kind: 'paragraph', parts: [{ text: 'Plans' }] }]);
  });

  it('keeps links on this site', () => {
    expect(parseAssistantText('See [invoices](/finance/invoices)')[0]).toEqual({ kind: 'paragraph', parts: [{ text: 'See ' }, { text: 'invoices', link: '/finance/invoices' }] });
  });

  it('removes the address from any link that leaves the site', () => {
    for (const href of ['https://evil.example/x', '//evil.example', 'javascript:evil', '/\\evil.example']) {
      const block = parseAssistantText(`Click [here](${href})`)[0];
      expect(block).toEqual({ kind: 'paragraph', parts: [{ text: 'Click ' }, { text: 'here' }] });
    }
  });

  it('never makes a link of a script address, even one with brackets', () => {
    const block = parseAssistantText('Click [here](javascript:alert(1))')[0];
    expect(JSON.stringify(block)).not.toContain('"link"');
  });

  it('treats an image, a tag or a script as plain words', () => {
    const block = parseAssistantText('<img src=x onerror=alert(1)> <script>x</script>')[0];
    expect(block).toEqual({ kind: 'paragraph', parts: [{ text: '<img src=x onerror=alert(1)> <script>x</script>' }] });
    const image = parseAssistantText('![secret](https://evil.example/?d=1)')[0];
    expect(JSON.stringify(image)).not.toContain('evil.example');
  });

  it('turns a table into a list, without its heading row or rule', () => {
    const blocks = parseAssistantText('Today:\n\n| Area | Message | Link |\n|---|:--|---|\n| HR | 1 expired | [Staff](/hr/employees) |\n| Sales | 2 late | |');
    expect(blocks).toEqual([
      { kind: 'paragraph', parts: [{ text: 'Today:' }] },
      {
        kind: 'list',
        ordered: false,
        items: [
          [{ text: 'HR · 1 expired · ' }, { text: 'Staff', link: '/hr/employees' }],
          [{ text: 'Sales · 2 late' }],
        ],
      },
    ]);
  });

  it('leaves a line with a single bar as words', () => {
    expect(parseAssistantText('Use a | b')[0]).toEqual({ kind: 'paragraph', parts: [{ text: 'Use a | b' }] });
  });

  it('keeps Arabic text', () => {
    expect(parseAssistantText('- **الخطط** المتاحة')[0]).toEqual({ kind: 'list', ordered: false, items: [[{ text: 'الخطط', bold: true }, { text: ' المتاحة' }]] });
  });
});

describe('isInternalLink', () => {
  it('accepts only paths on this site', () => {
    expect(isInternalLink('/parties')).toBeTrue();
    expect(isInternalLink('//x.example')).toBeFalse();
    expect(isInternalLink('https://x.example')).toBeFalse();
    expect(isInternalLink('parties')).toBeFalse();
  });
});
