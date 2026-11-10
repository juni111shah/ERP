import { carouselOffset, lastMonths } from './pricing-mockups';

describe('lastMonths', () => {
  const now = new Date(2026, 10, 15); // November 2026

  it('gives the months before this one, oldest first', () => {
    expect(lastMonths(3, false, now)).toEqual(['Aug', 'Sep', 'Oct']);
  });

  it('adds the year the way the web chart does, across a new year', () => {
    const months = lastMonths(12, true, now);
    expect(months[0]).toBe('Nov 25');
    expect(months[2]).toBe('Jan 26');
    expect(months[11]).toBe('Oct 26');
  });
});

describe('carouselOffset', () => {
  it('puts the chosen phone at the front', () => {
    expect(carouselOffset(2, 2, 5)).toBe(0);
  });

  it('places the neighbours either side', () => {
    expect(carouselOffset(3, 2, 5)).toBe(1);
    expect(carouselOffset(1, 2, 5)).toBe(-1);
  });

  it('goes the short way round the ends', () => {
    expect(carouselOffset(0, 4, 5)).toBe(1);
    expect(carouselOffset(4, 0, 5)).toBe(-1);
    expect(carouselOffset(3, 0, 5)).toBe(-2);
  });

  it('never moves a phone further than half way round', () => {
    for (let active = 0; active < 5; active++) {
      for (let i = 0; i < 5; i++) expect(Math.abs(carouselOffset(i, active, 5))).toBeLessThanOrEqual(2);
    }
  });
});
