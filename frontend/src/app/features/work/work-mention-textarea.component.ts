import { Component, ElementRef, Input, ViewChild, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { WorkProjectMember } from '../../core/models';

type MentionOption = { id: string; name: string; code: string; all: boolean };

@Component({
  selector: 'app-work-mention-textarea',
  imports: [ReactiveFormsModule],
  template: `
    <div class="mention-editor">
      <textarea #editor [formControl]="control" [placeholder]="placeholder" aria-label="{{ label }}"
        (input)="refresh()" (click)="refresh()" (keydown)="onKeydown($event)"></textarea>
      @if (options().length) {
        <div class="mention-options" role="listbox" aria-label="Mention project members">
          @for (option of options(); track option.id; let index = $index) {
            <button type="button" role="option" [attr.aria-selected]="index === activeIndex()"
              [class.active]="index === activeIndex()" (mousedown)="$event.preventDefault()" (click)="choose(option)">
              <span>{{ option.all ? '@All' : '@' + option.name }}</span>
              <small>{{ option.all ? 'All project members' : option.code }}</small>
            </button>
          }
        </div>
      }
      <small class="mention-hint">Type @ and select a project member or @All to notify them.</small>
    </div>
  `,
  styles: [`
    .mention-editor { position: relative; }
    textarea { width: 100%; min-height: 160px; }
    .mention-options { max-height: 240px; overflow: auto; margin-top: 4px; padding: 5px;
      background: var(--card); border: 1px solid var(--border-strong);
      border-radius: 8px; box-shadow: 0 12px 26px #17203322; }
    .mention-options button { display: flex; width: 100%; justify-content: space-between; gap: 12px;
      padding: 9px 11px; border: 0; border-radius: 5px; background: transparent; color: var(--ink);
      text-align: left; cursor: pointer; }
    .mention-options button:hover, .mention-options button.active { background: var(--card-muted); }
    .mention-options small, .mention-hint { color: var(--muted); }
    .mention-hint { display: block; margin-top: 5px; font-size: .75rem; }
  `],
})
export class WorkMentionTextareaComponent {
  @Input({ required: true }) control!: FormControl<string>;
  @Input({ required: true }) members: WorkProjectMember[] = [];
  @Input() placeholder = '';
  @Input() label = 'Work note';
  @ViewChild('editor') private editor?: ElementRef<HTMLTextAreaElement>;
  readonly options = signal<MentionOption[]>([]);
  readonly activeIndex = signal(0);
  private range: { start: number; end: number } | null = null;
  private chosen = new Map<string, string>();
  private allChosen = false;

  selection(): { mentionedEmployeeIds: string[]; mentionAll: boolean } {
    const text = this.control.value;
    return {
      mentionedEmployeeIds: [...this.chosen].filter(([, name]) => text.includes(`@${name}`)).map(([id]) => id),
      mentionAll: this.allChosen && /(^|\W)@All(?!\w)/i.test(text),
    };
  }

  refresh(): void {
    const element = this.editor?.nativeElement;
    if (!element) return;
    const before = element.value.slice(0, element.selectionStart);
    const start = before.lastIndexOf('@');
    if (start < 0 || (start > 0 && /\w/.test(before[start - 1]))) return this.close();
    const query = before.slice(start + 1);
    if (query.length > 40 || /[\n\r.,;:!?()[\]{}]/.test(query)) return this.close();
    const match = query.trim().toLocaleLowerCase();
    const choices: MentionOption[] = [];
    if ('all'.includes(match)) choices.push({ id: 'all', name: 'All', code: '', all: true });
    choices.push(...this.members
      .filter(member => member.canMention && (member.employeeName.toLocaleLowerCase().includes(match)
        || member.employeeNumber.toLocaleLowerCase().includes(match))
      )
      .slice(0, 8).map(member => ({ id: member.employeeId, name: member.employeeName,
        code: member.employeeNumber, all: false })));
    this.range = { start, end: element.selectionStart };
    this.activeIndex.set(0);
    this.options.set(choices);
  }

  onKeydown(event: KeyboardEvent): void {
    const choices = this.options();
    if (!choices.length) return;
    if (event.key === 'Escape') { this.close(); return; }
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeIndex.set((this.activeIndex() + (event.key === 'ArrowDown' ? 1 : choices.length - 1)) % choices.length);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      this.choose(choices[this.activeIndex()]);
    }
  }

  choose(option: MentionOption): void {
    const element = this.editor?.nativeElement;
    if (!element || !this.range) return;
    const inserted = `@${option.name} `;
    const next = element.value.slice(0, this.range.start) + inserted + element.value.slice(this.range.end);
    const caret = this.range.start + inserted.length;
    this.control.setValue(next);
    if (option.all) this.allChosen = true;
    else this.chosen.set(option.id, option.name);
    this.close();
    element.focus();
    element.setSelectionRange(caret, caret);
  }

  private close(): void { this.range = null; this.options.set([]); }
}
