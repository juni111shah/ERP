import { AfterContentChecked, ChangeDetectionStrategy, Component, ComponentRef, DestroyRef, Directive, DoCheck, ElementRef, Injector, NgModule, ViewContainerRef, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, ControlContainer, FormControlName, NgModel, PristineChangeEvent, TouchedChangeEvent } from '@angular/forms';
import { NzFormControlComponent, NzFormModule } from 'ng-zorro-antd/form';
import { Subscription } from 'rxjs';
import { tNow } from '../i18n/translate-now';
import { formErrorAttempts } from './form-errors';

/** The parts of ng-zorro's form control this directive steers. form-rules.spec.ts fails if an upgrade changes them. */
interface FormControlInternals {
  validateControl: AbstractControl | NgModel | null;
  defaultValidateControl?: unknown;
  nzValidateStatus: unknown;
  validateControlStatus(status: string, type?: string): boolean;
  setStatus(): void;
  cdr: { markForCheck(): void };
}

/**
 * When a field shows its error: not while it is being filled in for the first time, but from the moment it is left.
 * From then on the error follows every keystroke, so it clears as soon as the value is right.
 */
@Directive({
  selector: 'nz-form-control',
  standalone: true,
  host: { '(focusin)': 'focused = true', '(focusout)': 'onFocusOut($event)' },
})
export class FieldTimingDirective implements AfterContentChecked {
  private readonly host = inject(NzFormControlComponent) as unknown as FormControlInternals;
  private readonly el = inject(ElementRef<HTMLElement>).nativeElement as HTMLElement;
  private readonly destroyRef = inject(DestroyRef);
  private watched: AbstractControl | null = null;
  private events = Subscription.EMPTY;
  focused = false;

  constructor() {
    const original = this.host.validateControlStatus;
    this.host.validateControlStatus = (status, type) => {
      const control = this.host.validateControl;
      if (status === 'INVALID' && this.focused && control?.dirty && !control.touched) return false;
      return original.call(this.host, status, type);
    };
    this.destroyRef.onDestroy(() => this.events.unsubscribe());
  }

  ngAfterContentChecked(): void {
    // A page that builds a new form for each record leaves ng-zorro watching the control of the old one.
    const named = this.host.defaultValidateControl;
    if (named instanceof FormControlName && named.control && this.host.validateControl !== named.control) this.host.nzValidateStatus = named;
    const current = this.host.validateControl;
    const control = current instanceof NgModel ? current.control : current;
    if (control === this.watched) return;
    this.watched = control;
    this.events.unsubscribe();
    // ng-zorro only looks again when the value changes; being touched or marked at submit has to count too.
    this.events = control?.events.subscribe((e) => { if (e instanceof TouchedChangeEvent || e instanceof PristineChangeEvent) this.refresh(); }) ?? Subscription.EMPTY;
  }

  onFocusOut(event: FocusEvent): void {
    if (event.relatedTarget instanceof Node && this.el.contains(event.relatedTarget)) return;
    this.focused = false;
    this.refresh();
  }

  private refresh(): void {
    this.host.setStatus();
    this.host.cdr.markForCheck();
  }
}

export interface FormErrorItem { key: string; label: string; message: string; target: HTMLElement; }

/** Finds what is wrong by reading the form as drawn, so hidden fields stay out and labels are already in the user's language. */
export function collectFormErrors(form: HTMLElement): FormErrorItem[] {
  const leaves = Array.from(form.querySelectorAll<HTMLElement>('.ng-invalid'))
    .filter((el) => el.tagName !== 'FORM' && !el.querySelector('.ng-invalid') && (el.classList.contains('ng-touched') || el.classList.contains('ng-dirty')));
  const items: FormErrorItem[] = [];
  leaves.forEach((el, index) => {
    const label = fieldLabel(el);
    if (!label) return;
    const item = el.closest('nz-form-item');
    const row = el.closest('tr');
    items.push({
      key: `${index}:${label}`,
      label: row && row.rowIndex > 0 ? `${label} (${row.rowIndex})` : label,
      message: item?.querySelector('.ant-form-item-explain-error')?.textContent?.trim() ?? '',
      target: focusTarget(el),
    });
  });
  return items;
}

