import { DOCUMENT } from '@angular/common';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { AuthService } from './auth.service';
import { workspaceSlug } from './workspace-url';

export interface TenantTheme {
  id: string;
  name: string;
  description: string;
  scheme: 'light' | 'dark';
  primary: string;
  primaryRgb: string;
  onPrimary: string;
  accent: string;
  surface: string;
  sidebar: string;
  radius: number;
  density: 'comfortable' | 'compact';
}

export const THEME_PRESETS: TenantTheme[] = [
  {
    id: 'azure',
    name: 'Azure',
    description: 'Clear, focused and familiar',
    scheme: 'light',
    primary: '#0067b8',
    primaryRgb: '0 103 184',
    onPrimary: '#ffffff',
    accent: '#2b88d8',
    surface: '#f5f7fb',
    sidebar: '#071426',
    radius: 8,
    density: 'comfortable',
  },
  {
    id: 'azure-dark',
    name: 'Azure Night',
    description: 'Low-glare operations view',
    scheme: 'dark',
    primary: '#60a5fa',
    primaryRgb: '96 165 250',
    onPrimary: '#08111f',
    accent: '#38bdf8',
    surface: '#08111f',
    sidebar: '#030712',
    radius: 8,
    density: 'comfortable',
  },
  {
    id: 'black-white',
    name: 'Black & White',
    description: 'High-contrast monochrome workspace',
    scheme: 'dark',
    primary: '#ffffff',
    primaryRgb: '255 255 255',
    onPrimary: '#000000',
    accent: '#d4d4d4',
    surface: '#000000',
    sidebar: '#000000',
    radius: 8,
    density: 'comfortable',
  },
  {
    id: 'emerald',
    name: 'Evergreen',
    description: 'Calm green workspace',
    scheme: 'light',
    primary: '#047857',
    primaryRgb: '4 120 87',
    onPrimary: '#ffffff',
    accent: '#10b981',
    surface: '#f4f8f6',
    sidebar: '#06251d',
    radius: 8,
    density: 'comfortable',
  },
  {
    id: 'graphite',
    name: 'Graphite',
    description: 'Dense neutral workspace',
    scheme: 'light',
    primary: '#334155',
    primaryRgb: '51 65 85',
    onPrimary: '#ffffff',
    accent: '#64748b',
    surface: '#f3f4f6',
    sidebar: '#111827',
    radius: 8,
    density: 'compact',
  },
  {
    id: 'indigo',
    name: 'Indigo',
    description: 'Modern professional workspace',
    scheme: 'light',
    primary: '#4f46e5',
    primaryRgb: '79 70 229',
    onPrimary: '#ffffff',
    accent: '#6366f1',
    surface: '#f7f7fc',
    sidebar: '#17153b',
    radius: 8,
    density: 'comfortable',
  },
  {
    id: 'obsidian-gold',
    name: 'Obsidian Gold',
    description: 'Deep black workspace with warm gold accents',
    scheme: 'dark',
    primary: '#f5b942',
    primaryRgb: '245 185 66',
    onPrimary: '#17120a',
    accent: '#facc15',
    surface: '#0b0b0c',
    sidebar: '#050505',
    radius: 8,
    density: 'comfortable',
  },
  {
    id: 'slate-dark',
    name: 'Slate Night',
    description: 'Neutral dark workspace',
    scheme: 'dark',
    primary: '#93c5fd',
    primaryRgb: '147 197 253',
    onPrimary: '#0f172a',
    accent: '#7dd3fc',
    surface: '#0f172a',
    sidebar: '#020617',
    radius: 8,
    density: 'comfortable',
  },
  {
    id: 'teal',
    name: 'Teal',
    description: 'Balanced and calm workspace',
    scheme: 'light',
    primary: '#0f766e',
    primaryRgb: '15 118 110',
    onPrimary: '#ffffff',
    accent: '#14b8a6',
    surface: '#f3f9f8',
    sidebar: '#082f2d',
    radius: 8,
    density: 'comfortable',
  },
];

const PUBLIC_THEME_KEY = 'peopleflow.theme.public';
const WORKSPACE_THEME_KEY_PREFIX = 'peopleflow.theme.workspace.';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly auth = inject(AuthService);
  private readonly selected = signal<TenantTheme>(THEME_PRESETS.find(theme => theme.id === 'obsidian-gold')!);
  readonly presets = THEME_PRESETS;
  readonly current = this.selected.asReadonly();
  readonly isDark = computed(() => this.current().scheme === 'dark');

  constructor() {
    effect(() => {
      const tenantId = this.auth.tenantId();
      // Before sign-in there is no tenant ID in the session.  The hostname still
      // identifies the workspace, so use that key to keep its login screen in
      // sync with the theme the user selected after signing in.
      const workspaceThemeKey = `${WORKSPACE_THEME_KEY_PREFIX}${workspaceSlug()}`;
      const saved = localStorage.getItem(`peopleflow.theme.${tenantId}`)
        ?? (tenantId === 'anonymous' ? localStorage.getItem(workspaceThemeKey) : null)
        ?? (tenantId === 'anonymous' ? localStorage.getItem(PUBLIC_THEME_KEY) : null);
      let theme = THEME_PRESETS.find(theme => theme.id === 'obsidian-gold')!;
      if (saved) {
        try {
          const stored = JSON.parse(saved) as TenantTheme;
          const preset = THEME_PRESETS.find(candidate => candidate.id === stored.id);
          theme = {
            ...stored,
            onPrimary: stored.onPrimary ?? preset?.onPrimary ?? '#ffffff',
            radius: Math.min(8, Math.max(4, stored.radius ?? 8)),
          };
        } catch {
          localStorage.removeItem(`peopleflow.theme.${tenantId}`);
        }
      }
      // Migrate themes selected before the workspace key existed, so the next
      // visit to this workspace's sign-in page has the same appearance.
      if (tenantId !== 'anonymous') {
        localStorage.setItem(workspaceThemeKey, JSON.stringify(theme));
      }
      this.selected.set(theme);
      this.apply(theme);
    });
  }

  select(theme: TenantTheme): void {
    theme = { ...theme, radius: Math.min(8, Math.max(4, theme.radius)) };
    const tenantId = this.auth.tenantId();
    localStorage.setItem(`peopleflow.theme.${tenantId}`, JSON.stringify(theme));
    localStorage.setItem(`${WORKSPACE_THEME_KEY_PREFIX}${workspaceSlug()}`, JSON.stringify(theme));
    localStorage.setItem(PUBLIC_THEME_KEY, JSON.stringify(theme));
    this.selected.set(theme);
    this.apply(theme);
  }

  private apply(theme: TenantTheme): void {
    const root = this.document.documentElement;
    root.dataset['theme'] = theme.id;
    root.dataset['scheme'] = theme.scheme;
    root.dataset['density'] = theme.density;
    root.style.setProperty('--brand', theme.primary);
    root.style.setProperty('--brand-rgb', theme.primaryRgb);
    root.style.setProperty('--on-brand', theme.onPrimary);
    root.style.setProperty('--accent', theme.accent);
    root.style.setProperty('--app-bg', theme.surface);
    root.style.setProperty('--sidebar-bg', theme.sidebar);
    root.style.setProperty('--radius', `${theme.radius}px`);
    root.style.setProperty('--mat-sys-primary', theme.primary);
    root.style.setProperty('color-scheme', theme.scheme);
  }
}
