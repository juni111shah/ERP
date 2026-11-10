import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { KeyboardClickDirective } from './keyboard-click.directive';

@Component({
  standalone: true,
  imports: [KeyboardClickDirective],
  template: `
    <div id="card" erpKeyboardClick (click)="clicks = clicks + 1"><input id="inner" /></div>
    <div id="off" [erpKeyboardClick]="false" (click)="clicks = clicks + 1"></div>
    <table><tr id="row" erpKeyboardClick erpKeyboardClickRole="" (click)="clicks = clicks + 1"><td>x</td></tr></table>`,
})
class Host { clicks = 0; }

describe('KeyboardClickDirective', () => {
  const setup = () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  };
  const press = (el: Element, key: string) => el.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));

  it('is focusable and announced as a button', () => {
    const { root } = setup();
    const card = root.querySelector('#card')!;
    expect(card.getAttribute('tabindex')).toBe('0');
    expect(card.getAttribute('role')).toBe('button');
  });

  it('clicks on Enter and Space', () => {
    const { fixture, root } = setup();
    press(root.querySelector('#card')!, 'Enter');
    press(root.querySelector('#card')!, ' ');
    expect(fixture.componentInstance.clicks).toBe(2);
  });

  it('ignores keys typed in a nested control', () => {
    const { fixture, root } = setup();
    press(root.querySelector('#inner')!, 'Enter');
    expect(fixture.componentInstance.clicks).toBe(0);
  });

  it('does nothing when disabled', () => {
    const { fixture, root } = setup();
    const off = root.querySelector('#off')!;
    expect(off.hasAttribute('tabindex')).toBeFalse();
    press(off, 'Enter');
    expect(fixture.componentInstance.clicks).toBe(0);
  });

  it('keeps the native role when the role is cleared', () => {
    const { root } = setup();
    expect(root.querySelector('#row')!.hasAttribute('role')).toBeFalse();
  });
});
