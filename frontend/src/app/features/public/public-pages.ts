import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { workspaceSlug, workspaceUrl } from '../../core/workspace-url';

@Component({
  selector: 'app-public-home',
  imports: [RouterLink],
  template: `
  <div class="public-site">
    <nav class="public-nav"><a class="logo" routerLink="/"><span class="logo-mark" aria-hidden="true"><i></i><i></i><i></i></span><strong>PeopleFlow<span>.</span></strong></a>
      <div class="nav-links"><a href="#platform">Platform</a><a href="#why">Why PeopleFlow</a><a routerLink="/help">Help</a></div>
      <div class="nav-actions"><a routerLink="/login">Sign in</a><a class="nav-cta" routerLink="/get-started">Start free trial <span>↗</span></a></div>
    </nav>
    <main>
      <section class="hero"><div class="hero-copy"><span class="eyebrow"><i></i> THE PEOPLE OPERATING SYSTEM</span>
        <h1>Everything your people need.<br><em>All in one flow.</em></h1>
        <p>Bring people data, time, work, and everyday HR together in a workspace built for growing teams.</p>
        <div class="hero-actions"><a class="primary-action" routerLink="/get-started">Start your 30-day trial <span>↗</span></a><a class="secondary-action" href="#platform">Explore the platform <span>↓</span></a></div>
        <div class="hero-meta"><div class="avatars"><b>A</b><b>M</b><b>S</b></div><span>No card required to explore<br><strong>Set up in minutes</strong></span></div>
      </div>
      <div class="product-scene" aria-label="PeopleFlow product preview"><div class="scene-glow"></div><div class="scene-panel">
        <aside><div class="mini-logo">P</div><span class="mini-line active"></span><span class="mini-line"></span><span class="mini-line"></span><span class="mini-line"></span><span class="mini-line"></span></aside>
        <div class="scene-content"><div class="scene-top"><span>Overview / Dashboard</span><div class="scene-user">AB</div></div><h3>Good morning, Alex <span>✳</span></h3><p>Here’s what’s happening across your company.</p>
          <div class="metric-row"><div><small>TEAM MEMBERS</small><strong>128</strong><span class="delta">↑ 12.4%</span></div><div><small>ON TIME TODAY</small><strong>96%</strong><span class="delta">↑ 3.2%</span></div><div><small>OPEN REQUESTS</small><strong>08</strong><span class="muted">Needs review</span></div></div>
          <div class="scene-bottom"><div class="chart"><div class="chart-title">Team growth <span>Last 6 months ⌄</span></div><div class="bars"><i style="height:35%"></i><i style="height:48%"></i><i style="height:46%"></i><i style="height:68%"></i><i style="height:76%"></i><i style="height:91%"></i></div><div class="months"><span>APR</span><span>MAY</span><span>JUN</span><span>JUL</span><span>AUG</span><span>SEP</span></div></div><div class="activity"><div class="chart-title">Today’s pulse</div><div><b>✓</b><span>Attendance synced<small>Just now</small></span></div><div><b>↗</b><span>Leave approved<small>12 min ago</small></span></div><div><b>✳</b><span>New team member<small>2 hrs ago</small></span></div></div></div>
        </div></div><div class="float-card"><span>✳</span><div><strong>One connected workspace</strong><small>For every team, every day</small></div></div>
      </div></section>
      <section class="trust-strip"><span>BUILT TO KEEP WORK MOVING</span><div>PEOPLE <i>✳</i> TIME <i>✳</i> WORK <i>✳</i> GROWTH</div></section>
      <section id="platform" class="feature-section"><div class="section-heading"><span class="eyebrow">ONE PLATFORM, EVERY MOMENT</span><h2>Make room for<br><em>better work.</em></h2><p>Less chasing information. More time helping your people do their best work.</p></div>
        <div class="feature-grid"><article class="feature-card feature-large"><span class="feature-icon">◉</span><h3>People & organization</h3><p>Keep employee profiles, reporting lines, roles, and documents in one trusted place.</p><div class="feature-art people-art"><span>AD</span><span>MS</span><span>RK</span><span>+125</span></div></article>
          <article class="feature-card"><span class="feature-icon blue">◷</span><h3>Time & attendance</h3><p>Manage shifts, attendance, leave, holidays, and the moments that matter.</p><div class="mini-calendar"><b>M</b><b>T</b><b>W</b><b>T</b><b>F</b><i>12</i><i>13</i><i class="selected">14</i><i>15</i><i>16</i></div></article>
          <article class="feature-card"><span class="feature-icon violet">▣</span><h3>Work & delivery</h3><p>Plan projects, sprints, assignments, and progress with your whole team.</p><div class="task-art"><div><span></span> Product launch <b>In review</b></div><div><span></span> Onboarding flow <b>Done</b></div></div></article>
          <article class="feature-card"><span class="feature-icon orange">◈</span><h3>Payroll & expenses</h3><p>Prepare payroll runs, review claims, and keep compensation workflows organized.</p></article>
          <article class="feature-card"><span class="feature-icon green">✦</span><h3>Hiring & growth</h3><p>Track candidates, reviews, learning, and the next steps in every career.</p></article>
        </div>
      </section>
      <section id="why" class="why-section"><div><span class="eyebrow">BUILT FOR CLARITY</span><h2>A better view of<br><em>the whole company.</em></h2><p>Give each person the right workspace, from self-service to manager approvals and platform administration. Permissions and company boundaries are enforced in the application.</p><a routerLink="/get-started" class="text-link">Create your workspace <span>↗</span></a></div><div class="why-visual"><div class="orbit orbit-one"></div><div class="orbit orbit-two"></div><div class="orbit-center">P</div><span class="orbit-chip chip-one">People</span><span class="orbit-chip chip-two">Work</span><span class="orbit-chip chip-three">Time</span><span class="orbit-chip chip-four">Growth</span></div></section>
      <section class="cta-section"><span class="eyebrow">READY WHEN YOU ARE</span><h2>Give your team a<br><em>better way to work.</em></h2><p>Start with a 30-day trial. Add your company details as you go.</p><a routerLink="/get-started" class="primary-action">Get started free <span>↗</span></a></section>
    </main><footer class="public-footer"><div class="logo"><span class="logo-mark" aria-hidden="true"><i></i><i></i><i></i></span><strong>PeopleFlow<span>.</span></strong></div><div><a routerLink="/help">Help & support</a><a routerLink="/login">Sign in</a><a routerLink="/get-started">Start trial</a></div><small>© PeopleFlow HRMS</small></footer>
  </div>`,
  styleUrl: './public-pages.scss',
})
export class HomePage {
  constructor() { if (workspaceSlug() && workspaceSlug() !== 'platform') void inject(Router).navigateByUrl('/login'); }
}