function fieldLabel(el: HTMLElement): string {
  const label = el.closest('nz-form-item')?.querySelector('nz-form-label label')?.textContent?.trim();
  if (label) return label;
  const inner = focusTarget(el);
  return el.getAttribute('aria-label') ?? inner.getAttribute('aria-label') ?? inner.getAttribute('placeholder') ?? '';
}

function focusTarget(el: HTMLElement): HTMLElement {
  if (el.matches('input, textarea, select, button')) return el;
  return el.querySelector<HTMLElement>('input:not([type=hidden]), textarea, select, button, [tabindex]') ?? el;
}

/** The list at the top of a form after a failed submit. It shrinks as fields are fixed and goes away with the last one. */
@Component({
  selector: 'erp-form-error-summary',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (items().length) {
      <div #box class="erp-form-summary" role="alert" tabindex="-1" data-testid="form-error-summary">
        <strong class="erp-form-summary__title">{{ heading() }}</strong>
        <ul>
          @for (i of items(); track i.key) {
            <li><button type="button" (click)="go(i)" data-testid="form-error-link">{{ i.label }}@if (i.message) { <span>: {{ i.message }}</span> }</button></li>
          }
        </ul>
      </div>
    }`,
})
export class FormErrorSummaryComponent {
  readonly items = signal<FormErrorItem[]>([]);
  private readonly box = viewChild<ElementRef<HTMLElement>>('box');

  heading(): string {
    const n = this.items().length;
    return n === 1 ? tNow('ui.form.summaryOne', 'Fix this field to continue') : tNow('ui.form.summaryMany', 'Fix these {n} fields to continue', { n });
  }

  focusBox(): void {
    const box = this.box()?.nativeElement;
    box?.focus({ preventScroll: true });
    box?.scrollIntoView({ block: 'nearest' });
  }

  go(item: FormErrorItem): void {
    item.target.focus({ preventScroll: true });
    item.target.scrollIntoView({ block: 'center' });
  }
}

/** Gives every `<form nz-form>` its summary, shown when showFormErrors() is called for that form. */
@Directive({ selector: 'form[nz-form]', standalone: true })
export class FormErrorSummaryDirective implements DoCheck {
  private readonly container = inject(ControlContainer, { self: true, optional: true });
  private readonly el = inject(ElementRef<HTMLElement>).nativeElement as HTMLElement;
  private readonly vcr = inject(ViewContainerRef);
  private summary?: ComponentRef<FormErrorSummaryComponent>;
  private watched: AbstractControl | null = null;
  private events = Subscription.EMPTY;
  private readonly injector = inject(Injector);
  private scheduled = false;
  private shown = false;
  private focusWanted = false;

  constructor() {
    formErrorAttempts.pipe(takeUntilDestroyed()).subscribe((form) => { if (form === this.container?.control) this.open(); });
    inject(DestroyRef).onDestroy(() => this.events.unsubscribe());
  }

  /** A drawer builds a new form each time it opens, and that starts with no list. */
  ngDoCheck(): void {
    const control = this.container?.control ?? null;
    if (control === this.watched) return;
    this.watched = control;
    this.events.unsubscribe();
    this.close();
    this.events = control?.events.subscribe(() => { if (this.shown) this.schedule(); }) ?? Subscription.EMPTY;
  }

  private open(): void {
    this.shown = true;
    this.focusWanted = true;
    this.schedule();
  }

  private close(): void {
    this.shown = false;
    this.summary?.instance.items.set([]);
  }

  /** After the next render, when the fields have their classes and messages. */
  private schedule(): void {
    if (this.scheduled) return;
    this.scheduled = true;
    afterNextRender(() => {
      this.scheduled = false;
      if (!this.shown) return;
      const items = collectFormErrors(this.el);
      if (!items.length) { this.close(); return; }
      if (!this.summary) {
        this.summary = this.vcr.createComponent(FormErrorSummaryComponent);
        this.el.prepend(this.summary.location.nativeElement);
      }
      this.summary.instance.items.set(items);
      this.summary.changeDetectorRef.detectChanges();
      if (this.focusWanted) this.summary.instance.focusBox();
      this.focusWanted = false;
    }, { injector: this.injector });
  }
}

/** NzFormModule plus the app's form rules. Pages import this one, so every form follows them. */
@NgModule({
  imports: [NzFormModule, FieldTimingDirective, FormErrorSummaryDirective],
  exports: [NzFormModule, FieldTimingDirective, FormErrorSummaryDirective],
})
export class ErpFormModule {}
