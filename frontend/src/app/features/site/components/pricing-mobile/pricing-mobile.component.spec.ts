import { TestBed } from '@angular/core/testing';
import { NzIconService } from 'ng-zorro-antd/icon';
import { PricingMobileComponent } from './pricing-mobile.component';

describe('PricingMobileComponent', () => {
  const setup = () => {
    TestBed.configureTestingModule({ providers: [{ provide: NzIconService, useValue: { getRenderedContent: () => ({ subscribe: () => ({ unsubscribe() {} }) }), changeAssetsSource() {}, addIcon() {}, fetchFromIconfont() {} } }] });
    const fixture = TestBed.createComponent(PricingMobileComponent);
    fixture.detectChanges();
    return { fixture, slots: [...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('.slot')] };
  };
  const press = (el: Element, key: string) => el.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));

  it('lets the keyboard reach every phone in the carousel', () => {
    const { slots } = setup();
    expect(slots.length).toBeGreaterThan(1);
    for (const slot of slots) {
      expect(slot.getAttribute('tabindex')).toBe('0');
      expect(slot.getAttribute('role')).toBe('button');
    }
  });

  it('turns the phone chosen with Enter or Space to the front', () => {
    const { fixture, slots } = setup();
    const component = fixture.componentInstance;
    press(slots[2], 'Enter');
    expect(component.active()).toBe(2);
    press(slots[1], ' ');
    expect(component.active()).toBe(1);
  });
});
