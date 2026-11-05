import { ChangeDetectionStrategy, Component, ElementRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { statusLabel } from '../projects/projects-ui';
import { PortalAcceptance, PortalProject, PortalService } from './portal.service';

/** The customer's projects in the portal: progress and milestones, and the sign-offs waiting for them (a typed-name signature, or a reason to decline). */
@Component({
  selector: 'erp-portal-projects',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzButtonModule, NzInputModule, NzProgressModule, NzTableModule, NzTagModule],
  template: `
    @if (waiting().length) {
      <h3>Waiting for your sign-off</h3>
      @for (a of waiting(); track a.id) {
        <div class="card" [attr.data-testid]="'signoff-' + a.id">
          <b>{{ a.title }}</b> <span class="muted">{{ a.projectName }}</span>
          <div class="row">
            <input nz-input [(ngModel)]="name" placeholder="Your full name" maxlength="150" aria-label="Your full name" data-testid="signoff-name" />
            <input nz-input [(ngModel)]="signature" placeholder="Type your name to sign" maxlength="150" aria-label="Signature" data-testid="signoff-signature" />
          </div>
          <div class="pad">
            <canvas #pad width="480" height="140" aria-label="Draw your signature here" data-testid="signoff-pad"
                    (pointerdown)="down($event)" (pointermove)="move($event)" (pointerup)="up()" (pointerleave)="up()"></canvas>
            <button nz-button nzSize="small" type="button" (click)="clearPad()" data-testid="signoff-clear">Clear</button>
          </div>
          <input nz-input [(ngModel)]="comment" placeholder="Comment (needed if you decline)" maxlength="1000" aria-label="Comment" style="margin-top:8px" />
          <div class="row">
            <button nz-button nzType="primary" (click)="answer(a, true)" data-testid="signoff-accept">Accept and sign</button>
            <button nz-button nzDanger (click)="answer(a, false)" data-testid="signoff-decline">Decline</button>
          </div>
        </div>
      }
    }
    <h3>Your projects</h3>
    @for (p of projects(); track p.id) {
      <div class="card" [attr.data-testid]="'project-' + p.number">
        <div class="head"><b>{{ p.number }} · {{ p.name }}</b> <nz-tag>{{ label('project', p.status) }}</nz-tag></div>
        <div class="muted">{{ p.startDate || '' }} {{ p.endDate ? ' to ' + p.endDate : '' }} {{ p.managerName ? ' · ' + p.managerName : '' }}</div>
        <nz-progress [nzPercent]="p.percentComplete" nzSize="small"></nz-progress>
        @if (p.milestones.length) {
          <nz-table [nzData]="p.milestones" nzSize="small" [nzShowPagination]="false" [nzFrontPagination]="false">
            <thead><tr><th>Milestone</th><th>Due</th><th>Status</th></tr></thead>
            <tbody>@for (m of p.milestones; track m.id) { <tr><td>{{ m.title }}</td><td>{{ m.dueDate }}</td><td>{{ label('milestone', m.status) }}</td></tr> }</tbody>
          </nz-table>
        }
      </div>
    } @empty { <p class="muted">You have no projects with us right now.</p> }
    @if (answered().length) {
      <h3>Answered sign-offs</h3>
      <nz-table [nzData]="answered()" nzSize="small" [nzShowPagination]="false" [nzFrontPagination]="false">
        <thead><tr><th>Sign-off</th><th>Project</th><th>Result</th><th>By</th><th>On</th></tr></thead>
        <tbody>@for (a of answered(); track a.id) { <tr><td>{{ a.title }}</td><td>{{ a.projectName }}</td><td>{{ label('acceptance', a.status) }}</td><td>{{ a.signedByName }}</td><td>{{ a.signedAt }}</td></tr> }</tbody>
      </nz-table>
    }
  `,
  styles: ['.pad { margin-top:8px } canvas { border:1px dashed #94a3b8; border-radius:6px; background:#fff; touch-action:none; max-width:100%; display:block; margin-bottom:4px } .card { border:1px solid #e2e8f0; border-radius:10px; padding:12px; margin-bottom:12px; background:#fff } .row { display:flex; gap:8px; margin-top:8px } .muted { color:#64748b; font-size:12.5px } .head { display:flex; justify-content:space-between }'],
})
export class PortalProjectsComponent implements OnInit {
  private readonly portal = inject(PortalService);
  private readonly msg = inject(NzMessageService);
  readonly projects = signal<PortalProject[]>([]);
  readonly acceptances = signal<PortalAcceptance[]>([]);
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
    this.portal.projects().pipe(catchError(() => of([] as PortalProject[]))).subscribe((p) => this.projects.set(p));
    this.portal.acceptances().pipe(catchError(() => of([] as PortalAcceptance[]))).subscribe((a) => this.acceptances.set(a));
  }

  answer(a: PortalAcceptance, accept: boolean): void {
    this.portal.sign(a.id, { accept, signedByName: this.name.trim(), signature: this.signature.trim() || null, comment: this.comment.trim() || null, signatureImage: accept ? this.drawn() : null }).subscribe({
      next: () => { this.msg.success(accept ? 'Signed. Thank you.' : 'Declined'); this.name = this.signature = this.comment = ''; this.clearPad(); this.load(); },
      error: (e) => this.msg.error(e?.error?.errors ? Object.values(e.error.errors as Record<string, string[]>)[0]?.[0] ?? 'Check the form.' : e?.error?.detail ?? 'Could not save your answer.'),
    });
  }
}
