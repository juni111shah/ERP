import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { PortalProjectsComponent } from './portal-projects.component';

describe('customer portal projects', () => {
  it('shows progress and lets the customer sign a pending sign-off with a typed name', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(PortalProjectsComponent);
    fixture.detectChanges();
    http.expectOne((r) => r.url.endsWith('/m/projects/portal/projects')).flush([{ id: 'p1', number: 'PRJ-1', name: 'Fit-out', status: 'Active', percentComplete: 40, milestones: [{ id: 'm1', title: 'Phase 1', dueDate: '2026-07-01', status: 'Pending' }] }]);
    http.expectOne((r) => r.url.endsWith('/m/projects/portal/acceptances')).flush([{ id: 'a1', projectId: 'p1', projectName: 'Fit-out', title: 'Phase 1 delivered', status: 'Pending', requestedAt: '2026-06-01T00:00:00Z' }]);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="project-PRJ-1"]')?.textContent).toContain('Fit-out');
    expect(el.querySelector('[data-testid="signoff-a1"]')).not.toBeNull();

    const c = fixture.componentInstance;
    c.name = 'Dana Customer'; c.signature = 'Dana Customer';
    c.answer({ id: 'a1', projectId: 'p1', projectName: 'Fit-out', title: 'x', status: 'Pending', requestedAt: '' }, true);
    const sign = http.expectOne((r) => r.url.endsWith('/m/projects/portal/acceptances/a1/sign'));
    expect(sign.request.method).toBe('POST');
    expect(sign.request.body).toEqual({ accept: true, signedByName: 'Dana Customer', signature: 'Dana Customer', comment: null, signatureImage: null });
    sign.flush({ id: 'a1', status: 'Signed' });
    http.match(() => true).forEach((r) => r.flush([]));
  });
});
