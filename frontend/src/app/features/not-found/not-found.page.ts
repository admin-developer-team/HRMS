import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink, MatIconModule],
  template: `
    <main class="not-found-page">
      <section class="not-found-card" aria-labelledby="not-found-title">
        <span class="not-found-code">404</span>
        <mat-icon aria-hidden="true">travel_explore</mat-icon>
        <h1 id="not-found-title">Page not found</h1>
        <p>The address may be incorrect, or the page may no longer be available.</p>
        <a routerLink="/dashboard" class="not-found-action">Go to dashboard</a>
      </section>
    </main>
  `,
  styles: [`
    .not-found-page{min-height:calc(100dvh - 64px);display:grid;place-items:center;padding:24px;background:var(--app-bg);color:var(--ink)}
    .not-found-card{width:min(100%,520px);padding:clamp(32px,7vw,64px) 28px;text-align:center;background:var(--card);border:1px solid var(--border);border-radius:var(--radius);box-shadow:var(--shadow)}
    .not-found-code{display:block;color:var(--brand);font-size:clamp(4rem,13vw,7rem);font-weight:800;line-height:.9;letter-spacing:-.08em}
    mat-icon{width:42px;height:42px;margin:24px 0 12px;color:var(--brand);font-size:42px}
    h1{margin:0;font-size:1.75rem;letter-spacing:-.03em}p{margin:10px auto 28px;max-width:360px;color:var(--ink-muted);line-height:1.6}
    .not-found-action{display:inline-flex;align-items:center;justify-content:center;min-height:44px;padding:0 18px;border-radius:var(--radius);background:var(--brand);color:var(--on-brand);font-weight:700;text-decoration:none}
    .not-found-action:focus-visible{outline:3px solid rgb(var(--brand-rgb) / .4);outline-offset:3px}
  `],
})
export class NotFoundPage {}
