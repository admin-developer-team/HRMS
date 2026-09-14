import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';

interface Payslip {
  payrollRunId:string; runName:string; periodStart:string; periodEnd:string; paymentDate:string; currency:string;
  basicPay:number; allowances:number; overtimePay:number; deductions:number; taxes:number; grossPay:number; netPay:number;
  status:string; breakdownJson:string|null; companyName:string|null; employeeName:string|null; employeeNumber:string|null; paymentReference:string|null;
}
interface Breakdown {
  basic?:number; hra?:number; specialAllowance?:number; lossOfPay?:number; overtime?:number; overtimeHours?:number;
  pfEmployee?:number; pfEmployer?:number; esiEmployee?:number; esiEmployer?:number;
  professionalTax?:number; tds?:number; otherDeduction?:number; lostDays?:number;
  adjustments?:{code:string;description:string;isEarning:boolean;amount:number}[];
}

@Component({
  selector:'app-payslip-page',imports:[DatePipe,RouterLink],
  template:`<main class="payslip-page"><div class="screen-actions"><a routerLink="/my-services">← Back to my services</a><button (click)="print()" [disabled]="!payslip()">Print or save as PDF</button></div>
    @if(error()){<p class="error" role="alert">{{error()}}</p>}
    @if(payslip();as p){<article class="sheet"><header><div><span class="eyebrow">EMPLOYEE PAYSLIP</span><h1>{{p.companyName||'Company'}}</h1><p>{{p.runName}} · {{p.periodStart|date:'mediumDate'}} – {{p.periodEnd|date:'mediumDate'}}</p></div><div class="stamp">{{p.status}}</div></header>
      <div class="info"><div><small>Employee</small><strong>{{p.employeeName}}</strong><span>{{p.employeeNumber}}</span></div><div><small>Payment date</small><strong>{{p.paymentDate|date:'mediumDate'}}</strong><span>Reference {{p.paymentReference||'—'}}</span></div></div>
      <div class="columns"><section><h2>Earnings</h2><div class="line"><span>Basic salary</span><strong>{{money(details().basic??p.basicPay)}}</strong></div><div class="line"><span>House rent allowance</span><strong>{{money(details().hra??0)}}</strong></div><div class="line"><span>Special allowance</span><strong>{{money(details().specialAllowance??0)}}</strong></div><div class="line"><span>Overtime @if(details().overtimeHours){({{details().overtimeHours}} h)}</span><strong>{{money(p.overtimePay)}}</strong></div>
          @for(a of earningAdjustments();track a.code){<div class="line"><span>{{a.description}}</span><strong>{{money(a.amount)}}</strong></div>}
          <div class="line total"><span>Gross earnings</span><strong>{{money(p.grossPay)}}</strong></div></section>
        <section><h2>Deductions</h2><div class="line"><span>Loss of pay @if(details().lostDays){({{details().lostDays}} days)}</span><strong>{{money(details().lossOfPay??0)}}</strong></div><div class="line"><span>Employee PF</span><strong>{{money(details().pfEmployee??0)}}</strong></div><div class="line"><span>Employee ESI</span><strong>{{money(details().esiEmployee??0)}}</strong></div><div class="line"><span>Professional tax</span><strong>{{money(details().professionalTax??0)}}</strong></div><div class="line"><span>Income tax deducted (TDS)</span><strong>{{money(p.taxes)}}</strong></div><div class="line"><span>Other deductions</span><strong>{{money(details().otherDeduction??0)}}</strong></div>
          @for(a of deductionAdjustments();track a.code){<div class="line"><span>{{a.description}}</span><strong>{{money(a.amount)}}</strong></div>}
          <div class="line total"><span>Total deductions & tax</span><strong>{{money(p.deductions+p.taxes)}}</strong></div></section></div>
      <div class="net"><div><small>NET PAY</small><strong>{{money(p.netPay)}}</strong></div><span>Indian rupees</span></div>
      <footer>Generated from the approved payroll run. {{p.status==='Paid'?'Payment has been recorded.':'Payment is not yet recorded.'}} Keep this payslip for your records. If any amount appears incorrect, contact your payroll administrator.</footer></article>
    } @else if(!error()){<p class="loading">Loading payslip…</p>}
  </main>`,
  styles:[`.payslip-page{max-width:950px;margin:auto;padding:30px 20px 80px;color:var(--ink)}.screen-actions{display:flex;justify-content:space-between;align-items:center;margin-bottom:20px}.screen-actions a{color:var(--brand);text-decoration:none;font-weight:700}.screen-actions button{background:var(--brand);border:0;border-radius:9px;color:#fff;padding:10px 16px;font:inherit;font-weight:700;cursor:pointer}.sheet{background:var(--card);border:1px solid var(--border);border-radius:18px;padding:38px 43px;box-shadow:0 12px 35px #17314912}.sheet header{display:flex;justify-content:space-between;gap:25px;border-bottom:2px solid var(--brand);padding-bottom:25px}.eyebrow{color:var(--brand);font-size:11px;letter-spacing:.14em;font-weight:800}.sheet h1{font-size:28px;margin:9px 0}.sheet header p,.info span{color:var(--ink-muted);margin:0}.stamp{align-self:start;border:2px solid #30985b;color:#23864c;border-radius:8px;padding:8px 15px;font-size:15px;letter-spacing:.14em;font-weight:900;transform:rotate(-6deg)}.info{display:grid;grid-template-columns:1fr 1fr;gap:20px;padding:24px 0;border-bottom:1px solid var(--border)}.info small,.info strong,.info span{display:block}.info small{font-size:11px;color:var(--ink-muted);text-transform:uppercase;letter-spacing:.1em}.info strong{font-size:15px;margin:5px 0}.columns{display:grid;grid-template-columns:1fr 1fr;gap:35px;margin:22px 0 30px}.columns h2{font-size:17px;padding-bottom:12px;border-bottom:1px solid var(--border)}.line{display:flex;justify-content:space-between;gap:12px;padding:9px 0;font-size:13px}.line span{color:var(--ink-muted)}.line strong{white-space:nowrap}.line.total{border-top:1px solid var(--border);margin-top:12px;font-weight:800}.line.total span{color:var(--ink)}.net{display:flex;align-items:end;justify-content:space-between;background:var(--brand);color:white;border-radius:11px;padding:20px 24px}.net small,.net strong{display:block}.net small{font-size:11px;letter-spacing:.12em}.net strong{font-size:27px;margin-top:5px}.net span{font-size:12px;opacity:.85}.sheet footer{border-top:1px solid var(--border);margin-top:28px;padding-top:17px;color:var(--ink-muted);font-size:11px;line-height:1.5}.error{padding:14px;background:#fff0ee;color:#9b332c;border-radius:9px}.loading{text-align:center;padding:80px;color:var(--ink-muted)}@media(max-width:650px){.sheet{padding:22px}.columns,.info{grid-template-columns:1fr}.screen-actions{gap:10px}.screen-actions button{font-size:12px}}@media print{.screen-actions{display:none}.payslip-page{padding:0;max-width:none}.sheet{border:0;box-shadow:none;border-radius:0;padding:10mm}.sheet footer{margin-top:20px}@page{size:A4;margin:12mm}}`]
})
export class PayslipPage {
  private api=inject(ApiService); private route=inject(ActivatedRoute);
  payslip=signal<Payslip|null>(null); error=signal(''); details=signal<Breakdown>({});
  earningAdjustments=signal<NonNullable<Breakdown['adjustments']>>([]);
  deductionAdjustments=signal<NonNullable<Breakdown['adjustments']>>([]);
  private formatter=new Intl.NumberFormat('en-IN',{style:'currency',currency:'INR',maximumFractionDigits:2});
  constructor(){const runId=this.route.snapshot.paramMap.get('runId');this.api.get<Payslip[]>('/me/payslips').subscribe({next:rows=>{const p=rows.find(x=>x.payrollRunId===runId);if(!p){this.error.set('This payslip is unavailable.');return;}this.payslip.set(p);try{const d=JSON.parse(p.breakdownJson||'{}') as Breakdown;this.details.set(d);this.earningAdjustments.set((d.adjustments??[]).filter(x=>x.isEarning));this.deductionAdjustments.set((d.adjustments??[]).filter(x=>!x.isEarning));}catch{this.details.set({});}},error:()=>this.error.set('Could not load the payslip.')});}
  money(amount:number){return this.formatter.format(amount)}
  print(){window.print()}
}
