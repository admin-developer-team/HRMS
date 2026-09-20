import { CurrencyPipe, DatePipe, UpperCasePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../../core/toast.service';
import { Employee, PagedResult } from '../../core/models';

interface Policy {
  version: number; salaryBasis: 'annual' | 'monthly'; payableDaysBasis: 'calendar' | 'fixed30' | 'working';
  missingAttendance: 'block' | 'ignore' | 'unpaid'; deductUnpaidLeave: boolean; deductAbsences: boolean;
  deductHalfDays: boolean; standardDailyHours: number; overtimeMultiplier: number;
  pfEmployeeRate: number; pfEmployerRate: number; pfWageCeiling: number;
  esiEmployeeRate: number; esiEmployerRate: number; esiGrossCeiling: number;
  requireStatutoryReview: boolean; releasePayslipsOnApproval: boolean;
}
interface Profile {
  version: number; employeeId: string; basicPercent: number; hraPercentOfBasic: number;
  pfEnabled: boolean; esiEnabled: boolean; monthlyTds: number; professionalTax: number;
  otherMonthlyDeduction: number; overtimeHourlyRate: number | null; taxRegime: 'new' | 'old';
}
interface Run {
  id: string; name: string; periodStart: string; periodEnd: string; paymentDate: string;
  status: string; grossTotal: number; deductionTotal: number; netTotal: number; version: number;
  statutoryReviewedAt: string | null; paymentReference: string | null;
}
interface Item {
  id: string; employeeId: string; basicPay: number; allowances: number; overtimePay: number;
  deductions: number; taxes: number; grossPay: number; netPay: number; breakdownJson: string | null;
}
interface Adjustment { id: string; employeeId: string; code: string; description: string; isEarning: boolean; amount: number; }

@Component({
  selector: 'app-payroll-page',
  imports: [FormsModule, CurrencyPipe, DatePipe, UpperCasePipe],
  template: `
    <main class="payroll-page">
      <header class="page-head"><div><span class="eyebrow">INDIA / PAYROLL OPERATIONS</span><h1>Payroll</h1>
        <p>Configure salary rules, reconcile attendance and leave, review each run, then record disbursement.</p></div>
        <span class="currency-badge">₹ INR · Asia/Kolkata</span></header>
      <nav class="tabs" aria-label="Payroll sections">
        <button [class.active]="tab()==='runs'" (click)="tab.set('runs')">Pay runs</button>
        <button [class.active]="tab()==='policy'" (click)="tab.set('policy')">Company policy</button>
        <button [class.active]="tab()==='profiles'" (click)="tab.set('profiles')">Employee profiles</button>
      </nav>

      @if (tab()==='policy') {
        <section class="card">
          <div class="card-head"><div><span class="eyebrow">STEP 1</span><h2>Company payroll policy</h2><p>Set how monthly pay, attendance, overtime and enrolled statutory contributions are calculated.</p></div></div>
          @if (policy(); as p) {
            <div class="field-grid">
              <label>Employee base salary is recorded as<select [(ngModel)]="p.salaryBasis"><option value="annual">Annual salary</option><option value="monthly">Monthly salary</option></select></label>
              <label>Payable days<select [(ngModel)]="p.payableDaysBasis"><option value="calendar">Calendar days</option><option value="fixed30">Fixed 30 days</option><option value="working">Working days, excluding weekends and holidays</option></select></label>
              <label>Missing attendance<select [(ngModel)]="p.missingAttendance"><option value="block">Block payroll until resolved</option><option value="ignore">Treat as payable</option><option value="unpaid">Deduct as unpaid</option></select></label>
              <label>Standard hours per workday<input type="number" min="1" max="16" step="0.25" [(ngModel)]="p.standardDailyHours"></label>
              <label>Overtime multiplier<input type="number" min="0" max="5" step="0.25" [(ngModel)]="p.overtimeMultiplier"></label>
              <label>PF employee rate<input type="number" min="0" max="0.25" step="0.001" [(ngModel)]="p.pfEmployeeRate"></label>
              <label>PF employer rate<input type="number" min="0" max="0.25" step="0.001" [(ngModel)]="p.pfEmployerRate"></label>
              <label>PF wage ceiling (₹)<input type="number" min="0" step="1" [(ngModel)]="p.pfWageCeiling"></label>
              <label>ESI employee rate<input type="number" min="0" max="0.1" step="0.0001" [(ngModel)]="p.esiEmployeeRate"></label>
              <label>ESI employer rate<input type="number" min="0" max="0.1" step="0.0001" [(ngModel)]="p.esiEmployerRate"></label>
              <label>ESI enrollment reference ceiling (₹)<input type="number" min="0" step="1" [(ngModel)]="p.esiGrossCeiling"></label>
            </div>
            <div class="checks"><label><input type="checkbox" [(ngModel)]="p.deductUnpaidLeave">Deduct approved unpaid leave</label>
              <label><input type="checkbox" [(ngModel)]="p.deductAbsences">Deduct marked absences</label>
              <label><input type="checkbox" [(ngModel)]="p.deductHalfDays">Deduct half days</label>
              <label><input type="checkbox" [(ngModel)]="p.requireStatutoryReview">Require statutory review</label>
              <label><input type="checkbox" [(ngModel)]="p.releasePayslipsOnApproval">Release payslips after approval</label></div>
            <p class="hint">PF and ESI apply only to employees enrolled in their payroll profile. Professional tax and monthly TDS are employee-specific review inputs. Saving a policy resets calculated, unapproved runs.</p>
            <button class="primary" [disabled]="busy()" (click)="savePolicy()">Save company policy</button>
          } @else { <p>Loading company policy…</p> }
        </section>
      }

      @if (tab()==='profiles') {
        <div class="split"><section class="card people"><div class="card-head"><div><span class="eyebrow">STEP 2</span><h2>Employee profiles</h2><p>Review each salary structure before the first calculation.</p></div></div>
          <label class="search-label">Find employee<input placeholder="Name or employee number" [(ngModel)]="employeeSearch" (keyup.enter)="loadEmployees()"></label>
          <button class="secondary" (click)="loadEmployees()">Search</button>
          <div class="people-list">@for (e of employees(); track e.id) { <button [class.selected]="selectedEmployee()?.id===e.id" (click)="selectEmployee(e)"><b>{{ e.fullName }}</b><small>{{ e.employeeNumber }} · {{ e.baseSalary | currency:'INR' }} {{ policy()?.salaryBasis==='monthly' ? '/ month' : '/ year' }}</small></button> } @empty { <p>No employees found.</p> }</div>
          <div class="pager"><button [disabled]="employeePage===1" (click)="changeEmployeePage(-1)">Previous</button><span>Page {{ employeePage }} / {{ employeePages() }}</span><button [disabled]="employeePage>=employeePages()" (click)="changeEmployeePage(1)">Next</button></div>
        </section><section class="card">@if (selectedEmployee(); as e) { <div class="card-head"><div><span class="eyebrow">SALARY STRUCTURE</span><h2>{{ e.fullName }}</h2><p>{{ e.employeeNumber }} · {{ e.salaryCurrency }} salary</p></div></div>
            @if (profile(); as p) { <div class="field-grid">
              <label>Basic (% of salary)<input type="number" min="0" max="100" [(ngModel)]="p.basicPercent"></label>
              <label>HRA (% of basic)<input type="number" min="0" max="100" [(ngModel)]="p.hraPercentOfBasic"></label>
              <label>Monthly TDS reviewed (₹)<input type="number" min="0" [(ngModel)]="p.monthlyTds"></label>
              <label>Professional tax (₹)<input type="number" min="0" [(ngModel)]="p.professionalTax"></label>
              <label>Other monthly deduction (₹)<input type="number" min="0" [(ngModel)]="p.otherMonthlyDeduction"></label>
              <label>Overtime hourly rate override (₹)<input type="number" min="0" [(ngModel)]="p.overtimeHourlyRate"></label>
              <label>Tax regime<select [(ngModel)]="p.taxRegime"><option value="new">New</option><option value="old">Old</option></select></label>
            </div><div class="checks"><label><input type="checkbox" [(ngModel)]="p.pfEnabled">PF enrolled</label><label><input type="checkbox" [(ngModel)]="p.esiEnabled">ESI enrolled</label></div>
            <p class="hint">Confirm enrollment, state professional tax, TDS and salary structure with your payroll specialist before approval.</p>
            <button class="primary" [disabled]="busy()" (click)="saveProfile()">Save employee profile</button>
            } @else { <p>Loading profile…</p> }
          } @else { <div class="empty">Select an employee to configure their payroll.</div> }</section></div>
      }

      @if (tab()==='runs') {
        <div class="split"><section class="card runs"><div class="card-head"><div><span class="eyebrow">STEP 3</span><h2>Pay runs</h2><p>One run per company and month.</p></div></div>
          <div class="create-run"><label>Run name<input [(ngModel)]="newRun.name" placeholder="September 2026 payroll"></label>
            <label>Month<input type="month" [(ngModel)]="newRun.month"></label>
            <label>Payment date<input type="date" [(ngModel)]="newRun.paymentDate"></label>
            <button class="primary" [disabled]="busy()" (click)="createRun()">Create run</button></div>
          <div class="run-list">@for (r of runs(); track r.id) { <button [class.selected]="selectedRun()?.id===r.id" (click)="selectRun(r)"><span><b>{{ r.name }}</b><small>{{ r.periodStart }} – {{ r.periodEnd }}</small></span><span class="run-right"><strong>{{ r.netTotal | currency:'INR' }}</strong><em>{{ r.status }}</em></span></button> } @empty { <p>No pay runs yet.</p> }</div>
        </section><section class="card detail">@if (selectedRun(); as r) { <div class="card-head"><div><span class="eyebrow">{{ r.status | uppercase }} RUN</span><h2>{{ r.name }}</h2><p>Pay date {{ r.paymentDate | date:'mediumDate' }}</p></div></div>
            <div class="totals"><div><small>Gross</small><strong>{{ r.grossTotal | currency:'INR' }}</strong></div><div><small>Deductions & tax</small><strong>{{ r.deductionTotal | currency:'INR' }}</strong></div><div><small>Net pay</small><strong>{{ r.netTotal | currency:'INR' }}</strong></div></div>
            <div class="workflow"><span [class.done]="r.status!=='Draft'">1 Calculate</span><span [class.done]="!!r.statutoryReviewedAt">2 Review</span><span [class.done]="r.status==='Approved'||r.status==='Paid'">3 Approve</span><span [class.done]="r.status==='Paid'">4 Record payment</span></div>
            @if (r.status==='Draft'||r.status==='Processing') { <div class="actions"><button class="primary" [disabled]="busy()" (click)="calculate()">{{ r.status==='Processing'?'Recalculate':'Calculate payroll' }}</button>
              @if (r.status==='Processing') { <button class="secondary" [disabled]="busy()" (click)="review()">Confirm statutory review</button> }
              @if (r.status==='Processing'&&(r.statutoryReviewedAt||policy()?.requireStatutoryReview===false)) { <button class="secondary" [disabled]="busy()" (click)="approve()">Approve run</button> }</div> }
            @if (r.status==='Approved') { <div class="pay-box"><label>Actual payment date<input type="date" [(ngModel)]="paidOn"></label><label>Bank disbursement reference<input [(ngModel)]="paymentReference" placeholder="Bank transaction or batch reference"></label><button class="primary" [disabled]="busy()" (click)="markPaid()">Record payment and release payslips</button></div> }
            @if (r.status==='Paid') { <p class="notice success">Paid · reference {{ r.paymentReference }}. Employee payslips are available in Self service.</p> }
            <div class="subhead"><h3>One-time adjustments</h3><p>Bonuses, reimbursements or one-off deductions for this run. Any change resets an unapproved calculation.</p></div>
            @if (r.status==='Draft'||r.status==='Processing') { <div class="adjust-form"><select aria-label="Employee" [(ngModel)]="newAdjustment.employeeId"><option value="">Employee</option>@for(e of employees();track e.id){<option [value]="e.id">{{ e.fullName }}</option>}</select>
              <input aria-label="Code" [(ngModel)]="newAdjustment.code" placeholder="Code, e.g. BONUS"><input aria-label="Description" [(ngModel)]="newAdjustment.description" placeholder="Description">
              <select aria-label="Adjustment type" [(ngModel)]="newAdjustment.isEarning"><option [ngValue]="true">Earning</option><option [ngValue]="false">Deduction</option></select><input aria-label="Amount" type="number" min="0.01" [(ngModel)]="newAdjustment.amount" placeholder="₹ Amount"><button class="secondary" [disabled]="busy()" (click)="addAdjustment()">Add</button></div> }
            <div class="adjust-list">@for(a of adjustments();track a.id){<div><span>{{ employeeName(a.employeeId) }} · {{ a.code }} <small>{{ a.description }}</small></span><strong [class.negative]="!a.isEarning">{{ a.isEarning?'+':'−' }}{{ a.amount | currency:'INR' }}</strong>@if(r.status==='Draft'||r.status==='Processing'){<button (click)="deleteAdjustment(a.id)">Remove</button>}</div>} @empty {<p>No one-time adjustments.</p>}</div>
            <div class="subhead"><h3>Calculated employee pay</h3><p>Review every gross amount, statutory input and net amount before approval.</p></div>
            <div class="item-list">@for(i of items();track i.id){<details><summary><b>{{ employeeName(i.employeeId) }}</b><span>Gross {{ i.grossPay | currency:'INR' }} · Net <strong>{{ i.netPay | currency:'INR' }}</strong></span></summary><div class="breakdown"><span>Basic {{ i.basicPay | currency:'INR' }}</span><span>Allowances {{ i.allowances | currency:'INR' }}</span><span>Overtime {{ i.overtimePay | currency:'INR' }}</span><span>Deductions {{ i.deductions | currency:'INR' }}</span><span>TDS {{ i.taxes | currency:'INR' }}</span><pre>{{ formatBreakdown(i.breakdownJson) }}</pre></div></details>} @empty {<p>No calculated items yet.</p>}</div>
          } @else { <div class="empty">Choose a pay run to inspect it.</div> }</section></div>
      }
    </main>`,
  styleUrl: './payroll.page.scss',
})
export class PayrollPage {
  private api = inject(ApiService);
  private toast = inject(ToastService);
  tab = signal<'runs'|'policy'|'profiles'>('runs'); busy = signal(false);
  policy = signal<Policy|null>(null); profile = signal<Profile|null>(null);
  employees = signal<Employee[]>([]); selectedEmployee = signal<Employee|null>(null);
  runs = signal<Run[]>([]); selectedRun = signal<Run|null>(null);
  items = signal<Item[]>([]); adjustments = signal<Adjustment[]>([]);
  employeeSearch = ''; employeePage = 1; employeePages = signal(1);
  paymentReference = '';
  paidOn = new Date(Date.now()+330*60000).toISOString().slice(0,10);
  newRun = { name: '', month: new Date().toISOString().slice(0,7), paymentDate: new Date().toISOString().slice(0,10) };
  newAdjustment = { employeeId: '', code: '', description: '', isEarning: true, amount: 0 };
  constructor() { this.loadPolicy(); this.loadEmployees(); this.loadRuns(); }
  private fail(e: unknown) { const error = e as {error?:{detail?:string;title?:string}}; this.toast.error(error.error?.detail || error.error?.title || 'The action could not be completed. Refresh and try again.'); this.busy.set(false); }
  private start() { this.busy.set(true); }
  loadPolicy() { this.api.get<Policy>('/payroll/policy').subscribe({next:p=>this.policy.set(p),error:e=>this.fail(e)}); }
  savePolicy() { const p=this.policy(); if(!p)return; this.start(); this.api.put<Policy>('/payroll/policy',p).subscribe({next:x=>{this.policy.set(x);this.busy.set(false);this.toast.success('Company policy saved. Recalculate any affected draft runs.');this.loadRuns();},error:e=>this.fail(e)}); }
  loadEmployees() { this.api.employees({page:this.employeePage,pageSize:100,search:this.employeeSearch}).subscribe({next:r=>{this.employees.set(r.items);this.employeePages.set(r.totalPages);},error:e=>this.fail(e)}); }
  changeEmployeePage(delta:number) { this.employeePage=Math.max(1,this.employeePage+delta);this.loadEmployees(); }
  selectEmployee(e:Employee) { this.selectedEmployee.set(e);this.profile.set(null);this.api.get<Profile>(`/payroll/profiles/${e.id}`).subscribe({next:p=>this.profile.set(p),error:x=>this.fail(x)}); }
  saveProfile() { const e=this.selectedEmployee(),p=this.profile();if(!e||!p)return;this.start();this.api.put<Profile>(`/payroll/profiles/${e.id}`,p).subscribe({next:x=>{this.profile.set(x);this.busy.set(false);this.toast.success(`${e.fullName}'s payroll profile saved.`);this.loadRuns();},error:x=>this.fail(x)}); }
  loadRuns() { this.api.get<PagedResult<Run>>('/payroll/runs',{page:1,pageSize:100}).subscribe({next:r=>{this.runs.set(r.items);const latest=r.items.find(x=>x.id===this.selectedRun()?.id);if(latest)this.selectRun(latest);},error:e=>this.fail(e)}); }
  selectRun(r:Run) { this.selectedRun.set(r);this.paymentReference=r.paymentReference??'';this.api.get<Item[]>(`/payroll/runs/${r.id}/items`).subscribe({next:x=>this.items.set(x),error:e=>this.fail(e)});this.api.get<Adjustment[]>(`/payroll/runs/${r.id}/adjustments`).subscribe({next:x=>this.adjustments.set(x),error:e=>this.fail(e)}); }
  createRun() { const x=this.newRun;if(!x.month||!x.name.trim()||!x.paymentDate){this.toast.error('Enter a run name, month and payment date.');return;}const [year,month]=x.month.split('-').map(Number);const end=new Date(Date.UTC(year,month,0)).getUTCDate();this.start();this.api.post<Run>('/payroll/runs',{name:x.name.trim(),periodStart:`${x.month}-01`,periodEnd:`${x.month}-${String(end).padStart(2,'0')}`,paymentDate:x.paymentDate,currency:'INR'}).subscribe({next:r=>{this.busy.set(false);this.toast.success('Pay run created. Add adjustments, then calculate.');this.runs.update(rows=>[r,...rows]);this.selectRun(r);},error:e=>this.fail(e)}); }
  private action(path:string,payload:unknown,success:string){this.start();this.api.post<Run>(path,payload).subscribe({next:r=>{this.busy.set(false);this.toast.success(success);this.runs.update(rows=>rows.map(x=>x.id===r.id?r:x));this.selectRun(r);},error:e=>this.fail(e)});}
  calculate(){const r=this.selectedRun();if(r)this.action(`/payroll/runs/${r.id}/calculate`,{},'Payroll calculated. Review each item and confirm statutory inputs.');}
  review(){const r=this.selectedRun();if(r)this.action(`/payroll/runs/${r.id}/review`,{version:r.version,confirmation:'REVIEWED'},'Statutory review recorded. The run is ready for approval.');}
  approve(){const r=this.selectedRun();if(!r)return;this.start();this.api.put<Run>(`/payroll/runs/${r.id}/status?status=Approved&version=${r.version}`,{}).subscribe({next:x=>{this.busy.set(false);this.toast.success('Payroll approved. Record the bank payment when disbursed.');this.runs.update(rows=>rows.map(y=>y.id===x.id?x:y));this.selectRun(x);},error:e=>this.fail(e)});}
  markPaid(){const r=this.selectedRun();if(!r)return;if(this.paymentReference.trim().length<6||!this.paidOn){this.toast.error('Enter the actual payment date and bank reference (at least 6 characters).');return;}this.action(`/payroll/runs/${r.id}/pay`,{version:r.version,paymentReference:this.paymentReference.trim(),paidOn:this.paidOn},'Payment recorded. Employee payslips are released.');}
  addAdjustment(){const r=this.selectedRun();if(!r)return;const a=this.newAdjustment;if(!a.employeeId||!a.code.trim()||!a.description.trim()||a.amount<=0){this.toast.error('Enter employee, code, description and amount.');return;}this.start();this.api.post<Adjustment>(`/payroll/runs/${r.id}/adjustments`,a).subscribe({next:()=>{this.busy.set(false);this.toast.success('Adjustment saved. Recalculate this run.');this.newAdjustment={employeeId:'',code:'',description:'',isEarning:true,amount:0};this.loadRuns();},error:e=>this.fail(e)});}
  deleteAdjustment(id:string){const r=this.selectedRun();if(!r)return;this.start();this.api.delete(`/payroll/runs/${r.id}/adjustments/${id}`).subscribe({next:()=>{this.busy.set(false);this.toast.success('Adjustment removed. Recalculate this run.');this.loadRuns();},error:e=>this.fail(e)});}
  employeeName(id:string){const e=this.employees().find(x=>x.id===id);return e?`${e.fullName} (${e.employeeNumber})`:id;}
  formatBreakdown(json:string|null){if(!json)return 'No breakdown available.';try{return JSON.stringify(JSON.parse(json),null,2)}catch{return json}}
}
