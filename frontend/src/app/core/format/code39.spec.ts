import { code39 } from './code39';

describe('code39', () => {
  it('writes 5 bars per character, with start and stop added', () => {
    const image = code39('POS-00012')!;
    expect(image.bars.length).toBe((9 + 2) * 5);
  });

  it('every character is 3 wide elements out of 9, so all have the same width', () => {
    const one = code39('A')!;
    const two = code39('AB')!;
    const character = 3 * 2.5 + 6;
    expect(one.width).toBeCloseTo(3 * character + 2);
    expect(two.width).toBeCloseTo(4 * character + 3);
  });

  it('reads lower case as upper case', () => {
    expect(code39('abc')).toEqual(code39('ABC'));
  });

  it('refuses what the symbology cannot carry', () => {
    expect(code39('')).toBeNull();
    expect(code39('A_B')).toBeNull();
    expect(code39('A*B')).toBeNull();
  });
});
