import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { LegalDocuments, PlatformApi } from '@core/api/platform.api';
import { BrandAssistantComponent } from '@shared/components/brand-assistant/brand-assistant.component';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';

export interface LegalBlock { heading: boolean; text: string; }

/** The founder's plain text, with "## " marking a heading. It only ever becomes text on the page, never markup. */
export function legalBlocks(text: string): LegalBlock[] {
  return text.split(/\r?\n/).map((l) => l.trim()).filter((l) => l.length > 0)
    .map((l) => (l.startsWith('## ') ? { heading: true, text: l.slice(3).trim() } : { heading: false, text: l }));
}

/** The public Terms of Service and Privacy Policy. Route data says which. */
@Component({
  selector: 'erp-legal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BrandAssistantComponent, RouterLink, NzSpinModule, BrandLogoComponent, ErpDatePipe, StatePanelComponent],
  styleUrl: './legal.page.scss',
  templateUrl: './legal.page.html',
})
export class LegalPage {
  private readonly api = inject(PlatformApi);
  readonly kind: 'terms' | 'privacy' = inject(ActivatedRoute).snapshot.data['kind'] === 'privacy' ? 'privacy' : 'terms';
  readonly title = this.kind === 'privacy' ? 'Privacy Policy' : 'Terms of Service';
  readonly docs = signal<LegalDocuments | null>(null);
  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly blocks = computed(() => {
    const d = this.docs();
    return d ? legalBlocks(this.kind === 'privacy' ? d.privacy : d.terms) : [];
  });

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.api.legal().subscribe({
      next: (d) => { this.docs.set(d); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }
}
