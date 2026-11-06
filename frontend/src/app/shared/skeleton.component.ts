import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type SkeletonKind = 'detail' | 'table' | 'list' | 'cards' | 'form' | 'text' | 'document';

/**
 * What a page shows while its content loads: the shape of that content, not a spinner. The bars and their shimmer are styled in
 * theme/_skeleton.scss. To mark up one small slot, use the classes on their own: <span class="erp-skel erp-skel--line"></span>.
 */
@Component({
  selector: 'erp-skeleton',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="erp-skeleton" role="status" aria-busy="true" aria-label="Loading…" [attr.data-kind]="kind()" data-testid="skeleton">
      @switch (kind()) {
        @case ('detail') {
          <div class="erp-skel-row"><span class="erp-skel erp-skel--bar" style="width:120px"></span></div>
          <div class="erp-skel-row">
            @for (i of [1, 2, 3]; track i) { <span class="erp-skel erp-skel--button"></span> }
          </div>
          <div class="erp-skel-grid">
            @for (i of [1, 2, 3, 4]; track i) {
              <div class="erp-skel-card"><span class="erp-skel erp-skel--bar" style="width:55%"></span><span class="erp-skel erp-skel--title" style="width:40%"></span></div>
            }
          </div>
          <div class="erp-skel-card">
            <span class="erp-skel erp-skel--title" style="width:110px"></span>
            <div class="erp-skel-pairs">
              @for (w of widths(6); track $index) { <div><span class="erp-skel erp-skel--bar" style="width:35%"></span><span class="erp-skel erp-skel--line" [style.width]="w"></span></div> }
            </div>
          </div>
          <div class="erp-skel-card">
            <div class="erp-skel-row">
              @for (i of [1, 2, 3]; track i) { <span class="erp-skel erp-skel--bar" style="width:84px"></span> }
            </div>
            @for (w of widths(rows()); track $index) { <span class="erp-skel erp-skel--line" [style.width]="w"></span> }
          </div>
        }
        @case ('table') {
          <div class="erp-skel-card erp-skel-table">
            <div class="erp-skel-table__head">
              @for (i of columnList(); track i) { <span class="erp-skel erp-skel--bar"></span> }
            </div>
            @for (r of rowList(); track r) {
              <div class="erp-skel-table__row">
                @for (w of widths(columns()); track $index) { <span><span class="erp-skel erp-skel--line" [style.width]="w"></span></span> }
              </div>
            }
          </div>
        }
        @case ('list') {
          @for (w of widths(rows()); track $index) {
            <div class="erp-skel-person">
              <span class="erp-skel erp-skel--circle"></span>
              <div><span class="erp-skel erp-skel--bar" style="width:30%"></span><span class="erp-skel erp-skel--line" [style.width]="w"></span></div>
            </div>
          }
        }
        @case ('cards') {
          <div class="erp-skel-grid erp-skel-grid--wide">
            @for (i of rowList(); track i) {
              <div class="erp-skel-card">
                <span class="erp-skel erp-skel--title" style="width:50%"></span>
                <span class="erp-skel erp-skel--figure"></span>
                @for (w of widths(4); track $index) { <span class="erp-skel erp-skel--line" [style.width]="w"></span> }
              </div>
            }
          </div>
        }
        @case ('form') {
          <div class="erp-skel-card">
            <span class="erp-skel erp-skel--title" style="width:160px"></span>
            @for (r of rowList(); track r) {
              <div class="erp-skel-field"><span class="erp-skel erp-skel--bar" style="width:110px"></span><span class="erp-skel erp-skel--input"></span></div>
            }
          </div>
        }
        @case ('text') {
          <span class="erp-skel erp-skel--title" style="width:45%"></span>
          @for (w of widths(rows()); track $index) { <span class="erp-skel erp-skel--line" [style.width]="w"></span> }
        }
        @case ('document') {
          <div class="erp-skel-card erp-skel-document">
            <div class="erp-skel-document__head"><span class="erp-skel erp-skel--title" style="width:30%"></span><span class="erp-skel erp-skel--bar" style="width:18%"></span></div>
            @for (w of widths(3); track $index) { <span class="erp-skel erp-skel--bar" [style.width]="w"></span> }
            <div class="erp-skel-table">
              @for (r of rowList(); track r) {
                <div class="erp-skel-table__row">
                  @for (w of widths(4); track $index) { <span><span class="erp-skel erp-skel--line" [style.width]="w"></span></span> }
                </div>
              }
            </div>
          </div>
        }
      }
    </div>`,
})
export class SkeletonComponent {
  readonly kind = input<SkeletonKind>('text');
  /** Lines of text, table rows, list people, cards or form fields, depending on the kind. */
  readonly rows = input(5);
  readonly columns = input(5);

  readonly rowList = computed(() => Array.from({ length: this.rows() }, (_, i) => i));
  readonly columnList = computed(() => Array.from({ length: this.columns() }, (_, i) => i));

  /** Text never has lines of one length, so neither does its shape. */
  widths(count: number): string[] {
    const pattern = ['92%', '78%', '86%', '64%', '96%', '71%'];
    return Array.from({ length: count }, (_, i) => pattern[i % pattern.length]);
  }
}
