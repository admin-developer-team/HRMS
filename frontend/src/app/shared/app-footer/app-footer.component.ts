import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-footer',
  imports: [RouterLink],
  template: `@if (show()) {
    <footer class="app-footer">
      <section class="footer-main">
        <div class="footer-brand"><strong>PeopleFlow</strong><p>One secure workspace for your people, time, and work.</p><span><i></i>Service status: operational</span></div>
        <nav aria-label="Product"><b>Product</b><a routerLink="/get-started">Plans & pricing</a><a routerLink="/">Platform overview</a><a routerLink="/login">Sign in</a></nav>
        <nav aria-label="Company"><b>Company</b><a routerLink="/">About PeopleFlow</a><a routerLink="/get-started">Create a workspace</a><a routerLink="/help">Contact us</a></nav>
        <nav aria-label="Support"><b>Support</b><a routerLink="/help">Help centre</a><a routerLink="/help">Contact HR</a><span>Tenant-isolated access</span></nav>
      </section>
      <section class="footer-legal"><span>© 2026 PeopleFlow HRMS. All rights reserved.</span><div><a routerLink="/help">Privacy</a><a routerLink="/help">Terms</a><a routerLink="/help">Security</a></div></section>
    </footer>
  }`,
  styles: `.app-footer{padding:44px clamp(20px,5vw,72px) 18px;border-top:1px solid var(--border);background:var(--sidebar-bg);color:rgb(255 255 255 / .7);font:inherit}.footer-main{max-width:1400px;margin:auto;display:grid;grid-template-columns:minmax(240px,2fr) repeat(3,minmax(130px,1fr));gap:32px}.footer-brand strong{color:#fff;font-size:1.1rem}.footer-brand p{max-width:300px;margin:10px 0 16px;font-size:.82rem;line-height:1.6}.footer-brand span{display:flex;align-items:center;gap:7px;font-size:.72rem}.footer-brand i{width:7px;height:7px;border-radius:50%;background:#6ce9a6}.app-footer nav{display:grid;align-content:start;gap:9px}.app-footer nav b{margin-bottom:3px;color:#fff;font-size:.78rem}.app-footer a{color:rgb(255 255 255 / .7);font-size:.78rem}.app-footer a:hover{color:#fff;text-decoration:underline;text-underline-offset:3px}.app-footer nav span{font-size:.72rem}.footer-legal{max-width:1400px;margin:38px auto 0;padding-top:16px;border-top:1px solid rgb(255 255 255 / .14);display:flex;align-items:center;justify-content:space-between;gap:16px;font-size:.7rem}.footer-legal div{display:flex;gap:16px}@media(max-width:760px){.footer-main{grid-template-columns:1fr 1fr}.footer-brand{grid-column:1/-1}.footer-legal{align-items:flex-start;flex-direction:column;gap:10px}}@media(max-width:420px){.footer-main{grid-template-columns:1fr}.footer-brand{grid-column:auto}}`,
})
export class AppFooterComponent {
  readonly show = input(true);
}
