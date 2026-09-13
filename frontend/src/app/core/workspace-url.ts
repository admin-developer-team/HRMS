import { environment } from '../../environments/environment';

export function workspaceSlug(): string | null {
  const domain = environment.tenantBaseDomain.toLowerCase();
  const host = window.location.hostname.toLowerCase();
  if (host === domain) return 'platform';
  const suffix = `.${domain}`;
  if (!host.endsWith(suffix)) return null;
  const slug = host.slice(0, -suffix.length);
  return /^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$/.test(slug) ? slug : null;
}

export function workspaceUrl(slug: string): string {
  const domain = environment.tenantBaseDomain;
  const host = slug === 'platform' ? domain : `${slug}.${domain}`;
  return `${window.location.protocol}//${host}${window.location.port ? `:${window.location.port}` : ''}`;
}
