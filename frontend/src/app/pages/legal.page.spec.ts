import { legalBlocks } from './legal.page';

describe('legalBlocks', () => {
  it('turns ## lines into headings and everything else into paragraphs', () => {
    expect(legalBlocks('Intro line\n\n## The service\nWhat you get.\r\n')).toEqual([
      { heading: false, text: 'Intro line' },
      { heading: true, text: 'The service' },
      { heading: false, text: 'What you get.' },
    ]);
  });

  it('keeps markup as plain text', () => {
    expect(legalBlocks('<script>alert(1)</script>')).toEqual([{ heading: false, text: '<script>alert(1)</script>' }]);
  });

  it('gives nothing for empty text', () => {
    expect(legalBlocks(' \n \n')).toEqual([]);
  });
});