@Component({
  selector: 'app-get-started', imports: [FormsModule, RouterLink], styleUrl: './public-pages.scss',
  template: `<div class="public-site form-site"><nav class="public-nav"><a class="logo" routerLink="/"><span class="logo-mark" aria-hidden="true"><i></i><i></i><i></i></span><strong>PeopleFlow<span>.</span></strong></a><div class="nav-actions"><a routerLink="/help">Need help?</a><a routerLink="/login">Sign in</a></div></nav>
    <main class="form-layout"><div class="form-intro"><span class="eyebrow">YOUR NEXT CHAPTER STARTS HERE</span><h1>Your people platform,<br><em>ready in minutes.</em></h1><p>Start a 30-day trial. We’ll email a secure link to set your password and open your company workspace.</p><div class="step-list"><div><b>01</b><span>Create your workspace</span></div><div><b>02</b><span>Activate from your email</span></div><div><b>03</b><span>Explore for 30 days</span></div></div></div>
      <section class="form-card">@if (done) { <span class="success-icon">✓</span><h2>Check your inbox</h2><p>We sent an activation link to <strong>{{ adminEmail }}</strong>. Set your password to start the trial.</p><p>Your workspace: <a [href]="companyUrl">{{ companyUrl }}</a></p><button type="button" class="outline-button" (click)="resend()" [disabled]="busy">Resend activation email</button>@if (resendMessage) { <p class="form-note">{{ resendMessage }}</p> } } @else {
        <span class="card-kicker">GET STARTED FREE</span><h2>Create your company</h2><p>Only the essentials for now. You can finish your company profile later.</p>
        <form (ngSubmit)="submit()"><label>Company name<input name="companyName" [(ngModel)]="companyName" required minlength="2" placeholder="Acme Studio" /></label>
          <label>Workspace address<div class="slug-input"><input name="slug" [(ngModel)]="slug" required minlength="3" pattern="[a-z0-9][a-z0-9-]*[a-z0-9]" placeholder="acme" /><span>.{{ domain }}</span></div></label>
          <div class="form-row"><label>Your name<input name="adminName" [(ngModel)]="adminName" required placeholder="Alex Morgan" /></label><label>Work email<input name="adminEmail" [(ngModel)]="adminEmail" type="email" required placeholder="alex@company.com" /></label></div>
          <label>Plan to explore<select name="preferredPlanCode" [(ngModel)]="preferredPlanCode"><option value="starter">Starter</option><option value="professional">Professional</option><option value="enterprise">Enterprise</option></select></label>
          @if (error) { <p class="form-error">{{ error }}</p> }<button class="form-submit" [disabled]="busy" type="submit">{{ busy ? 'Creating your workspace…' : 'Start 30-day free trial' }} <span>↗</span></button>
          <small>No payment details needed today. Your trial begins when you activate your account.</small></form>
        } </section></main></div>`,
})
export class GetStartedPage {
  private readonly api = inject(ApiService);
  companyName = ''; slug = ''; adminName = ''; adminEmail = ''; preferredPlanCode = 'starter';
  domain = window.location.hostname === 'localhost' ? 'localhost' : window.location.hostname;
  companyUrl = ''; busy = false; done = false; error = ''; resendMessage = '';
  submit(): void {
    if (this.busy) return;
    this.slug = this.slug.trim().toLowerCase(); this.busy = true; this.error = '';
    this.api.post('/public/trials', { companyName: this.companyName, slug: this.slug, adminName: this.adminName,
      adminEmail: this.adminEmail, preferredPlanCode: this.preferredPlanCode, applicationBaseUrl: window.location.origin }).subscribe({
      next: () => { this.busy = false; this.done = true; this.companyUrl = workspaceUrl(this.slug); },
      error: e => { this.busy = false; this.error = e.error?.detail || 'We could not create the workspace. Please try again.'; },
    });
  }
  resend(): void {
    this.busy = true; this.api.post('/public/trials/resend', {
      slug: this.slug, email: this.adminEmail, applicationBaseUrl: window.location.origin,
    }).subscribe({
      next: () => { this.busy = false; this.resendMessage = 'If the account is pending, another email has been queued.'; },
      error: () => { this.busy = false; this.resendMessage = 'Could not resend right now. Please try again later.'; },
    });
  }
}

