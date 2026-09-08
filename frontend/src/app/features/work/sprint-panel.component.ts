import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { SearchableSelectComponent, SearchableSelectOption } from '../../shared/searchable-select/searchable-select.component';

export interface WorkSprint {
  id: string; projectId: string; name: string; goal?: string; startsOn: string; endsOn: string;
  status: 'Planned' | 'Active' | 'Completed'; totalItems: number; completedItems: number;
  totalPoints: number; completedPoints: number; version: number;
}

@Component({
  selector: 'app-sprint-panel',
  imports: [DatePipe, ReactiveFormsModule, MatButtonModule, MatIconModule, MatProgressBarModule, SearchableSelectComponent],
  template: `
    <section class="sprint-panel" aria-label="Sprint planning">
      <div class="report-head"><div><h2>Plan a focused delivery cycle</h2><p>Create a sprint, add work from ticket details, then start when the team is ready.</p></div>
        @if (canPlan()) { <button mat-flat-button (click)="showCreate.set(!showCreate())"><mat-icon>add</mat-icon>New sprint</button> }
      </div>
      @if (error()) { <div class="error-banner" role="alert">{{ error() }}</div> }
      @if (message()) { <p class="success-banner" role="status">{{ message() }}</p> }
      @if (showCreate() && canPlan()) {
        <form class="sprint-create" [formGroup]="form" (ngSubmit)="create()">
          <label>Sprint name<input formControlName="name" placeholder="e.g. September · Employee experience" maxlength="100"></label>
          <label>Start date<input type="date" formControlName="startsOn"></label>
          <label>End date<input type="date" formControlName="endsOn"></label>
          <label class="wide">Sprint goal<textarea formControlName="goal" placeholder="What will this sprint deliver?" maxlength="2000"></textarea></label>
          <button mat-flat-button type="submit" [disabled]="busy() || form.invalid">Create sprint</button>
          <button mat-button type="button" (click)="showCreate.set(false)">Cancel</button>
        </form>
      }
      <div class="sprint-grid">
        @for (sprint of sprints(); track sprint.id) {
          <article class="sprint-card" [class.sprint-active]="sprint.status === 'Active'">
            <div class="sprint-card-head"><span class="work-status" [class.inprogress]="sprint.status === 'Active'">{{ sprint.status }}</span><span>{{ sprint.startsOn | date:'d MMM' }} – {{ sprint.endsOn | date:'d MMM yyyy' }}</span></div>
            <h3>{{ sprint.name }}</h3><p>{{ sprint.goal || 'No sprint goal added.' }}</p>
            <mat-progress-bar mode="determinate" [value]="sprint.totalItems ? sprint.completedItems / sprint.totalItems * 100 : 0" [attr.aria-label]="sprint.name + ' completion'"></mat-progress-bar>
            <div class="sprint-totals"><span>{{ sprint.completedItems }} / {{ sprint.totalItems }} completed</span><span>{{ sprint.completedPoints }} / {{ sprint.totalPoints }} points</span></div>
            <div class="sprint-actions"><button mat-stroked-button (click)="selected.emit(sprint.id)">View work</button>
              @if (canPlan() && sprint.status === 'Planned') { <button mat-flat-button [disabled]="busy()" (click)="change(sprint, 'Active')">Start sprint</button> }
              @if (canPlan() && sprint.status === 'Active') { <button mat-flat-button [disabled]="busy()" (click)="completing.set(sprint)">Complete sprint</button> }
            </div>
          </article>
        } @empty { <div class="work-empty"><mat-icon>flag</mat-icon><h3>No sprints yet</h3><p>Your board works without sprints. Add a delivery cycle when your team needs one.</p></div> }
      </div>
      @if (completing(); as sprint) {
        <div class="sprint-complete" role="region" aria-label="Complete sprint">
          <h3>Complete {{ sprint.name }}</h3><p>Finished tickets remain in this sprint. Choose where unfinished tickets should go.</p>
          <label>Move unfinished tickets to<app-searchable-select #destination [options]="completionOptions(sprint.id)" placeholder="Unscheduled backlog" searchPlaceholder="Search sprints" /></label>
          <button mat-flat-button [disabled]="busy()" (click)="change(sprint, 'Completed', destination.value)">Confirm completion</button>
          <button mat-button (click)="completing.set(null)">Keep sprint active</button>
        </div>
      }
    </section>
  `,
})
export class SprintPanelComponent {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  readonly projectId = input.required<string>();
  readonly canPlan = input(false);
  readonly selected = output<string>();
  readonly changed = output<void>();
  readonly sprints = signal<WorkSprint[]>([]);
  readonly busy = signal(false);
  readonly showCreate = signal(false);
  readonly completing = signal<WorkSprint | null>(null);
  readonly error = signal('');
  readonly message = signal('');
  readonly form = this.fb.nonNullable.group({ name: ['', Validators.required], goal: [''], startsOn: ['', Validators.required], endsOn: ['', Validators.required] });

  constructor() { effect(() => { const id = this.projectId(); this.completing.set(null); this.error.set(''); if (id) this.load(id); }); }
  completionOptions(currentSprintId: string): SearchableSelectOption[] {
    return [{ value: '', label: 'Unscheduled backlog' }, ...this.sprints()
      .filter(sprint => sprint.id !== currentSprintId && sprint.status !== 'Completed')
      .map(sprint => ({ value: sprint.id, label: sprint.name }))];
  }
  load(id = this.projectId()): void {
    this.api.get<WorkSprint[]>(`/work/projects/${id}/sprints`).subscribe({ next: rows => { if (id === this.projectId()) this.sprints.set(rows); }, error: e => this.error.set(e.error?.detail ?? 'Unable to load sprints.') });
  }
  create(): void {
    if (this.form.invalid || this.busy()) return;
    this.busy.set(true); this.error.set('');
    this.api.post(`/work/projects/${this.projectId()}/sprints`, this.form.getRawValue()).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => { this.showCreate.set(false); this.form.reset(); this.load(); this.changed.emit(); this.message.set('Sprint created. Open a ticket to add it to the sprint.'); },
      error: e => this.error.set(e.error?.detail ?? 'Unable to create sprint.'),
    });
  }
  change(sprint: WorkSprint, status: 'Active' | 'Completed', destination?: string): void {
    if (this.busy()) return;
    this.busy.set(true); this.error.set('');
    this.api.put(`/work/sprints/${sprint.id}/status`, { status, version: sprint.version, moveUnfinishedToSprintId: destination || null }).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => { this.completing.set(null); this.load(); this.changed.emit(); this.message.set(status === 'Active' ? 'Sprint started.' : 'Sprint completed and unfinished work moved.'); },
      error: e => this.error.set(e.error?.detail ?? 'Unable to update sprint.'),
    });
  }
}
