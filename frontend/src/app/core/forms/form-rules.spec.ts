import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { NzInputModule } from 'ng-zorro-antd/input';
import { ErpFormModule } from './erp-form.module';
import { showFormErrors } from './form-errors';

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, ErpFormModule, NzInputModule],
  template: `
    <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="submit()">
      <nz-form-item id="email-item">
        <nz-form-label nzFor="email" nzRequired>Email</nz-form-label>
        <nz-form-control nzErrorTip="Enter a real email address"><input nz-input id="email" formControlName="email" /></nz-form-control>
      </nz-form-item>
      <nz-form-item>
        <nz-form-label nzFor="name" nzRequired>Name</nz-form-label>
        <nz-form-control nzErrorTip="Enter a name"><input nz-input id="name" formControlName="name" /></nz-form-control>
      </nz-form-item>
      @if (showSecret()) {
        <nz-form-item>
          <nz-form-label nzFor="secret" nzRequired>Secret</nz-form-label>
          <nz-form-control nzErrorTip="Enter it"><input nz-input id="secret" formControlName="secret" /></nz-form-control>
        </nz-form-item>
      }
      <button id="go">Go</button>
    </form>`,
})
class HostComponent {
  readonly showSecret = signal(false);
  form = new FormGroup({
    email: new FormControl('', [Validators.required, Validators.email]),
    name: new FormControl('', Validators.required),
    secret: new FormControl('', Validators.required),
  });
  submit(): void { if (this.form.invalid) showFormErrors(this.form); }
}

describe('form rules', () => {
  let fixture: ComponentFixture<HostComponent>;
  const q = <T extends HTMLElement>(s: string) => fixture.nativeElement.querySelector(s) as T;
  const emailError = () => q('#email-item .ant-form-item-explain-error');
  const type = (el: HTMLInputElement, value: string) => { el.focus(); el.dispatchEvent(new FocusEvent('focusin', { bubbles: true })); el.value = value; el.dispatchEvent(new Event('input', { bubbles: true })); fixture.detectChanges(); };
  const leave = (el: HTMLInputElement) => { el.dispatchEvent(new Event('blur')); el.dispatchEvent(new FocusEvent('focusout', { bubbles: true })); fixture.detectChanges(); };

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent], providers: [provideNoopAnimations()] });
    fixture = TestBed.createComponent(HostComponent);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
  });
  afterEach(() => fixture.nativeElement.remove());

  it('stays quiet while a field is being filled in for the first time', () => {
    type(q('#email'), 'a');
    expect(emailError()).toBeNull();
  });

  it('shows the error when the field is left', () => {
    const email = q<HTMLInputElement>('#email');
    type(email, 'a');
    leave(email);
    expect(emailError()?.textContent).toContain('Enter a real email address');
  });

  it('clears the error on the keystroke that fixes it, and brings it back on the one that breaks it', () => {
    const email = q<HTMLInputElement>('#email');
    type(email, 'a');
    leave(email);
    type(email, 'a@b.co');
    expect(emailError()).toBeNull();
    type(email, 'a@');
    expect(emailError()?.textContent).toContain('Enter a real email address');
  });

  it('shows the error of a field that was never visited after submit', fakeAsync(() => {
    fixture.componentInstance.submit();
    fixture.detectChanges();
    tick();
    expect(emailError()?.textContent).toContain('Enter a real email address');
  }));

  it('keeps following the form when a page swaps in a new one', () => {
    const host = fixture.componentInstance;
    host.form = new FormGroup({
      email: new FormControl('', [Validators.required, Validators.email]),
      name: new FormControl('', Validators.required),
      secret: new FormControl(''),
    });
    fixture.changeDetectorRef.markForCheck();
    fixture.detectChanges();
    host.submit();
    fixture.detectChanges();
    expect(emailError()?.textContent).toContain('Enter a real email address');
  });

  describe('summary', () => {
    const links = () => Array.from(fixture.nativeElement.querySelectorAll('[data-testid="form-error-link"]')) as HTMLButtonElement[];
    const submit = () => { fixture.componentInstance.submit(); fixture.detectChanges(); tick(); fixture.detectChanges(); };

    it('is not there before a failed submit', fakeAsync(() => {
      type(q('#email'), 'a');
      leave(q('#email'));
      tick();
      expect(q('[data-testid="form-error-summary"]')).toBeNull();
    }));

    it('sits at the top of the form and names every broken field and its message', fakeAsync(() => {
      submit();
      expect(q('form').firstElementChild?.tagName).toBe('ERP-FORM-ERROR-SUMMARY');
      expect(links().map((l) => l.textContent?.trim())).toEqual(['Email: Enter a real email address', 'Name: Enter a name']);
    }));

    it('leaves out a field the user cannot see', fakeAsync(() => {
      submit();
      expect(links().some((l) => l.textContent?.includes('Secret'))).toBeFalse();
    }));

    it('moves focus to the field a link names', fakeAsync(() => {
      submit();
      links()[1].click();
      expect(document.activeElement).toBe(q('#name'));
    }));

    it('shrinks as fields are fixed and goes away with the last one', fakeAsync(() => {
      submit();
      fixture.componentInstance.form.patchValue({ email: 'a@b.co' });
      fixture.detectChanges(); tick(); fixture.detectChanges();
      expect(links().length).toBe(1);
      fixture.componentInstance.form.patchValue({ name: 'Sam' });
      fixture.detectChanges(); tick(); fixture.detectChanges();
      expect(q('[data-testid="form-error-summary"]')).toBeNull();
    }));
  });
});
