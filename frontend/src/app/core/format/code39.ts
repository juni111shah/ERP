/** Code 39 bars: each character is 9 elements (bar, space, bar...), of which 3 are wide. Scanners read it with no check digit. */
const PATTERNS: Record<string, string> = {
  '0': 'nnnwwnwnn', '1': 'wnnwnnnnw', '2': 'nnwwnnnnw', '3': 'wnwwnnnnn', '4': 'nnnwwnnnw', '5': 'wnnwwnnnn', '6': 'nnwwwnnnn', '7': 'nnnwnnwnw',
  '8': 'wnnwnnwnn', '9': 'nnwwnnwnn', A: 'wnnnnwnnw', B: 'nnwnnwnnw', C: 'wnwnnwnnn', D: 'nnnnwwnnw', E: 'wnnnwwnnn', F: 'nnwnwwnnn', G: 'nnnnnwwnw',
  H: 'wnnnnwwnn', I: 'nnwnnwwnn', J: 'nnnnwwwnn', K: 'wnnnnnnww', L: 'nnwnnnnww', M: 'wnwnnnnwn', N: 'nnnnwnnww', O: 'wnnnwnnwn', P: 'nnwnwnnwn',
  Q: 'nnnnnnwww', R: 'wnnnnnwwn', S: 'nnwnnnwwn', T: 'nnnnwnwwn', U: 'wwnnnnnnw', V: 'nwwnnnnnw', W: 'wwwnnnnnn', X: 'nwnnwnnnw', Y: 'wwnnwnnnn',
  Z: 'nwwnwnnnn', '-': 'nwnnnnwnw', '.': 'wwnnnnwnn', ' ': 'nwwnnnwnn', $: 'nwnwnwnnn', '/': 'nwnwnnnwn', '+': 'nwnnnwnwn', '%': 'nnnwnwnwn', '*': 'nwnnwnwnn',
};

export interface BarcodeBar {
  x: number;
  width: number;
}

export interface BarcodeImage {
  bars: BarcodeBar[];
  width: number;
}

const NARROW = 1;
const WIDE = 2.5;

/**
 * The black bars of a Code 39 barcode for the text, or null when it holds a character the symbology cannot carry.
 * Lower case is read as upper, and the start and stop characters are added.
 */
export function code39(text: string): BarcodeImage | null {
  const value = text.toUpperCase();
  if (!value || [...value].some((c) => c === '*' || !PATTERNS[c])) return null;
  const bars: BarcodeBar[] = [];
  let x = 0;
  for (const c of `*${value}*`) {
    [...PATTERNS[c]].forEach((element, i) => {
      const width = element === 'w' ? WIDE : NARROW;
      if (i % 2 === 0) bars.push({ x, width });
      x += width;
    });
    x += NARROW; // the gap between characters
  }
  return { bars, width: x - NARROW };
}
