import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { parseAssistantText } from '@core/format/assistant-text';

/** Shows an assistant's reply. Everything is an Angular text binding, so nothing in a reply can become markup. */
@Component({
  selector: 'erp-assistant-text',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  styles: [`
    :host { display: block; line-height: 1.55; overflow-wrap: anywhere; }
    p { margin: 0 0 6px; }
    p:last-child { margin-bottom: 0; }
    ul, ol { margin: 0 0 6px; padding-inline-start: 20px; }
    ul:last-child, ol:last-child { margin-bottom: 0; }
    a { color: var(--erp-color-primary-text); text-decoration: underline; }
  `],
  template: `
    @for (b of blocks(); track $index) {
      @if (b.kind === 'paragraph') {
        <p dir="auto">@for (part of b.parts; track $index) {
          @if (part.link) { <a [routerLink]="part.link" (click)="navigated.emit()">{{ part.text }}</a> }
          @else if (part.bold) { <strong>{{ part.text }}</strong> }
          @else { <span>{{ part.text }}</span> }
        }</p>
      } @else if (b.ordered) {
        <ol dir="auto">@for (item of b.items; track $index) { <li>@for (part of item; track $index) {
          @if (part.link) { <a [routerLink]="part.link" (click)="navigated.emit()">{{ part.text }}</a> }
          @else if (part.bold) { <strong>{{ part.text }}</strong> }
          @else { <span>{{ part.text }}</span> }
        }</li> }</ol>
      } @else {
        <ul dir="auto">@for (item of b.items; track $index) { <li>@for (part of item; track $index) {
          @if (part.link) { <a [routerLink]="part.link" (click)="navigated.emit()">{{ part.text }}</a> }
          @else if (part.bold) { <strong>{{ part.text }}</strong> }
          @else { <span>{{ part.text }}</span> }
        }</li> }</ul>
      }
    }`,
})
export class AssistantTextComponent {
  readonly text = input.required<string>();
  /** A link in the reply was followed. */
  readonly navigated = output<void>();
  readonly blocks = computed(() => parseAssistantText(this.text()));
}