@Component({
  selector: 'app-activate', imports: [FormsModule, RouterLink], styleUrl: './public-pages.scss',
  template: `<div class="public-site form-site"><nav class="public-nav"><a class="logo" routerLink="/"><span class="logo-mark" aria-hidden="true"><i></i><i></i><i></i></span><strong>PeopleFlow<span>.</span></strong></a><a routerLink="/help">Need help?</a></nav>
  <main class="single-form"><section class="form-card"><span class="card-kicker">SECURE ACCOUNT SETUP</span>@if (done) { <span class="success-icon">✓</span><h1>Your account is ready.</h1><p>Your 30-day trial has started. Sign in to explore your workspace.</p><a class="form-submit" routerLink="/login">Continue to sign in <span>↗</span></a> } @else { <h1>Make it yours.</h1><p>Set your password now. The remaining company details can wait.</p>
    <form (ngSubmit)="submit()"><label>Password<input name="password" [(ngModel)]="password" type="password" required minlength="12" autocomplete="new-password" placeholder="At least 12 characters" /></label>
      <label>Confirm password<input name="confirm" [(ngModel)]="confirm" type="password" required autocomplete="new-password" /></label>
      <label>Legal company name <span class="optional">optional</span><input name="legalName" [(ngModel)]="legalName" placeholder="Add later if you prefer" /></label>
      <label>Time zone <span class="optional">optional</span><select name="timeZone" [(ngModel)]="timeZone"><option value="">Use Asia/Kolkata</option><option value="Asia/Kolkata">Asia/Kolkata</option></select></label>
      @if (error) { <p class="form-error">{{ error }}</p> }<button class="form-submit" [disabled]="busy" type="submit">{{ busy ? 'Activating…' : 'Activate my account' }} <span>↗</span></button></form> }</section></main></div>`,
})
export class ActivatePage {
  private readonly api = inject(ApiService); private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  token = this.route.snapshot.queryParamMap.get('token') ?? '';
  password = ''; confirm = ''; legalName = ''; timeZone = ''; busy = false; done = false; error = '';
  constructor() { this.auth.beginPublicActivation(); }
  submit(): void {
    if (this.password !== this.confirm) { this.error = 'Passwords do not match.'; return; }
    if (!this.token) { this.error = 'This activation link is missing its token.'; return; }
    this.busy = true; this.error = '';
    this.api.post('/public/activate', { token: this.token, password: this.password, legalName: this.legalName, timeZone: this.timeZone }).subscribe({
      next: () => { this.busy = false; this.done = true; this.password = ''; this.confirm = ''; },
      error: e => { this.busy = false; this.error = e.error?.detail || 'This link could not be used. Request another invitation.'; },
    });
  }
}

