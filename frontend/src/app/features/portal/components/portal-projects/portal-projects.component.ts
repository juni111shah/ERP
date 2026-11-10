import { ChangeDetectionStrategy, Component, ElementRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { statusLabel } from '@features/projects/projects-ui';
import { PortalAcceptance, PortalProject, PortalService } from '../../portal.service';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

/** The customer's projects in the portal: progress and milestones, and the sign-offs waiting for them (a typed-name signature, or a reason to decline). */
@Component({
  selector: 'erp-portal-projects',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, FormsModule, NzButtonModule, NzInputModule, NzProgressModule, NzTableModule, StatePanelComponent, StatusTagComponent],
  templateUrl: './portal-projects.component.html',
  styleUrl: './portal-projects.component.scss',
})
export class PortalProjectsComponent implements OnInit {
  private readonly portal = inject(PortalService);
  private readonly msg = inject(NzMessageService);
  readonly projects = signal<PortalProject[]>([]);
  readonly acceptances = signal<PortalAcceptance[]>([]);
  readonly failed = signal(false);
  name = ''; signature = ''; comment = '';
  private readonly pad = viewChild<ElementRef<HTMLCanvasElement>>('pad');
  private drawing = false;
  private inked = false;

  private point(e: PointerEvent): { x: number; y: number } {
    const c = this.pad()!.nativeElement; const r = c.getBoundingClientRect();
    return { x: (e.clientX - r.left) * (c.width / r.width), y: (e.clientY - r.top) * (c.height / r.height) };
  }
  down(e: PointerEvent): void {
    const ctx = this.pad()?.nativeElement.getContext('2d'); if (!ctx) return;
    this.drawing = true; const p = this.point(e); ctx.lineWidth = 2; ctx.lineCap = 'round'; ctx.strokeStyle = '#0f172a'; ctx.beginPath(); ctx.moveTo(p.x, p.y);
  }
  move(e: PointerEvent): void {
    if (!this.drawing) return; const ctx = this.pad()?.nativeElement.getContext('2d'); if (!ctx) return;
    const p = this.point(e); ctx.lineTo(p.x, p.y); ctx.stroke(); this.inked = true;
  }
  up(): void { this.drawing = false; }
  clearPad(): void { const c = this.pad()?.nativeElement; c?.getContext('2d')?.clearRect(0, 0, c.width, c.height); this.inked = false; }
  /** The drawn signature as a PNG data URL, or null when nothing was drawn. */
  drawn(): string | null { return this.inked ? this.pad()?.nativeElement.toDataURL('image/png') ?? null : null; }

  waiting(): PortalAcceptance[] { return this.acceptances().filter((a) => a.status.toLowerCase() === 'pending'); }
  answered(): PortalAcceptance[] { return this.acceptances().filter((a) => a.status.toLowerCase() !== 'pending'); }
  label = statusLabel;

  ngOnInit(): void { this.load(); }

  load(): void {
    this.failed.set(false);
    // A disabled module answers 403: the customer simply has no projects then.
    const fail = (e: { status?: number }) => { if (e.status !== 403 && e.status !== 404) this.failed.set(true); };
    this.portal.projects().pipe(catchError((e) => { fail(e); return of([] as PortalProject[]); })).subscribe((p) => this.projects.set(p));
    this.portal.acceptances().pipe(catchError((e) => { fail(e); return of([] as PortalAcceptance[]); })).subscribe((a) => this.acceptances.set(a));
  }

  answer(a: PortalAcceptance, accept: boolean): void {
    this.portal.sign(a.id, { accept, signedByName: this.name.trim(), signature: this.signature.trim() || null, comment: this.comment.trim() || null, signatureImage: accept ? this.drawn() : null }).subscribe({
      next: () => { this.msg.success(accept ? `${a.title} signed. Thank you.` : `${a.title} declined`); this.name = this.signature = this.comment = ''; this.clearPad(); this.load(); },
      error: (e) => this.msg.error(e?.error?.errors ? Object.values(e.error.errors as Record<string, string[]>)[0]?.[0] ?? 'Check the form.' : e?.error?.detail ?? 'Could not save your answer.'),
    });
  }
}
