import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { parseAssistantText } from '@core/format/assistant-text';

/** Shows an assistant's reply. Everything is an Angular text binding, so nothing in a reply can become markup. */
@Component({
  selector: 'erp-assistant-text',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  styleUrl: './assistant-text.component.scss',
  templateUrl: './assistant-text.component.html',
})
export class AssistantTextComponent {
  readonly text = input.required<string>();
  /** A link in the reply was followed. */
  readonly navigated = output<void>();
  readonly blocks = computed(() => parseAssistantText(this.text()));
}