@Component({
  selector: 'app-public-help', imports: [FormsModule, RouterLink], styleUrl: './public-pages.scss',
  template: `<div class="public-site form-site"><nav class="public-nav"><a class="logo" routerLink="/"><span class="logo-mark" aria-hidden="true"><i></i><i></i><i></i></span><strong>PeopleFlow<span>.</span></strong></a><div class="nav-actions"><a routerLink="/get-started">Start trial</a><a routerLink="/login">Sign in</a></div></nav>
  <main class="form-layout help-layout"><div class="form-intro"><span class="eyebrow">WE'RE HERE TO HELP</span><h1>Good support starts<br><em>with listening.</em></h1><p>Tell us what’s happening. We’ll save your request and route it to the right people.</p><div class="help-points"><div><span>✳</span><strong>Account & access</strong><small>Sign-in, setup, and company workspaces</small></div><div><span>◈</span><strong>Billing questions</strong><small>Plans, trials, and subscriptions</small></div><div><span>◉</span><strong>Product support</strong><small>Features, workflows, and feedback</small></div></div></div>
    <section class="form-card">@if (reference) { <span class="success-icon">✓</span><h2>Request received.</h2><p>Your reference is <strong>{{ reference }}</strong>. Keep it handy if you contact us again.</p><a class="outline-button" routerLink="/">Back to home</a> } @else { <span class="card-kicker">CONTACT SUPPORT</span><h2>Raise a support request</h2><p>Share the details and our team will follow up by email.</p>
      <form (ngSubmit)="submit()"><div class="form-row"><label>Your name<input name="name" [(ngModel)]="contactName" required placeholder="Alex Morgan" /></label><label>Email address<input name="email" [(ngModel)]="contactEmail" type="email" required placeholder="alex@company.com" /></label></div>
      <label>What is this about?<select name="category" [(ngModel)]="category"><option value="general">General question</option><option value="account">Account & access</option><option value="billing">Billing & subscriptions</option><option value="technical">Technical issue</option><option value="feedback">Product feedback</option></select></label>
      <label>Subject<input name="subject" [(ngModel)]="subject" required minlength="5" placeholder="A short summary" /></label><label>Tell us more<textarea name="description" [(ngModel)]="description" required minlength="15" rows="6" placeholder="What happened? What would you like help with?"></textarea></label>
      @if (error) { <p class="form-error">{{ error }}</p> }<button class="form-submit" [disabled]="busy" type="submit">{{ busy ? 'Sending request…' : 'Send support request' }} <span>↗</span></button></form> }</section></main></div>`,
})
export class HelpPage {
  private readonly api = inject(ApiService);
  contactName = ''; contactEmail = ''; category = 'general'; subject = ''; description = ''; busy = false; error = ''; reference = '';
  submit(): void {
    this.busy = true; this.error = '';
    this.api.post<{reference: string}>('/public/support-tickets', { contactName: this.contactName, contactEmail: this.contactEmail,
      category: this.category, subject: this.subject, description: this.description }).subscribe({
      next: value => { this.busy = false; this.reference = value.reference; },
      error: e => { this.busy = false; this.error = e.error?.detail || 'We could not save the request. Please try again.'; },
    });
  }
}
