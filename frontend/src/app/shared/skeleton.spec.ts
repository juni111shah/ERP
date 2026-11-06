import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ErpTableModule } from './erp-table.module';
import { SkeletonComponent } from './skeleton.component';

@Component({
  standalone: true,
  imports: [ErpTableModule],
  template: `
    <nz-table [nzData]="rows()" [nzLoading]="loading()" [nzShowPagination]="false" [nzFrontPagination]="false" [nzPageSize]="pageSize()">
      <thead><tr><th>Name</th><th>Status</th><th nzAlign="right">Amount</th></tr></thead>
      <tbody>@for (r of rows(); track r) { <tr class="real"><td>{{ r }}</td><td>ok</td><td>1</td></tr> }</tbody>
    </nz-table>`,
})
class TableHost {
  readonly rows = signal<string[]>([]);
  readonly loading = signal(true);
  readonly pageSize = signal(10);
}

describe('loading shapes', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideNoopAnimations()] }));

  describe('a loading table', () => {
    const setup = () => { const f = TestBed.createComponent(TableHost); f.detectChanges(); f.detectChanges(); return f; };
    const shapes = (f: { nativeElement: HTMLElement }) => f.nativeElement.querySelectorAll('.erp-table-skeleton__row');

    it('draws rows of its own columns while nothing has arrived', () => {
      const f = setup();
      expect(shapes(f).length).toBe(8);
      expect(shapes(f)[0].querySelectorAll('td').length).toBe(3);
      expect(f.nativeElement.querySelector('nz-table').getAttribute('aria-busy')).toBe('true');
    });

    it('does not draw more rows than the page holds', () => {
      const f = TestBed.createComponent(TableHost);
      f.componentInstance.pageSize.set(3);
      f.detectChanges(); f.detectChanges();
      expect(shapes(f).length).toBe(3);
    });

    it('replaces them with the data', () => {
      const f = setup();
      f.componentInstance.rows.set(['a', 'b']);
      f.componentInstance.loading.set(false);
      f.detectChanges(); f.detectChanges();
      expect(shapes(f).length).toBe(0);
      expect(f.nativeElement.querySelectorAll('tr.real').length).toBe(2);
      expect(f.nativeElement.querySelector('nz-table').getAttribute('aria-busy')).toBe('false');
    });

    it('keeps the rows it has while they reload', () => {
      const f = setup();
      f.componentInstance.rows.set(['a']);
      f.detectChanges(); f.detectChanges();
      expect(shapes(f).length).toBe(0);
      expect(f.nativeElement.querySelectorAll('tr.real').length).toBe(1);
    });
  });

  describe('erp-skeleton', () => {
    it('tells assistive technology the page is loading', () => {
      const f = TestBed.createComponent(SkeletonComponent);
      f.detectChanges();
      const root = f.nativeElement.querySelector('[data-testid="skeleton"]');
      expect(root.getAttribute('role')).toBe('status');
      expect(root.getAttribute('aria-busy')).toBe('true');
    });

    it('draws a circle and a short bar for each person in a list', () => {
      const f = TestBed.createComponent(SkeletonComponent);
      f.componentRef.setInput('kind', 'list');
      f.componentRef.setInput('rows', 3);
      f.detectChanges();
      expect(f.nativeElement.querySelectorAll('.erp-skel--circle').length).toBe(3);
      expect(f.nativeElement.querySelectorAll('.erp-skel--bar').length).toBe(3);
      expect(f.nativeElement.querySelectorAll('.erp-skel--line').length).toBe(3);
    });
  });
});
