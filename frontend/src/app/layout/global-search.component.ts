import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { API_BASE_URL } from '../core/api/api-config';

interface SearchHit { category: string; title: string; subtitle?: string; link: string; }

/** Header search box: one query across core and enabled modules; picking a hit navigates to the record. */
@Component({
  selector: 'erp-global-search',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzInputModule, NzIconModule],
  styles: [`
    :host { position: relative; display: block; width: 360px; max-width: 40vw; }
    .panel { position: absolute; top: 48px; inset-inline: 0; background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); border-radius: 8px; box-shadow: 0 6px 24px rgba(0,0,0,.12); max-height: 420px; overflow: auto; z-index: 1000; line-height: 1.4; }
    .cat { padding: 8px 12px 2px; font-size: 11px; text-transform: uppercase; letter-spacing: .04em; color: var(--erp-color-text-tertiary); }
    .hit { padding: 6px 12px; cursor: pointer; }
    .hit:hover { background: var(--erp-color-bg-canvas); }
    .sub { font-size: 12px; color: var(--erp-color-text-tertiary); }
    .empty { padding: 12px; color: var(--erp-color-text-tertiary); }
  `],
  template: `
    <nz-input-group [nzPrefix]="icon"><input nz-input placeholder="Search records" [ngModel]="term()" (ngModelChange)="type($event)" (focus)="open.set(true)" (blur)="close()" data-testid="global-search" /></nz-input-group>
    <ng-template #icon><span nz-icon nzType="search"></span></ng-template>
    @if (open() && term().trim().length >= 2) {
      <div class="panel" data-testid="search-results">
        @for (h of hits(); track $index; let i = $index) {
          @if (i === 0 || hits()[i - 1].category !== h.category) { <div class="cat">{{ h.category }}</div> }
          <div class="hit" (mousedown)="go(h)" [attr.data-testid]="'search-hit'">
            <div>{{ h.title }}</div>@if (h.subtitle) { <div class="sub">{{ h.subtitle }}</div> }
          </div>
        } @empty { <div class="empty">No matches</div> }
      </div>
    }`,
})
export class GlobalSearchComponent {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly router = inject(Router);
  readonly term = signal('');
  readonly open = signal(false);
  readonly hits = signal<SearchHit[]>([]);
  private readonly typed = new Subject<string>();

  constructor() {
    this.typed.pipe(
      debounceTime(250), distinctUntilChanged(),
      switchMap((q) => q.trim().length < 2 ? of({ hits: [] }) : this.http.get<{ hits: SearchHit[] }>(`${this.base}/search`, { params: { q: q.trim() } }).pipe(catchError(() => of({ hits: [] })))),
    ).subscribe((r) => this.hits.set(r.hits));
  }

  type(value: string): void { this.term.set(value); this.open.set(true); this.typed.next(value); }
  close(): void { setTimeout(() => this.open.set(false), 150); }
  go(hit: SearchHit): void { this.open.set(false); this.term.set(''); this.hits.set([]); void this.router.navigateByUrl(hit.link); }
}
