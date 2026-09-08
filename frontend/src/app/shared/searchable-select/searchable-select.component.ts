import { Component, ElementRef, EventEmitter, HostListener, Input, Output, forwardRef, inject, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export interface SearchableSelectOption {
  value: string;
  label: string;
  disabled?: boolean;
}

@Component({
  selector: 'app-searchable-select',
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => SearchableSelectComponent),
    multi: true,
  }],
  template: `
    <button class="select-trigger" type="button" [disabled]="disabled" (click)="toggle()"
      [attr.aria-expanded]="open()" aria-haspopup="listbox">
      <span [class.placeholder]="!selectedLabel()">{{ selectedLabel() || placeholder }}</span>
      <span class="chevron">⌄</span>
    </button>
    @if (open()) {
      <div class="select-panel">
        <div class="select-search">
          <span>⌕</span>
          <input #searchInput type="search" [value]="query()" (input)="query.set($any($event.target).value)"
            (keydown.escape)="close()" [placeholder]="searchPlaceholder" aria-label="Search options" />
        </div>
        <div class="select-options" role="listbox">
          @for (option of filteredOptions(); track option.value) {
            <button type="button" role="option" [attr.aria-selected]="option.value === value"
              [class.selected]="option.value === value" [disabled]="option.disabled" (click)="choose(option.value)">
              <span>{{ option.label }}</span>@if (option.value === value) { <b>✓</b> }
            </button>
          } @empty {
            <p>No matching options</p>
          }
        </div>
      </div>
    }
  `,
  styles: [`
    :host { position: relative; display: block; min-width: 0; }
    .select-trigger { width: 100%; height: 40px; min-height: 40px; padding: 0 10px 0 12px; border: 1px solid var(--border-strong); border-radius: 7px; background: var(--card); color: var(--ink); display: flex; align-items: center; justify-content: space-between; gap: 8px; font-family: inherit; font-size: .8125rem; font-weight: 500; line-height: 1.2; text-align: left; cursor: pointer; }
    .select-trigger:focus-visible { outline: 2px solid rgb(var(--brand-rgb) / .3); outline-offset: 1px; }
    .select-trigger:disabled { opacity: .58; cursor: not-allowed; }
    .select-trigger span:first-child { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .placeholder { color: var(--ink-muted); }
    .chevron { color: var(--ink-muted); font-size: .875rem; }
    .select-panel { position: absolute; z-index: 1300; top: calc(100% + 4px); right: 0; left: 0; min-width: 210px; border: 1px solid var(--border); border-radius: 7px; background: var(--card); box-shadow: 0 10px 24px rgb(15 23 42 / .16); overflow: hidden; }
    .select-search { height: 34px; margin: 6px; padding: 0 8px; border: 1px solid var(--border-strong); border-radius: 5px; display: flex; align-items: center; gap: 6px; color: var(--ink-muted); font-size: .75rem; }
    .select-search input { width: 100%; min-width: 0; height: 32px; padding: 0; border: 0; outline: 0; background: transparent; color: var(--ink); font-family: inherit; font-size: .75rem; }
    .select-options { max-height: 200px; overflow-y: auto; padding: 0 4px 4px; }
    .select-options button { width: 100%; min-height: 32px; padding: 6px 8px; border: 0; border-radius: 4px; background: transparent; color: var(--ink); display: flex; align-items: center; justify-content: space-between; gap: 10px; font-family: inherit; font-size: .8125rem; line-height: 1.25; text-align: left; cursor: pointer; }
    .select-options button:hover, .select-options button:focus-visible, .select-options button.selected { background: var(--card-muted); }
    .select-options button:disabled { opacity: .48; cursor: not-allowed; }
    .select-options button b { color: var(--brand); }
    .select-options p { margin: 0; padding: 16px 10px; color: var(--ink-muted); text-align: center; font-size: .78rem; }
  `],
})
export class SearchableSelectComponent implements ControlValueAccessor {
  private readonly host = inject(ElementRef<HTMLElement>);

  @Input() options: SearchableSelectOption[] = [];
  @Input() placeholder = 'Select an option';
  @Input() searchPlaceholder = 'Search...';
  @Input() disabled = false;
  @Input() value = '';
  @Output() readonly valueChange = new EventEmitter<string>();

  readonly open = signal(false);
  readonly query = signal('');
  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  selectedLabel(): string { return this.options.find(option => option.value === this.value)?.label ?? ''; }
  filteredOptions(): SearchableSelectOption[] {
    const query = this.query().trim().toLocaleLowerCase();
    return query ? this.options.filter(option => option.label.toLocaleLowerCase().includes(query)) : this.options;
  }
  toggle(): void {
    if (this.disabled) return;
    this.open.update(value => !value);
    this.query.set('');
    if (this.open()) setTimeout(() =>
      (this.host.nativeElement.querySelector('input[type="search"]') as HTMLInputElement | null)?.focus(),
    );
  }
  close(): void { this.open.set(false); this.onTouched(); }
  choose(value: string): void { this.value = value; this.valueChange.emit(value); this.onChange(value); this.close(); }
  writeValue(value: string | null | undefined): void { this.value = value ?? ''; }
  registerOnChange(fn: (value: string) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(disabled: boolean): void { this.disabled = disabled; }

  @HostListener('document:mousedown', ['$event'])
  onDocumentMouseDown(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) this.close();
  }
}
