import { Pipe, PipeTransform, inject } from '@angular/core';
import { I18nService } from './i18n.service';

/** `{{ 'common.save' | translate: 'Save' }}`. Impure so it follows language changes (the signal is read inside). */
@Pipe({ name: 'translate', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(I18nService);
  transform(key: string, fallback?: string): string { return this.i18n.t(key, fallback); }
}
